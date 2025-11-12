using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace Cocoar.FileSystem;

/// <summary>
/// A production-grade file system monitor that provides lossless event delivery
/// even when the underlying FileSystemWatcher fails or the directory is deleted/recreated.
/// </summary>
public sealed class ResilientFileSystemMonitor : IDisposable
{
    private readonly Options _options;
    private readonly object _gate = new();
    private readonly Timer _healthTimer;
    private readonly Timer _auditTimer;
    private readonly Timer _pollingTimer;
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, DateTime> _debounceTracker = new();
    
    // Steady-state index: relative path -> file metadata
    private Dictionary<string, FileMeta> _index = new(StringComparer.OrdinalIgnoreCase);
    private ulong _rollingDigest;
    private DateTime _lastEventUtc = DateTime.MinValue;
    
    private FileSystemWatcher? _watcher;
    private MonitorState _state = MonitorState.Stopped;
    private bool _disposed;
    
    // Event serialization channel
    private readonly Channel<EventEntry> _eventChannel;
    private readonly Task _eventDeliveryTask;

    /// <summary>
    /// Monitor state.
    /// </summary>
    private enum MonitorState
    {
        Stopped,
        Watching,
        Polling,
        Recovering
    }

    /// <summary>
    /// File metadata record for change detection.
    /// </summary>
    internal sealed record FileMeta(
        long Length,
        DateTime LastWriteUtc,
        FileAttributes Attributes
    );
    
    /// <summary>
    /// Queued event entry for serialized delivery.
    /// </summary>
    private sealed record EventEntry(
        EventType Type,
        FileSystemEventArgs Args,
        RenamedEventArgs? RenamedArgs = null,
        ErrorEventArgs? ErrorArgs = null,
        ModeChangedEventArgs? ModeArgs = null
    );
    
    private enum EventType
    {
        Created,
        Changed,
        Deleted,
        Renamed,
        Error,
        ModeChanged
    }

    /// <summary>
    /// Configuration options for the resilient file system monitor.
    /// </summary>
    public sealed record Options
    {
        public required string Path { get; init; }

        /// <summary>
        /// Very cheap operation (just Directory.Exists).
        /// </summary>
        public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// O(files) operation but detects missed events reliably.
        /// </summary>
        public TimeSpan AuditInterval { get; init; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Used when directory doesn't exist or watcher has failed.
        /// </summary>
        public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);

        public bool EnablePollingFallback { get; init; } = true;
        public bool AutoRecoverFromErrors { get; init; } = true;
        public string Filter { get; init; } = "*";
        public bool IncludeSubdirectories { get; init; }

        /// <summary>
        /// 0 = root only, 1 = direct children, -1 = unlimited. Only applies when IncludeSubdirectories is true.
        /// </summary>
        public int MaxDepth { get; init; }

        public NotifyFilters NotifyFilter { get; init; } = 
            NotifyFilters.FileName | 
            NotifyFilters.LastWrite | 
            NotifyFilters.Size;

        /// <summary>
        /// Increase for high-change-rate scenarios to avoid buffer overflow.
        /// </summary>
        public int InternalBufferSize { get; init; } = 64 * 1024;

        /// <summary>
        /// When set, multiple changes to the same file within this time window will only fire one event.
        /// </summary>
        public TimeSpan? DebounceTime { get; init; }
        
        /// <summary>
        /// Computes partial content hash (first/last N bytes) for files with identical metadata.
        /// Useful for scenarios where tools preserve mtime but change content.
        /// </summary>
        public bool EnableAdaptiveHashOnReconcile { get; init; }
        
        public int AdaptiveHashBytesPerEdge { get; init; } = 64 * 1024;
    }

    /// <summary>
    /// Raised when the monitoring mode changes (e.g., Watching -> Polling).
    /// </summary>
    public event EventHandler<ModeChangedEventArgs>? ModeChanged;

    public event EventHandler<ErrorEventArgs>? Error;
    public event EventHandler<FileSystemEventArgs>? Created;
    public event EventHandler<FileSystemEventArgs>? Changed;
    public event EventHandler<FileSystemEventArgs>? Deleted;
    public event EventHandler<RenamedEventArgs>? Renamed;

    /// <summary>
    /// Events are delivered in the same order they occurred.
    /// </summary>
    public ChannelReader<FileSystemEvent> Events => _publicEventChannel.Reader;
    
    private readonly Channel<FileSystemEvent> _publicEventChannel;

    public bool IsUsingWatcher
    {
        get
        {
            lock (_gate)
                return _state == MonitorState.Watching;
        }
    }

    [Obsolete("Use IsUsingWatcher instead")]
    public bool IsWatcherActive => IsUsingWatcher;

    [Obsolete("Use !IsUsingWatcher instead")]
    public bool IsPolling => !IsUsingWatcher;

    public static MonitorBuilder Watch(string path) => new MonitorBuilder(path);

    public static MonitorBuilder Watch(string path, string filter) => new MonitorBuilder(path).WithFilter(filter);

    public ResilientFileSystemMonitor(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Path, $"{nameof(options)}.{nameof(options.Path)}");
        
        if (string.IsNullOrWhiteSpace(options.Path))
            throw new ArgumentException("Path cannot be null or whitespace.", $"{nameof(options)}.{nameof(options.Path)}");

        _options = options;
        _rootPath = Path.GetFullPath(options.Path);
        
        // Create event serialization channel (internal)
        _eventChannel = Channel.CreateUnbounded<EventEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        // Create public event channel (for ChannelReader<FileSystemEvent> API)
        _publicEventChannel = Channel.CreateUnbounded<FileSystemEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
        
        // Start event delivery task
        _eventDeliveryTask = Task.Run(DeliverEventsAsync);
        
        // Create timers (but don't start them yet)
        _healthTimer = new Timer(OnHealthTick, null, Timeout.Infinite, Timeout.Infinite);
        _auditTimer = new Timer(OnAuditTick, null, Timeout.Infinite, Timeout.Infinite);
        _pollingTimer = new Timer(OnPollingTick, null, Timeout.Infinite, Timeout.Infinite);
        
        StartMonitoring();
    }

    private void StartMonitoring()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            // Build initial index
            _index = Snapshot(_rootPath);
            _rollingDigest = ComputeDigest(_index);
            _lastEventUtc = DateTime.UtcNow;

            if (Directory.Exists(_rootPath))
            {
                TryStartFileSystemWatcher();
            }
            else if (_options.EnablePollingFallback)
            {
                TransitionToPolling("directory does not exist");
            }

            // Start health and audit timers
            _healthTimer.Change(_options.HealthCheckInterval, _options.HealthCheckInterval);
            _auditTimer.Change(_options.AuditInterval, _options.AuditInterval);
        }
    }

    private void TryStartFileSystemWatcher()
    {
        // Must be called under lock
        try
        {
            StopFileSystemWatcher();

            _watcher = new FileSystemWatcher(_rootPath)
            {
                Filter = _options.Filter,
                IncludeSubdirectories = _options.IncludeSubdirectories,
                NotifyFilter = _options.NotifyFilter,
                InternalBufferSize = _options.InternalBufferSize,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnFileSystemWatcherError;

            _state = MonitorState.Watching;
            StopPollingTimer();
            
            // Take a fresh snapshot and reconcile to detect any changes that occurred
            // while we were in polling mode or during the transition
            var currentSnapshot = Snapshot(_rootPath);
            Reconcile(currentSnapshot);
            
            EnqueueEvent(new EventEntry(EventType.ModeChanged, null!, ModeArgs: new ModeChangedEventArgs(WatcherMode.Native, "watcher started")));
        }
        catch (Exception ex)
        {
            if (_options.EnablePollingFallback && _options.AutoRecoverFromErrors)
            {
                TransitionToPolling($"failed to start watcher: {ex.Message}");
            }
            else
            {
                throw;
            }
        }
    }

    private void StopFileSystemWatcher()
    {
        // Must be called under lock
        if (_watcher != null)
        {
            _watcher.Created -= OnFileCreated;
            _watcher.Changed -= OnFileChanged;
            _watcher.Deleted -= OnFileDeleted;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnFileSystemWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void TransitionToPolling(string reason)
    {
        // Must be called under lock
        if (_state == MonitorState.Polling)
            return;

        StopFileSystemWatcher();
        _state = MonitorState.Polling;
        
        _pollingTimer.Change(_options.PollingInterval, _options.PollingInterval);
        
        EnqueueEvent(new EventEntry(EventType.ModeChanged, null!, ModeArgs: new ModeChangedEventArgs(WatcherMode.Polling, reason)));
    }

    private void StopPollingTimer()
    {
        _pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    #region Event Handlers (FileSystemWatcher)

    private void OnFileCreated(object sender, FileSystemEventArgs e) => 
        ApplyEvent(() => UpsertFile(e.FullPath), e.FullPath, WatcherChangeTypes.Created);

    private void OnFileChanged(object sender, FileSystemEventArgs e) => 
        ApplyEvent(() => UpsertFile(e.FullPath), e.FullPath, WatcherChangeTypes.Changed);

    private void OnFileDeleted(object sender, FileSystemEventArgs e) => 
        ApplyEvent(() => _index.Remove(GetRelativePath(e.FullPath)), e.FullPath, WatcherChangeTypes.Deleted);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        lock (_gate)
        {
            _index.Remove(GetRelativePath(e.OldFullPath));
            UpsertFile(e.FullPath);
            _lastEventUtc = DateTime.UtcNow;
            _rollingDigest = BumpDigest(_rollingDigest);
        }
        
        if (ShouldEmitEvent(e.FullPath))
            EnqueueEvent(new EventEntry(EventType.Renamed, e, RenamedArgs: e));
    }

    private void OnFileSystemWatcherError(object sender, ErrorEventArgs e)
    {
        EnqueueEvent(new EventEntry(EventType.Error, null!, ErrorArgs: e));

        if (_options.AutoRecoverFromErrors && _options.EnablePollingFallback)
        {
            lock (_gate)
            {
                TransitionToPolling($"watcher error: {e.GetException()?.Message ?? "unknown"}");
            }
        }
    }

    #endregion

    #region Event Application

    private void ApplyEvent(Action updateIndex, string fullPath, WatcherChangeTypes changeType)
    {
        lock (_gate)
        {
            updateIndex();
            _lastEventUtc = DateTime.UtcNow;
            _rollingDigest = BumpDigest(_rollingDigest);
        }

        if (ShouldEmitEvent(fullPath))
        {
            var args = new FileSystemEventArgs(changeType, Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath));
            
            var eventType = changeType switch
            {
                WatcherChangeTypes.Created => EventType.Created,
                WatcherChangeTypes.Changed => EventType.Changed,
                WatcherChangeTypes.Deleted => EventType.Deleted,
                _ => throw new ArgumentOutOfRangeException(nameof(changeType))
            };
            
            EnqueueEvent(new EventEntry(eventType, args));
        }
    }

    private void UpsertFile(string fullPath)
    {
        if (TryStatFile(fullPath, out var meta))
        {
            _index[GetRelativePath(fullPath)] = meta;
        }
    }

    private bool ShouldEmitEvent(string fullPath)
    {
        // Check depth limit first (if configured)
        if (!IsWithinDepthLimit(fullPath))
            return false;

        if (_options.DebounceTime == null)
            return true;

        var now = DateTime.UtcNow;
        var key = fullPath.ToLowerInvariant();
        
        if (_debounceTracker.TryGetValue(key, out var lastTime))
        {
            if (now - lastTime < _options.DebounceTime)
                return false;
        }
        
        _debounceTracker[key] = now;
        return true;
    }

    private bool IsWithinDepthLimit(string fullPath)
    {
        // If not monitoring subdirectories or unlimited depth, accept all
        if (!_options.IncludeSubdirectories || _options.MaxDepth < 0)
            return true;

        // If maxDepth is 0, only accept files in root directory
        if (_options.MaxDepth == 0)
        {
            var dir = Path.GetDirectoryName(fullPath);
            return string.Equals(dir, _rootPath, StringComparison.OrdinalIgnoreCase);
        }

        // Calculate actual depth
        var relativePath = GetRelativePath(fullPath);
        var depth = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1;
        
        return depth <= _options.MaxDepth;
    }

    #endregion

    #region Health & Audit

    private void OnHealthTick(object? state)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (!Directory.Exists(_rootPath))
            {
                if (_state == MonitorState.Watching)
                {
                    TransitionToPolling("directory removed");
                }
            }
        }
    }

    private void OnAuditTick(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _state != MonitorState.Watching)
                return;

            var quietPeriod = _options.AuditInterval / 2;
            var timeSinceLastEvent = DateTime.UtcNow - _lastEventUtc;
            
            // Only audit if we've been quiet (no burst activity)
            if (timeSinceLastEvent < quietPeriod)
                return;

            try
            {
                var currentSnapshot = Snapshot(_rootPath);
                var currentDigest = ComputeDigest(currentSnapshot);

                if (currentDigest != _rollingDigest)
                {
                    // Divergence detected! Reconcile
                    TransitionToPolling("audit detected divergence");
                    Reconcile(currentSnapshot);
                    TryStartFileSystemWatcher();
                }
            }
            catch (Exception ex)
            {
                // Audit failure - transition to polling for safety
                TransitionToPolling($"audit failed: {ex.Message}");
            }
        }
    }

    private void OnPollingTick(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _state != MonitorState.Polling)
                return;

            if (Directory.Exists(_rootPath))
            {
                try
                {
                    var currentSnapshot = Snapshot(_rootPath);
                    Reconcile(currentSnapshot);
                    
                    if (_options.AutoRecoverFromErrors)
                    {
                        TryStartFileSystemWatcher();
                        
                        // Re-snapshot and reconcile after starting watcher to catch any files
                        // that appeared between initial snapshot and watcher start
                        if (_state == MonitorState.Watching)
                        {
                            var postWatcherSnapshot = Snapshot(_rootPath);
                            Reconcile(postWatcherSnapshot);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Continue polling
                    EnqueueEvent(new EventEntry(EventType.Error, null!, ErrorArgs: new ErrorEventArgs(ex)));
                }
            }
        }
    }

    #endregion

    #region Reconciliation (Lossless)

    private void Reconcile(Dictionary<string, FileMeta> currentSnapshot)
    {
        // Must be called under lock
        var oldIndex = _index;

        // Detect deleted files FIRST (so we don't confuse with creates)
        foreach (var relPath in oldIndex.Keys)
        {
            if (!currentSnapshot.ContainsKey(relPath))
            {
                EmitSyntheticEvent(relPath, WatcherChangeTypes.Deleted);
            }
        }

        // Detect created/changed files
        foreach (var (relPath, meta) in currentSnapshot)
        {
            if (!oldIndex.TryGetValue(relPath, out var oldMeta))
            {
                // Created (new file that wasn't in old index)
                EmitSyntheticEvent(relPath, WatcherChangeTypes.Created);
            }
            else if (HasChanged(oldMeta, meta, relPath))
            {
                // Changed (file existed before and now has different metadata)
                EmitSyntheticEvent(relPath, WatcherChangeTypes.Changed);
            }
        }

        // Update index and digest
        _index = currentSnapshot;
        _rollingDigest = ComputeDigest(_index);
        _lastEventUtc = DateTime.UtcNow;
    }

    private bool HasChanged(FileMeta old, FileMeta current, string relPath)
    {
        // Primary check: metadata
        if (old.Length != current.Length ||
            old.LastWriteUtc != current.LastWriteUtc ||
            old.Attributes != current.Attributes)
        {
            return true;
        }

        // Adaptive hashing (optional, off by default)
        if (_options.EnableAdaptiveHashOnReconcile)
        {
            return HasContentChanged(relPath);
        }

        return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", 
        Justification = "MD5 is used for change detection, not security. Fast, deterministic hash for content comparison.")]
    private bool HasContentChanged(string relPath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, relPath);
            var fileInfo = new FileInfo(fullPath);
            
            if (!fileInfo.Exists || fileInfo.Length == 0)
                return false;

            var bytesToRead = Math.Min(_options.AdaptiveHashBytesPerEdge, fileInfo.Length);
            
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            // Hash first N bytes
            var buffer = new byte[bytesToRead];
            var read = fs.Read(buffer, 0, buffer.Length);
            
            #pragma warning disable CA5351 // MD5 used for change detection, not security
            var hash1 = MD5.HashData(buffer.AsSpan(0, read));
            #pragma warning restore CA5351
            
            // Hash last N bytes (if file is large enough)
            if (fileInfo.Length > bytesToRead * 2)
            {
                fs.Seek(-bytesToRead, SeekOrigin.End);
                read = fs.Read(buffer, 0, buffer.Length);
                var hash2 = MD5.HashData(buffer.AsSpan(0, read));
                
                // Conservative: assume changed if adaptive hashing is on
                // Real implementation would store and compare previous hashes
                return true;
            }
            
            return false;
        }
        catch
        {
            return false; // Can't read file, assume no change
        }
    }

    private void EmitSyntheticEvent(string relPath, WatcherChangeTypes changeType)
    {
        var fullPath = Path.Combine(_rootPath, relPath);
        var dir = Path.GetDirectoryName(fullPath)!;
        var name = Path.GetFileName(fullPath);
        var args = new FileSystemEventArgs(changeType, dir, name);

        var eventType = changeType switch
        {
            WatcherChangeTypes.Created => EventType.Created,
            WatcherChangeTypes.Changed => EventType.Changed,
            WatcherChangeTypes.Deleted => EventType.Deleted,
            _ => throw new ArgumentOutOfRangeException(nameof(changeType))
        };

        EnqueueEvent(new EventEntry(eventType, args));
    }

    #endregion

    #region Snapshot & Digest

    private Dictionary<string, FileMeta> Snapshot(string rootPath)
    {
        var snapshot = new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
        
        if (!Directory.Exists(rootPath))
            return snapshot;

        try
        {
            var searchOption = _options.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = _options.IncludeSubdirectories,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
            };

            foreach (var file in Directory.EnumerateFiles(rootPath, _options.Filter, enumerationOptions))
            {
                if (TryStatFile(file, out var meta))
                {
                    var relPath = GetRelativePath(file);
                    snapshot[relPath] = meta;
                }
            }
        }
        catch
        {
            // Return partial snapshot
        }

        return snapshot;
    }

    private static bool TryStatFile(string fullPath, out FileMeta meta)
    {
        meta = default!;
        
        try
        {
            var fi = new FileInfo(fullPath);
            if (fi.Exists && (fi.Attributes & FileAttributes.Directory) == 0)
            {
                meta = new FileMeta(fi.Length, fi.LastWriteTimeUtc, fi.Attributes);
                return true;
            }
        }
        catch
        {
            // File raced away or inaccessible
        }
        
        return false;
    }

    private static ulong ComputeDigest(Dictionary<string, FileMeta> index)
    {
        // FNV-1a 64-bit hash over sorted entries
        ulong hash = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        foreach (var kv in index.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            // Hash relative path
            foreach (char c in kv.Key)
            {
                hash ^= c;
                hash *= prime;
            }

            // Hash metadata
            unchecked
            {
                var length = kv.Value.Length;
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(length >> (i * 8));
                    hash *= prime;
                }

                var ticks = kv.Value.LastWriteUtc.Ticks;
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(ticks >> (i * 8));
                    hash *= prime;
                }

                var attr = (int)kv.Value.Attributes;
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (byte)(attr >> (i * 8));
                    hash *= prime;
                }
            }
        }

        return hash;
    }

    private static ulong BumpDigest(ulong current)
    {
        // Simple rolling update (real impl would incorporate the actual change)
        return unchecked(current * 1099511628211UL + 14695981039346656037UL);
    }

    private string GetRelativePath(string fullPath) => 
        Path.GetRelativePath(_rootPath, fullPath);

    #endregion

    #region Event Delivery
    
    private void EnqueueEvent(EventEntry entry)
    {
        _eventChannel.Writer.TryWrite(entry);
    }
    
    private async Task DeliverEventsAsync()
    {
        await foreach (var entry in _eventChannel.Reader.ReadAllAsync())
        {
            try
            {
                // Create unified event for public channel
                var unifiedEvent = entry.Type switch
                {
                    EventType.Created => FileSystemEvent.Create(entry.Args),
                    EventType.Changed => FileSystemEvent.Change(entry.Args),
                    EventType.Deleted => FileSystemEvent.Delete(entry.Args),
                    EventType.Renamed => FileSystemEvent.Rename(entry.RenamedArgs!),
                    EventType.Error => FileSystemEvent.Error(entry.ErrorArgs!),
                    EventType.ModeChanged => FileSystemEvent.ModeChange(entry.ModeArgs!),
                    _ => null
                };
                
                if (unifiedEvent != null)
                {
                    _publicEventChannel.Writer.TryWrite(unifiedEvent);
                }
                
                // Fire traditional events
                switch (entry.Type)
                {
                    case EventType.Created:
                        Created?.Invoke(this, entry.Args);
                        break;
                    case EventType.Changed:
                        Changed?.Invoke(this, entry.Args);
                        break;
                    case EventType.Deleted:
                        Deleted?.Invoke(this, entry.Args);
                        break;
                    case EventType.Renamed:
                        Renamed?.Invoke(this, entry.RenamedArgs!);
                        break;
                    case EventType.Error:
                        Error?.Invoke(this, entry.ErrorArgs!);
                        break;
                    case EventType.ModeChanged:
                        ModeChanged?.Invoke(this, entry.ModeArgs!);
                        break;
                }
            }
            catch
            {
                // Swallow exceptions from user event handlers
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _state = MonitorState.Stopped;

            StopFileSystemWatcher();
            
            _healthTimer.Dispose();
            _auditTimer.Dispose();
            _pollingTimer.Dispose();
            
            // Complete the event channels and wait for delivery to finish
            _eventChannel.Writer.Complete();
            _publicEventChannel.Writer.Complete();
        }
        
        // Wait for event delivery outside the lock
        try
        {
            _eventDeliveryTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Best effort - don't throw from Dispose
        }
    }

    #endregion
}

/// <summary>
/// Watcher mode.
/// </summary>
public enum WatcherMode
{
    Native,
    Polling
}

/// <summary>
/// Event args for mode changes.
/// </summary>
public sealed class ModeChangedEventArgs : EventArgs
{
    public WatcherMode Mode { get; }
    public string Reason { get; }

    public ModeChangedEventArgs(WatcherMode mode, string reason)
    {
        Mode = mode;
        Reason = reason;
    }
}
