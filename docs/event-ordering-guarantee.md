# Event Ordering Guarantee

## Problem

File system events (Created, Changed, Deleted, Renamed, Error) can occur rapidly and from multiple sources (FileSystemWatcher callbacks, polling reconciliation, audit checks). Without proper serialization, event handlers could execute in parallel, creating race conditions for consumers.

**Example race condition:**
```csharp
monitor.Changed += (s, e) => { UpdateDatabase(e.FullPath); };
monitor.Deleted += (s, e) => { RemoveFromDatabase(e.FullPath); };
```

If `Changed` and `Deleted` fire in parallel for the same file, the database could end up in an inconsistent state depending on which handler completes first.

## Solution

**All events are delivered sequentially through a `System.Threading.Channels.Channel<EventEntry>`**.

### How it works

1. **Event producers** (FileSystemWatcher callbacks, reconciliation, audit) never invoke event handlers directly
2. They call `EnqueueEvent(new EventEntry(...))` which writes to the channel
3. A dedicated **event delivery task** (`_eventDeliveryTask`) reads from the channel and invokes handlers **one at a time, in order**
4. The channel acts as a sequential queue with guaranteed FIFO ordering

### Key code paths

```csharp
// ✅ CORRECT: Queue event for sequential delivery
private void ApplyEvent(Action updateIndex, string fullPath, WatcherChangeTypes changeType)
{
    lock (_gate)
    {
        updateIndex();
        _lastEventUtc = DateTime.UtcNow;
    }

    if (ShouldEmitEvent(fullPath))
    {
        EnqueueEvent(new EventEntry(EventType.Created, args));  // ← Queued
    }
}

// Background task delivers events sequentially
private async Task DeliverEventsAsync()
{
    await foreach (var entry in _eventChannel.Reader.ReadAllAsync())
    {
        switch (entry.Type)
        {
            case EventType.Created:
                Created?.Invoke(this, entry.Args);  // ← Delivered in order
                break;
            // ...
        }
    }
}
```

### Guarantees

✅ **Sequential execution** - Only one event handler runs at a time  
✅ **FIFO ordering** - Events fire in the exact order they were enqueued  
✅ **Cross-source ordering** - Events from FileSystemWatcher, polling, and audit are all serialized together  
✅ **No Rx dependency** - Uses built-in `System.Threading.Channels`  

## Performance impact

**Negligible.** Channel writes are lock-free and extremely fast (~10-50ns). The sequential delivery task uses async/await and only blocks when the channel is empty. Event handlers execute on a background thread, so file system operations remain fast.

## Consumer best practices

Even with sequential delivery, consumers should still:

1. **Keep handlers fast** - Long-running work should be offloaded (e.g., to `Task.Run()` or a `Channel`)
2. **Be idempotent** - FileSystemWatcher can report duplicate events (especially `Changed`)
3. **Handle exceptions** - Unhandled exceptions in event handlers can crash the delivery task

### Example: Batching with sequential guarantee

```csharp
var batchChannel = Channel.CreateUnbounded<string>();

monitor.Changed += (s, e) =>
{
    // Fast: just queue the path (guaranteed sequential)
    batchChannel.Writer.TryWrite(e.FullPath);
};

// Background task processes batches
Task.Run(async () =>
{
    await foreach (var batch in batchChannel.Reader.ReadAllAsync().Buffer(100, TimeSpan.FromSeconds(1)))
    {
        await ProcessBatchAsync(batch);  // Slow work here
    }
});
```

## Alternative considered: Rx Observable

Using `System.Reactive` would give consumers more power (throttling, buffering, etc.) but adds a heavy dependency. The channel-based approach gives 90% of the benefit with zero external dependencies.

If you need advanced operators, you can bridge:

```csharp
var observable = Observable.Create<FileSystemEventArgs>(observer =>
{
    void Handler(object s, FileSystemEventArgs e) => observer.OnNext(e);
    monitor.Changed += Handler;
    return Disposable.Create(() => monitor.Changed -= Handler);
})
.Throttle(TimeSpan.FromMilliseconds(500));
```

---

**Summary:** All events are delivered sequentially via a channel. No parallel execution, guaranteed order, no Rx dependency.
