namespace Cocoar.FileSystem;

/// <summary>
/// Fluent builder for creating and configuring a ResilientFileSystemMonitor.
/// </summary>
public sealed class MonitorBuilder
{
    private string? _path;
    private string _filter = "*";
    private List<string>? _filters;
    private bool _enablePollingFallback = true;
    private bool _autoRecoverFromErrors = true;
    private int _maxDepth; // 0 = no subdirectories, -1 = unlimited
    private TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _auditInterval = TimeSpan.FromSeconds(60);
    private TimeSpan? _debounceTime;
    private NotifyFilters _notifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
    private int _internalBufferSize = 64 * 1024;
    private bool _enableAdaptiveHashOnReconcile;
    private int _adaptiveHashBytesPerEdge = 64 * 1024;
    
    private EventHandler<FileSystemEventArgs>? _created;
    private EventHandler<FileSystemEventArgs>? _changed;
    private EventHandler<FileSystemEventArgs>? _deleted;
    private EventHandler<RenamedEventArgs>? _renamed;
    private EventHandler<ErrorEventArgs>? _error;
    private EventHandler<ModeChangedEventArgs>? _modeChanged;

    internal MonitorBuilder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        
        _path = path;
    }

    /// <summary>
    /// Adds file patterns to monitor. Can be called multiple times to add more patterns.
    /// Patterns are matched against filenames only (not full paths).
    /// Supports DOS-style wildcards: * (any characters) and ? (single character).
    /// </summary>
    /// <param name="patterns">One or more file patterns (e.g., "*.txt", "test-*.log")</param>
    /// <example>
    /// .WithFilter("*.pfx", "*.p12", "*.cer")
    /// .WithFilter("*.json")  // Adds to existing patterns
    /// </example>
    public MonitorBuilder WithFilter(params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Length == 0)
            throw new ArgumentException("At least one pattern must be specified.", nameof(patterns));
        if (patterns.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Patterns cannot be null or whitespace.", nameof(patterns));
        
        _filters ??= new List<string>();
        _filters.AddRange(patterns);
        _filter = "*"; // Set wildcard for FileSystemWatcher, actual filtering in ShouldEmitEvent
        return this;
    }

    /// <summary>
    /// Clears all previously configured file patterns.
    /// </summary>
    public MonitorBuilder ClearFilters()
    {
        _filters?.Clear();
        _filter = "*";
        return this;
    }

    /// <summary>
    /// Enables automatic fallback to polling mode when the FileSystemWatcher fails or directory is deleted.
    /// Default is enabled with 5-second polling interval.
    /// </summary>
    /// <param name="pollingInterval">The polling interval (milliseconds).</param>
    public MonitorBuilder WithPollingFallback(int pollingInterval = 5000)
    {
        if (pollingInterval <= 0)
            throw new ArgumentException("Polling interval must be positive.", nameof(pollingInterval));
        
        _enablePollingFallback = true;
        _pollingInterval = TimeSpan.FromMilliseconds(pollingInterval);
        return this;
    }

    /// <summary>
    /// Enables automatic fallback to polling mode when the FileSystemWatcher fails or directory is deleted.
    /// Default is enabled with 5-second polling interval.
    /// </summary>
    /// <param name="pollingInterval">The polling interval.</param>
    public MonitorBuilder WithPollingFallback(TimeSpan pollingInterval)
    {
        if (pollingInterval <= TimeSpan.Zero)
            throw new ArgumentException("Polling interval must be positive.", nameof(pollingInterval));
        
        _enablePollingFallback = true;
        _pollingInterval = pollingInterval;
        return this;
    }

    /// <summary>
    /// Disables automatic fallback to polling mode. 
    /// The monitor will stop delivering events if the FileSystemWatcher fails.
    /// </summary>
    public MonitorBuilder WithoutPollingFallback()
    {
        _enablePollingFallback = false;
        return this;
    }

    /// <summary>
    /// Multiple changes to the same file within this window will only fire one event.
    /// </summary>
    public MonitorBuilder WithDebounce(int milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentException("Debounce time cannot be negative.", nameof(milliseconds));
        
        _debounceTime = milliseconds == 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Multiple changes to the same file within this window will only fire one event.
    /// </summary>
    public MonitorBuilder WithDebounce(TimeSpan debounceTime)
    {
        if (debounceTime < TimeSpan.Zero)
            throw new ArgumentException("Debounce time cannot be negative.", nameof(debounceTime));
        
        _debounceTime = debounceTime == TimeSpan.Zero ? null : debounceTime;
        return this;
    }

    /// <summary>
    /// The monitor will raise an error event but not attempt to restart.
    /// </summary>
    public MonitorBuilder WithoutAutoRecovery()
    {
        _autoRecoverFromErrors = false;
        return this;
    }

    /// <summary>
    /// True monitors all subdirectories (unlimited depth), false monitors only the root directory.
    /// </summary>
    public MonitorBuilder IncludeSubdirectories(bool include = true)
    {
        _maxDepth = include ? -1 : 0; // unlimited if true, none if false
        return this;
    }

    /// <summary>
    /// 0 = root only, 1 = direct children, 2+ = specific depth, -1 = unlimited.
    /// </summary>
    public MonitorBuilder IncludeSubdirectories(int maxDepth)
    {
        if (maxDepth < -1)
            throw new ArgumentException("Max depth must be -1 (unlimited) or greater than or equal to 0.", nameof(maxDepth));
        
        _maxDepth = maxDepth;
        return this;
    }

    public MonitorBuilder WithNotifyFilters(NotifyFilters filters)
    {
        _notifyFilter = filters;
        return this;
    }

    /// <summary>
    /// Sets the internal buffer size for the FileSystemWatcher. Default is 64KB.
    /// Increase for high-change-rate scenarios to avoid buffer overflow.
    /// </summary>
    /// <param name="sizeInBytes">The buffer size in bytes.</param>
    public MonitorBuilder WithInternalBufferSize(int sizeInBytes)
    {
        if (sizeInBytes <= 0)
            throw new ArgumentException("Buffer size must be positive.", nameof(sizeInBytes));
        
        _internalBufferSize = sizeInBytes;
        return this;
    }

    /// <summary>
    /// Sets the health check interval to detect directory removal. Default is 1 second.
    /// This is a lightweight operation (just Directory.Exists).
    /// </summary>
    /// <param name="interval">The health check interval.</param>
    public MonitorBuilder WithHealthCheckInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Health check interval must be positive.", nameof(interval));
        
        _healthCheckInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the audit interval to detect silent event loss via metadata fingerprinting. Default is 60 seconds.
    /// This is an O(files) operation but detects missed events reliably.
    /// </summary>
    /// <param name="interval">The audit interval.</param>
    public MonitorBuilder WithAuditInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Audit interval must be positive.", nameof(interval));
        
        _auditInterval = interval;
        return this;
    }

    /// <summary>
    /// Enables adaptive content hashing during reconciliation for stronger change detection.
    /// When enabled, computes partial content hash for files with identical metadata.
    /// </summary>
    /// <param name="bytesPerEdge">Number of bytes to hash from start/end of file. Default is 64KB.</param>
    public MonitorBuilder WithAdaptiveHashing(int bytesPerEdge = 64 * 1024)
    {
        if (bytesPerEdge <= 0)
            throw new ArgumentException("Bytes per edge must be positive.", nameof(bytesPerEdge));
        
        _enableAdaptiveHashOnReconcile = true;
        _adaptiveHashBytesPerEdge = bytesPerEdge;
        return this;
    }

    public MonitorBuilder OnCreated(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _created += handler;
        return this;
    }

    public MonitorBuilder OnChanged(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _changed += handler;
        return this;
    }

    public MonitorBuilder OnDeleted(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _deleted += handler;
        return this;
    }

    public MonitorBuilder OnRenamed(EventHandler<RenamedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _renamed += handler;
        return this;
    }

    public MonitorBuilder OnError(EventHandler<ErrorEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _error += handler;
        return this;
    }

    public MonitorBuilder OnModeChanged(EventHandler<ModeChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _modeChanged += handler;
        return this;
    }

    public ResilientFileSystemMonitor Build()
    {
        if (string.IsNullOrWhiteSpace(_path))
            throw new InvalidOperationException("Path must be set before building.");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = _path,
            Filter = _filter,
            Filters = _filters?.ToArray(),
            EnablePollingFallback = _enablePollingFallback,
            AutoRecoverFromErrors = _autoRecoverFromErrors,
            IncludeSubdirectories = _maxDepth != 0,
            MaxDepth = _maxDepth,
            PollingInterval = _pollingInterval,
            HealthCheckInterval = _healthCheckInterval,
            AuditInterval = _auditInterval,
            DebounceTime = _debounceTime,
            NotifyFilter = _notifyFilter,
            InternalBufferSize = _internalBufferSize,
            EnableAdaptiveHashOnReconcile = _enableAdaptiveHashOnReconcile,
            AdaptiveHashBytesPerEdge = _adaptiveHashBytesPerEdge
        };

        var monitor = new ResilientFileSystemMonitor(options);
        
        if (_created != null) monitor.Created += _created;
        if (_changed != null) monitor.Changed += _changed;
        if (_deleted != null) monitor.Deleted += _deleted;
        if (_renamed != null) monitor.Renamed += _renamed;
        if (_error != null) monitor.Error += _error;
        if (_modeChanged != null) monitor.ModeChanged += _modeChanged;

        return monitor;
    }
}
