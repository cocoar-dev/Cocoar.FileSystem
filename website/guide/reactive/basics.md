# Channel-Based Consumption

The `Events` property on `ResilientFileSystemMonitor` exposes a `ChannelReader<FileSystemEvent>` — a unified, ordered stream of all events.

## Basic Usage

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .Build();

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    Console.WriteLine($"{evt.Kind}: {evt.FullPath}");
}
```

## Why Channels?

Compared to traditional .NET events:

- **Unified**: All event types in one stream (Created, Changed, Deleted, Renamed, Error, ModeChanged)
- **Ordered**: Strict FIFO across all event types
- **Sequential**: No parallel delivery — one event at a time
- **Async-native**: Works with `await foreach`
- **Zero dependencies**: Uses `System.Threading.Channels` from .NET BCL

## Switch Pattern

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
            Rename(evt.OldFullPath!, evt.FullPath);
            break;
        case FileSystemEventKind.Error:
            _logger.LogWarning(evt.Exception, "Monitor error");
            break;
        case FileSystemEventKind.ModeChanged:
            _logger.LogInformation("Mode: {Mode}", evt.Mode);
            break;
    }
}
```

## Manual Throttling

Without Rx.NET, you can implement per-file throttling:

```csharp
var lastProcessed = new Dictionary<string, DateTime>();
var window = TimeSpan.FromMilliseconds(500);

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    if (evt.Kind == FileSystemEventKind.Changed)
    {
        var now = DateTime.UtcNow;
        if (!lastProcessed.TryGetValue(evt.FullPath, out var last)
            || now - last > window)
        {
            lastProcessed[evt.FullPath] = now;
            await ProcessAsync(evt.FullPath);
        }
    }
}
```

## Using Both APIs

Traditional events and the channel work simultaneously:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .OnCreated((s, e) => _logger.LogDebug("Created: {Name}", e.Name))
    .Build();

// Also consume via channel
await foreach (var evt in monitor.Events.ReadAllAsync())
{
    // Both this AND the OnCreated handler fire
}
```

## Performance

The channel uses an **unbounded** internal buffer. Events queue in memory and deliver as fast as your consumer processes them. No events are dropped.

::: tip
Keep your event processing fast. If you need to do slow work (HTTP calls, database writes), offload it to a separate `Task.Run()` or another channel.
:::
