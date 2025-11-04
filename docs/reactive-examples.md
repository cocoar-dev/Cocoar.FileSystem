# Reactive Examples for ResilientFileSystemMonitor

This document shows how to use the `Events` channel with reactive patterns.

## Using without Rx.NET

The `Events` property exposes a `ChannelReader<FileSystemEvent>` which can be consumed directly:

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data"
});

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    Console.WriteLine($"{evt.Kind}: {evt.FullPath}");
}
```

## Manual Throttling with LINQ

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data"
});

var lastProcessed = new Dictionary<string, DateTime>();
var throttleWindow = TimeSpan.FromMilliseconds(500);

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    if (evt.Kind == FileSystemEventKind.Changed)
    {
        var now = DateTime.UtcNow;
        if (!lastProcessed.TryGetValue(evt.FullPath, out var last) || 
            now - last > throttleWindow)
        {
            lastProcessed[evt.FullPath] = now;
            Console.WriteLine($"Processing: {evt.FullPath}");
            await ProcessFileAsync(evt.FullPath);
        }
    }
}
```

## Using with System.Reactive (Rx.NET)

If you want advanced reactive operators like `Throttle`, `Buffer`, `DistinctUntilChanged`, etc., you can convert the channel to an Observable:

```bash
dotnet add package System.Reactive
```

```csharp
using System.Reactive.Linq;
using System.Threading.Channels;

public static class ChannelExtensions
{
    public static IObservable<T> AsObservable<T>(this ChannelReader<T> reader)
    {
        return Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await foreach (var item in reader.ReadAllAsync(ct))
                {
                    observer.OnNext(item);
                }
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }
}
```

### Example: Throttle Changes

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data"
});

var subscription = monitor.Events
    .AsObservable()
    .Where(e => e.Kind == FileSystemEventKind.Changed)
    .Throttle(TimeSpan.FromMilliseconds(500))
    .Subscribe(evt =>
    {
        Console.WriteLine($"Throttled change: {evt.FullPath}");
    });

// Later: subscription.Dispose();
```

### Example: Buffer Events

```csharp
var subscription = monitor.Events
    .AsObservable()
    .Buffer(TimeSpan.FromSeconds(5))
    .Where(batch => batch.Any())
    .Subscribe(batch =>
    {
        Console.WriteLine($"Processing batch of {batch.Count} events");
        foreach (var evt in batch)
        {
            Console.WriteLine($"  - {evt.Kind}: {evt.FullPath}");
        }
    });
```

### Example: Distinct Changes per File

```csharp
var subscription = monitor.Events
    .AsObservable()
    .Where(e => e.Kind == FileSystemEventKind.Changed)
    .GroupBy(e => e.FullPath)
    .SelectMany(group => group.Throttle(TimeSpan.FromMilliseconds(500)))
    .Subscribe(evt =>
    {
        Console.WriteLine($"Distinct change: {evt.FullPath}");
    });
```

## Why Use the Events Channel?

### Traditional approach (separate events):
```csharp
monitor.Created += (s, e) => HandleEvent("Created", e.FullPath);
monitor.Changed += (s, e) => HandleEvent("Changed", e.FullPath);
monitor.Deleted += (s, e) => HandleEvent("Deleted", e.FullPath);
monitor.Renamed += (s, e) => HandleEvent("Renamed", e.FullPath);
monitor.Error += (s, e) => HandleEvent("Error", "");
```

**Problems:**
- ❌ Events can fire concurrently if multiple handlers are registered
- ❌ No guaranteed ordering across different event types
- ❌ Difficult to apply reactive operators (throttle, buffer, etc.)
- ❌ Each event type needs separate handler registration

### New approach (unified stream):
```csharp
await foreach (var evt in monitor.Events.ReadAllAsync())
{
    switch (evt.Kind)
    {
        case FileSystemEventKind.Created:
        case FileSystemEventKind.Changed:
        case FileSystemEventKind.Deleted:
        case FileSystemEventKind.Renamed:
            HandleEvent(evt.Kind.ToString(), evt.FullPath);
            break;
        case FileSystemEventKind.Error:
            HandleError(evt.Exception);
            break;
    }
}
```

**Benefits:**
- ✅ All events in one ordered stream
- ✅ Sequential delivery guaranteed
- ✅ Easy to apply LINQ, async iteration, or Rx operators
- ✅ Single subscription point
- ✅ Perfect for reactive patterns without adding Rx.NET dependency

## Performance Note

The `Events` channel uses an unbounded channel internally, so there's no performance penalty. Events are queued in memory and delivered as fast as your consumer can process them. The traditional event handlers still work exactly as before - both APIs can be used simultaneously if needed.
