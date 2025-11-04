using System.Collections.Concurrent;

namespace Cocoar.FileSystem;

/// <summary>
/// Production-ready FileSystemWatcher with automatic fallback, error recovery, and debouncing.
/// Handles common issues: folder not existing initially, permission errors, watcher failures.
/// Automatically switches between FileSystemWatcher (efficient) and polling (resilient) as needed.
/// </summary>
public sealed class ResilientFileSystemMonitor : IDisposable
{
    private readonly Options _options;
    private readonly object _lock = new();
    private readonly Timer _pollingTimer;
    private readonly ConcurrentDictionary<string, DateTime> _debounceTracker = new();
    
    private FileSystemWatcher? _watcher;
    private bool _isPolling;
    private bool _disposed;
    private Dictionary<string, DateTime> _lastSeenFiles = new();

    /// <summary>
    /// Configuration options for the resilient file system monitor.
    /// </summary>
    public sealed record Options
    {
        /// <summary>
        /// The directory path to monitor.
        /// </summary>
        public required string Path { get; init; }
        
        /// <summary>
        /// The filter string used to determine what files are monitored (default: "*").
        /// </summary>
        public string Filter { get; init; } = "*";
        
        /// <summary>
        /// Whether to monitor subdirectories (default: false).
        /// </summary>
        public bool IncludeSubdirectories { get; init; }
        
        /// <summary>
        /// The type of changes to watch for (default: LastWrite | FileName | CreationTime).
        /// </summary>
        public NotifyFilters NotifyFilter { get; init; } = 
            NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
        
        /// <summary>
        /// Enable automatic fallback to polling when FileSystemWatcher fails or directory doesn't exist (default: true).
        /// </summary>
        public bool EnablePollingFallback { get; init; } = true;
        
        /// <summary>
        /// Interval for polling when in fallback mode (default: 5 seconds).
        /// </summary>
        public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);
        
        /// <summary>
        /// Automatically recover from errors by switching to polling (default: true).
        /// </summary>
        public bool AutoRecoverFromErrors { get; init; } = true;
        
        /// <summary>
        /// Optional debounce time to reduce noise from rapid file changes (default: null = no debouncing).
        /// When set, multiple changes to the same file within this time window will only fire one event.
        /// </summary>
        public TimeSpan? DebounceTime { get; init; }
    }

    /// <summary>
    /// Occurs when a file or directory is changed.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? Changed;
    
    /// <summary>
    /// Occurs when a file or directory is created.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? Created;
    
    /// <summary>
    /// Occurs when a file or directory is deleted.
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? Deleted;
    
    /// <summary>
    /// Occurs when a file or directory is renamed.
    /// </summary>
    public event EventHandler<RenamedEventArgs>? Renamed;
    
    /// <summary>
    /// Occurs when an error is encountered (for diagnostics/logging).
    /// </summary>
    public event EventHandler<ErrorEventArgs>? Error;
    
    /// <summary>
    /// Occurs when the monitor switches between watcher and polling modes.
    /// </summary>
    public event EventHandler<MonitorModeChangedEventArgs>? ModeChanged;

    /// <summary>
    /// Returns true if currently using FileSystemWatcher (efficient mode).
    /// </summary>
    public bool IsWatcherActive
    {
        get
        {
            lock (_lock)
            {
                return _watcher != null && !_disposed;
            }
        }
    }

    /// <summary>
    /// Returns true if currently using polling (resilient fallback mode).
    /// </summary>
    public bool IsPolling
    {
        get
        {
            lock (_lock)
            {
                return _isPolling && !_disposed;
            }
        }
    }

    public ResilientFileSystemMonitor(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Path);
        
        _options = options;
        _pollingTimer = new Timer(PollingCallback, null, Timeout.Infinite, Timeout.Infinite);
        
        StartMonitoring();
    }

    private void StartMonitoring()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            if (Directory.Exists(_options.Path))
            {
                TryStartFileSystemWatcher();
            }
            else if (_options.EnablePollingFallback)
            {
                StartPolling();
            }
        }
    }

    private void TryStartFileSystemWatcher()
    {
        lock (_lock)
        {
            if (_disposed || _watcher != null)
                return;

            try
            {
                _watcher = new FileSystemWatcher(_options.Path, _options.Filter)
                {
                    IncludeSubdirectories = _options.IncludeSubdirectories,
                    NotifyFilter = _options.NotifyFilter,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileSystemCreated;
                _watcher.Changed += OnFileSystemChanged;
                _watcher.Deleted += OnFileSystemDeleted;
                _watcher.Renamed += OnFileSystemRenamed;
                _watcher.Error += OnFileSystemWatcherError;

                _isPolling = false;
                _pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                ModeChanged?.Invoke(this, new MonitorModeChangedEventArgs(MonitorMode.Watcher, "FileSystemWatcher active"));
            }
            catch (Exception ex)
            {
                _watcher?.Dispose();
                _watcher = null;
                
                Error?.Invoke(this, new ErrorEventArgs(ex));
                
                if (_options.EnablePollingFallback && _options.AutoRecoverFromErrors)
                {
                    StartPolling();
                }
            }
        }
    }

    private void StartPolling()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            StopFileSystemWatcher();
            _isPolling = true;
            
            RefreshFileSnapshot();
            
            _pollingTimer.Change(TimeSpan.Zero, _options.PollingInterval);
            
            ModeChanged?.Invoke(this, new MonitorModeChangedEventArgs(MonitorMode.Polling, "Polling fallback active"));
        }
    }

    private void PollingCallback(object? state)
    {
        lock (_lock)
        {
            if (_disposed || !_isPolling)
                return;

            if (Directory.Exists(_options.Path) && _watcher == null)
            {
                TryStartFileSystemWatcher();
                return;
            }

            DetectChanges();
        }
    }

    private void RefreshFileSnapshot()
    {
        try
        {
            if (!Directory.Exists(_options.Path))
            {
                _lastSeenFiles.Clear();
                return;
            }

            var searchOption = _options.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            var currentFiles = Directory.GetFiles(_options.Path, _options.Filter, searchOption)
                .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));

            _lastSeenFiles = currentFiles;
        }
        catch
        {
            // Ignore errors during snapshot refresh
        }
    }

    private void DetectChanges()
    {
        try
        {
            if (!Directory.Exists(_options.Path))
                return;

            var searchOption = _options.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            var currentFiles = Directory.GetFiles(_options.Path, _options.Filter, searchOption)
                .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));

            foreach (var file in currentFiles.Keys.Except(_lastSeenFiles.Keys))
            {
                RaiseEvent(() => Created?.Invoke(this, new FileSystemEventArgs(WatcherChangeTypes.Created, 
                    Path.GetDirectoryName(file) ?? _options.Path, Path.GetFileName(file))), file);
            }

            foreach (var file in _lastSeenFiles.Keys.Except(currentFiles.Keys))
            {
                RaiseEvent(() => Deleted?.Invoke(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, 
                    Path.GetDirectoryName(file) ?? _options.Path, Path.GetFileName(file))), file);
            }

            foreach (var file in currentFiles.Keys.Intersect(_lastSeenFiles.Keys))
            {
                if (currentFiles[file] != _lastSeenFiles[file])
                {
                    RaiseEvent(() => Changed?.Invoke(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, 
                        Path.GetDirectoryName(file) ?? _options.Path, Path.GetFileName(file))), file);
                }
            }

            _lastSeenFiles = currentFiles;
        }
        catch
        {
            // Ignore errors during change detection
        }
    }

    private void OnFileSystemCreated(object sender, FileSystemEventArgs e)
    {
        RaiseEvent(() => Created?.Invoke(this, e), e.FullPath);
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        RaiseEvent(() => Changed?.Invoke(this, e), e.FullPath);
    }

    private void OnFileSystemDeleted(object sender, FileSystemEventArgs e)
    {
        RaiseEvent(() => Deleted?.Invoke(this, e), e.FullPath);
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        RaiseEvent(() => Renamed?.Invoke(this, e), e.FullPath);
    }

    private void OnFileSystemWatcherError(object sender, ErrorEventArgs e)
    {
        Error?.Invoke(this, e);
        
        if (_options.AutoRecoverFromErrors && _options.EnablePollingFallback)
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    StartPolling();
                }
            }
        }
    }

    private void RaiseEvent(Action raiseAction, string filePath)
    {
        if (_options.DebounceTime is { } debounce)
        {
            var now = DateTime.UtcNow;
            var shouldRaise = false;

            if (_debounceTracker.TryGetValue(filePath, out var lastEventTime))
            {
                if (now - lastEventTime >= debounce)
                {
                    _debounceTracker[filePath] = now;
                    shouldRaise = true;
                }
            }
            else
            {
                _debounceTracker[filePath] = now;
                shouldRaise = true;
            }

            if (shouldRaise)
            {
                raiseAction();
            }
        }
        else
        {
            raiseAction();
        }
    }

    private void StopFileSystemWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileSystemCreated;
            _watcher.Changed -= OnFileSystemChanged;
            _watcher.Deleted -= OnFileSystemDeleted;
            _watcher.Renamed -= OnFileSystemRenamed;
            _watcher.Error -= OnFileSystemWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            StopFileSystemWatcher();
            _pollingTimer?.Dispose();
            _debounceTracker.Clear();
            _lastSeenFiles.Clear();
        }
    }
}

/// <summary>
/// Event args for when the monitor switches modes.
/// </summary>
public sealed class MonitorModeChangedEventArgs : EventArgs
{
    public MonitorMode NewMode { get; }
    public string Reason { get; }

    public MonitorModeChangedEventArgs(MonitorMode newMode, string reason)
    {
        NewMode = newMode;
        Reason = reason;
    }
}

/// <summary>
/// Monitoring mode.
/// </summary>
public enum MonitorMode
{
    /// <summary>
    /// Using FileSystemWatcher (efficient, real-time).
    /// </summary>
    Watcher,
    
    /// <summary>
    /// Using polling (resilient fallback).
    /// </summary>
    Polling
}
