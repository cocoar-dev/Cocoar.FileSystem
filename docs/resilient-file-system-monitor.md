# ResilientFileSystemMonitor

A production-ready FileSystemWatcher replacement with automatic fallback, error recovery, and debouncing.

## Features

- ✅ **Automatic Fallback**: Switches to polling when FileSystemWatcher fails or directory doesn't exist
- ✅ **Auto-Recovery**: Handles directory disappearing/reappearing, permission errors, watcher crashes
- ✅ **Debouncing**: Optional built-in debouncing to reduce noise from rapid file changes
- ✅ **Fluent API**: Clean, discoverable API with method chaining
- ✅ **Options Pattern**: Alternative configuration for serialization scenarios
- ✅ **Observable**: Know current state (watcher vs polling) via properties and events

## Usage Examples

### Basic Usage (Fluent API)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\configs", "*.json")
    .OnChanged((sender, e) => 
    {
        Console.WriteLine($"File changed: {e.Name}");
        ReloadConfiguration(e.FullPath);
    })
    .OnCreated((sender, e) => 
    {
        Console.WriteLine($"File created: {e.Name}");
    })
    .OnRenamed((sender, e) => 
    {
        Console.WriteLine($"File renamed: {e.OldName} → {e.Name}");
    })
    .Build();
```

### With Debouncing (Reduce Noise)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certificates", "*.pfx")
    .WithDebounce(500) // milliseconds - only fire once per file per 500ms
    .OnChanged((sender, e) => 
    {
        // This will only fire once even if file is saved multiple times rapidly
        RefreshCertificateCache();
    })
    .Build();
```

### Advanced Configuration

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .WithFilter("*.txt")
    .WithDebounce(TimeSpan.FromMilliseconds(500))
    .WithPollingFallback(TimeSpan.FromSeconds(3))
    .WithHealthCheckInterval(TimeSpan.FromSeconds(1))
    .WithAuditInterval(TimeSpan.FromMinutes(1))
    .IncludeSubdirectories(true)
    .OnCreated(OnFileCreated)
    .OnChanged(OnFileChanged)
    .OnDeleted(OnFileDeleted)
    .OnError(OnMonitorError)
    .OnModeChanged(OnModeChanged)
    .Build();
```

### Options Pattern (Alternative)

For configuration files or when you need to serialize options:

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\configs",
    Filter = "*.json",
    EnablePollingFallback = true,
    PollingInterval = TimeSpan.FromSeconds(5),
    DebounceTime = TimeSpan.FromMilliseconds(500)
});

monitor.Changed += (sender, e) => ReloadConfiguration(e.FullPath);
```

### Monitor Mode Changes (Observability)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .OnModeChanged((sender, e) => 
    {
        _logger.LogInformation($"Monitor switched to {e.NewMode}: {e.Reason}");
    })
    .OnError((sender, e) => 
    {
        _logger.LogWarning($"Monitor error: {e.GetException().Message}");
    })
    .Build();

// Check current state
if (monitor.IsUsingWatcher)
{
    Console.WriteLine("Using efficient FileSystemWatcher");
}
else
{
    Console.WriteLine("Using polling fallback");
}
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Path` | `string` | (required) | Directory path to monitor |
| `Filter` | `string` | `"*"` | File filter pattern (e.g., "*.json", "*.pfx") |
| `IncludeSubdirectories` | `bool` | `false` | Monitor subdirectories recursively |
| `NotifyFilter` | `NotifyFilters` | `LastWrite \| FileName \| CreationTime` | Types of changes to watch for |
| `EnablePollingFallback` | `bool` | `true` | Enable automatic polling fallback |
| `PollingInterval` | `TimeSpan` | `5 seconds` | How often to poll when in fallback mode |
| `AutoRecoverFromErrors` | `bool` | `true` | Automatically switch to polling on errors |
| `DebounceTime` | `TimeSpan?` | `null` | Optional debouncing to reduce event noise |

## How It Works

1. **Initial State**: Attempts to create FileSystemWatcher if directory exists
2. **Directory Missing**: Falls back to polling, waiting for directory to appear
3. **Watcher Error**: Automatically switches to polling fallback
4. **Recovery**: When directory becomes available, switches back to efficient watcher
5. **Debouncing**: If enabled, filters out rapid duplicate events per file

## Benefits Over Raw FileSystemWatcher

- ✅ No crashes when directory doesn't exist initially
- ✅ No silent failures when watcher encounters errors
- ✅ Handles Docker volume mounts appearing after container start
- ✅ Handles network share availability issues
- ✅ Built-in debouncing (no need for manual throttling)
- ✅ Production-ready error handling

## Thread Safety

All operations are thread-safe. Events are raised on background threads.

## Disposal

Always dispose when done:

```csharp
using var monitor = new ResilientFileSystemMonitor(options);
// Use monitor...
// Automatically disposed at end of scope
```
