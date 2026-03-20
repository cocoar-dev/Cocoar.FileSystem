# Rx.NET Integration

For advanced reactive operators like `Throttle`, `Buffer`, `GroupBy`, and `DistinctUntilChanged`, you can bridge the channel to an Rx.NET `IObservable`.

## Setup

```bash
dotnet add package System.Reactive
```

## Bridge Extension

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

## Throttle Changes

Only process the last event per throttle window:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .Build();

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

## Buffer Events

Batch events into time windows:

```csharp
var subscription = monitor.Events
    .AsObservable()
    .Buffer(TimeSpan.FromSeconds(5))
    .Where(batch => batch.Any())
    .Subscribe(batch =>
    {
        Console.WriteLine($"Batch of {batch.Count} events");
        foreach (var evt in batch)
            Console.WriteLine($"  {evt.Kind}: {evt.FullPath}");
    });
```

## Per-File Throttle

Throttle independently per file path:

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

## Why Optional?

Rx.NET is **not a dependency** of Cocoar.FileSystem. The channel-based approach covers 90% of use cases without adding external packages. Use Rx.NET only when you need advanced operators.

| Approach | Dependency | Best For |
|----------|-----------|----------|
| `await foreach` | None | Simple sequential processing |
| Built-in debounce | None | Per-file throttling |
| Manual throttle | None | Custom logic |
| Rx.NET | `System.Reactive` | Throttle, Buffer, GroupBy, Merge, etc. |
