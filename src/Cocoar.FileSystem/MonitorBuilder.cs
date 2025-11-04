namespace Cocoar.FileSystem;

/// <summary>
/// Fluent builder for creating and configuring a ResilientFileSystemMonitor.
/// </summary>
public sealed class MonitorBuilder
{
    private string? _path;
    private string _filter = "*";
    private bool _enablePollingFallback = true;
    private bool _autoRecoverFromErrors = true;
    private bool _includeSubdirectories = true;
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
    /// Sets the file filter pattern. Default is "*" (all files).
    /// </summary>
    /// <param name="filter">The filter pattern (e.g., "*.json", "*.txt").</param>
    public MonitorBuilder WithFilter(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (string.IsNullOrWhiteSpace(filter))
            throw new ArgumentException("Filter cannot be empty or whitespace.", nameof(filter));
        
        _filter = filter;
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
    /// Sets the debounce time to reduce noise from rapid file changes.
    /// Multiple changes to the same file within this window will only fire one event.
    /// </summary>
    /// <param name="milliseconds">The debounce time in milliseconds.</param>
    public MonitorBuilder WithDebounce(int milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentException("Debounce time cannot be negative.", nameof(milliseconds));
        
        _debounceTime = milliseconds == 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the debounce time to reduce noise from rapid file changes.
    /// Multiple changes to the same file within this window will only fire one event.
    /// </summary>
    /// <param name="debounceTime">The debounce time.</param>
    public MonitorBuilder WithDebounce(TimeSpan debounceTime)
    {
        if (debounceTime < TimeSpan.Zero)
            throw new ArgumentException("Debounce time cannot be negative.", nameof(debounceTime));
        
        _debounceTime = debounceTime == TimeSpan.Zero ? null : debounceTime;
        return this;
    }

    /// <summary>
    /// Disables automatic recovery from errors. The monitor will raise an error event but not attempt to restart.
    /// </summary>
    public MonitorBuilder WithoutAutoRecovery()
    {
        _autoRecoverFromErrors = false;
        return this;
    }

    /// <summary>
    /// Configures whether to monitor subdirectories. Default is true.
    /// </summary>
    /// <param name="include">True to monitor subdirectories, false to monitor only the root directory.</param>
    public MonitorBuilder IncludeSubdirectories(bool include = true)
    {
        _includeSubdirectories = include;
        return this;
    }

    /// <summary>
    /// Sets the notify filters to watch. Default is FileName | LastWrite | Size.
    /// </summary>
    /// <param name="filters">The notify filters.</param>
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

    /// <summary>
    /// Registers a handler for file creation events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnCreated(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _created += handler;
        return this;
    }

    /// <summary>
    /// Registers a handler for file change events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnChanged(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _changed += handler;
        return this;
    }

    /// <summary>
    /// Registers a handler for file deletion events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnDeleted(EventHandler<FileSystemEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _deleted += handler;
        return this;
    }

    /// <summary>
    /// Registers a handler for file rename events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnRenamed(EventHandler<RenamedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _renamed += handler;
        return this;
    }

    /// <summary>
    /// Registers a handler for error events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnError(EventHandler<ErrorEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _error += handler;
        return this;
    }

    /// <summary>
    /// Registers a handler for monitoring mode change events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    public MonitorBuilder OnModeChanged(EventHandler<ModeChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _modeChanged += handler;
        return this;
    }

    /// <summary>
    /// Builds and starts the configured ResilientFileSystemMonitor.
    /// </summary>
    /// <returns>A configured and started monitor instance.</returns>
    public ResilientFileSystemMonitor Build()
    {
        if (string.IsNullOrWhiteSpace(_path))
            throw new InvalidOperationException("Path must be set before building.");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = _path,
            Filter = _filter,
            EnablePollingFallback = _enablePollingFallback,
            AutoRecoverFromErrors = _autoRecoverFromErrors,
            IncludeSubdirectories = _includeSubdirectories,
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
        
        // Attach event handlers
        if (_created != null) monitor.Created += _created;
        if (_changed != null) monitor.Changed += _changed;
        if (_deleted != null) monitor.Deleted += _deleted;
        if (_renamed != null) monitor.Renamed += _renamed;
        if (_error != null) monitor.Error += _error;
        if (_modeChanged != null) monitor.ModeChanged += _modeChanged;

        return monitor;
    }
}
