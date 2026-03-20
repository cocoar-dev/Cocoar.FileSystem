# Events & Channels

`ResilientFileSystemMonitor` offers two consumption models: traditional .NET events and a unified `ChannelReader<FileSystemEvent>` stream.

## Traditional Events

Subscribe to individual event types:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .OnCreated((s, e) => HandleCreated(e))
    .OnChanged((s, e) => HandleChanged(e))
    .OnDeleted((s, e) => HandleDeleted(e))
    .OnRenamed((s, e) => HandleRenamed(e))
    .OnError((s, e) => HandleError(e))
    .OnModeChanged((s, e) => HandleModeChange(e))
    .Build();
```

Or subscribe after building:

```csharp
var monitor = ResilientFileSystemMonitor.Watch(@"C:\data").Build();
monitor.Created += (s, e) => Console.WriteLine($"Created: {e.Name}");
monitor.Changed += (s, e) => Console.WriteLine($"Changed: {e.Name}");
```

## ChannelReader Stream

The `Events` property exposes a `ChannelReader<FileSystemEvent>` with all events unified:

```csharp
var monitor = ResilientFileSystemMonitor.Watch(@"C:\data").Build();

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    Console.WriteLine($"{evt.Kind}: {evt.FullPath}");
}
```

### FileSystemEvent Model

All events are wrapped in a unified `FileSystemEvent`:

| Property | Type | Description |
|----------|------|-------------|
| `Kind` | `FileSystemEventKind` | Created, Changed, Deleted, Renamed, Error, ModeChanged |
| `FullPath` | `string` | Full path of the affected file |
| `Name` | `string` | File name only |
| `ChangeType` | `WatcherChangeTypes` | Original change type |
| `OldFullPath` | `string?` | Previous path (Renamed only) |
| `OldName` | `string?` | Previous name (Renamed only) |
| `Exception` | `Exception?` | Error details (Error only) |
| `Mode` | `WatcherMode?` | New mode (ModeChanged only) |
| `Reason` | `string?` | Reason for mode change (ModeChanged only) |

### Switch Pattern

```csharp
await foreach (var evt in monitor.Events.ReadAllAsync())
{
    switch (evt.Kind)
    {
        case FileSystemEventKind.Created:
            LoadFile(evt.FullPath);
            break;
        case FileSystemEventKind.Changed:
            ReloadFile(evt.FullPath);
            break;
        case FileSystemEventKind.Deleted:
            UnloadFile(evt.FullPath);
            break;
        case FileSystemEventKind.Renamed:
            RenameFile(evt.OldFullPath!, evt.FullPath);
            break;
        case FileSystemEventKind.Error:
            _logger.LogWarning(evt.Exception, "Monitor error");
            break;
        case FileSystemEventKind.ModeChanged:
            _logger.LogInformation("Monitor: {Mode} - {Reason}", evt.Mode, evt.Reason);
            break;
    }
}
```

## Why Use the Channel?

| Feature | Traditional Events | ChannelReader |
|---------|-------------------|---------------|
| Unified stream | Separate handlers | Single stream |
| Ordering | Per event type | All events ordered |
| Async iteration | Manual | `await foreach` |
| Reactive operators | Difficult | Easy with Rx.NET |
| Concurrency control | One at a time per type | One at a time, all types |

Both APIs can be used simultaneously. The channel receives all events regardless of whether traditional event handlers are registered.

## ModeChanged Event

The `ModeChanged` event is specific to Cocoar.FileSystem and fires when the monitor switches between native and polling mode:

```csharp
.OnModeChanged((s, e) =>
{
    if (e.Mode == WatcherMode.Polling)
        _logger.LogWarning("Monitor fell back to polling: {Reason}", e.Reason);
    else
        _logger.LogInformation("Monitor recovered to native watcher");
})
```
