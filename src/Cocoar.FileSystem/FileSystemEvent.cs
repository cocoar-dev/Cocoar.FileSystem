namespace Cocoar.FileSystem;

/// <summary>
/// Unified event type for all file system changes.
/// Allows consuming all events from a single stream.
/// </summary>
public sealed class FileSystemEvent
{
    /// <summary>
    /// Type of file system event.
    /// </summary>
    public FileSystemEventKind Kind { get; }
    
    /// <summary>
    /// Full path to the file or directory.
    /// </summary>
    public string FullPath { get; }
    
    /// <summary>
    /// Name of the file or directory.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Change type (Created, Deleted, Changed, Renamed, All).
    /// </summary>
    public WatcherChangeTypes ChangeType { get; }
    
    /// <summary>
    /// Old full path (only for Renamed events).
    /// </summary>
    public string? OldFullPath { get; }
    
    /// <summary>
    /// Old name (only for Renamed events).
    /// </summary>
    public string? OldName { get; }
    
    /// <summary>
    /// Exception (only for Error events).
    /// </summary>
    public Exception? Exception { get; }
    
    /// <summary>
    /// Watcher mode (only for ModeChanged events).
    /// </summary>
    public WatcherMode? Mode { get; }
    
    /// <summary>
    /// Reason for mode change (only for ModeChanged events).
    /// </summary>
    public string? Reason { get; }
    
    private FileSystemEvent(
        FileSystemEventKind kind,
        string fullPath,
        string name,
        WatcherChangeTypes changeType,
        string? oldFullPath = null,
        string? oldName = null,
        Exception? exception = null,
        WatcherMode? mode = null,
        string? reason = null)
    {
        Kind = kind;
        FullPath = fullPath;
        Name = name;
        ChangeType = changeType;
        OldFullPath = oldFullPath;
        OldName = oldName;
        Exception = exception;
        Mode = mode;
        Reason = reason;
    }
    
    internal static FileSystemEvent Create(FileSystemEventArgs args) =>
        new(FileSystemEventKind.Created, args.FullPath, args.Name ?? "", args.ChangeType);
    
    internal static FileSystemEvent Change(FileSystemEventArgs args) =>
        new(FileSystemEventKind.Changed, args.FullPath, args.Name ?? "", args.ChangeType);
    
    internal static FileSystemEvent Delete(FileSystemEventArgs args) =>
        new(FileSystemEventKind.Deleted, args.FullPath, args.Name ?? "", args.ChangeType);
    
    internal static FileSystemEvent Rename(RenamedEventArgs args) =>
        new(FileSystemEventKind.Renamed, args.FullPath, args.Name ?? "", args.ChangeType, 
            args.OldFullPath, args.OldName);
    
    internal static FileSystemEvent Error(ErrorEventArgs args) =>
        new(FileSystemEventKind.Error, "", "", WatcherChangeTypes.All, exception: args.GetException());
    
    internal static FileSystemEvent ModeChange(ModeChangedEventArgs args) =>
        new(FileSystemEventKind.ModeChanged, "", "", WatcherChangeTypes.All, mode: args.Mode, reason: args.Reason);
}

/// <summary>
/// Kind of file system event.
/// </summary>
public enum FileSystemEventKind
{
    /// <summary>
    /// File or directory was created.
    /// </summary>
    Created,
    
    /// <summary>
    /// File or directory was changed.
    /// </summary>
    Changed,
    
    /// <summary>
    /// File or directory was deleted.
    /// </summary>
    Deleted,
    
    /// <summary>
    /// File or directory was renamed.
    /// </summary>
    Renamed,
    
    /// <summary>
    /// An error occurred.
    /// </summary>
    Error,
    
    /// <summary>
    /// Monitoring mode changed (watcher vs polling).
    /// </summary>
    ModeChanged
}
