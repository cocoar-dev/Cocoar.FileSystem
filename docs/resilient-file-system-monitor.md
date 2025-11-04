# ResilientFileSystemMonitor

A production-ready FileSystemWatcher replacement with automatic fallback, error recovery, and debouncing.

## Features

- ✅ **Automatic Fallback**: Switches to polling when FileSystemWatcher fails or directory doesn't exist
- ✅ **Auto-Recovery**: Handles directory disappearing/reappearing, permission errors, watcher crashes
- ✅ **Debouncing**: Optional built-in debouncing to reduce noise from rapid file changes
- ✅ **Configurable**: Opt-in to features you need via Options pattern
- ✅ **Observable**: Know current state (watcher vs polling) via properties and events

## Usage Examples

### Basic Usage (Resilient Config File Monitoring)

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\configs",
    Filter = "*.json",
    EnablePollingFallback = true,
    PollingInterval = TimeSpan.FromSeconds(5)
});

monitor.Changed += (sender, e) => 
{
    Console.WriteLine($"File changed: {e.Name}");
    ReloadConfiguration(e.FullPath);
};

monitor.Created += (sender, e) => 
{
    Console.WriteLine($"File created: {e.Name}");
};
```

### With Debouncing (Reduce Noise)

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\certificates",
    Filter = "*.pfx",
    EnablePollingFallback = true,
    DebounceTime = TimeSpan.FromMilliseconds(500) // Only fire once per file per 500ms
});

monitor.Changed += (sender, e) => 
{
    // This will only fire once even if file is saved multiple times rapidly
    RefreshCertificateCache();
};
```

### Monitor Mode Changes (Observability)

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data",
    EnablePollingFallback = true
});

monitor.ModeChanged += (sender, e) => 
{
    _logger.LogInformation($"Monitor switched to {e.NewMode}: {e.Reason}");
};

monitor.Error += (sender, e) => 
{
    _logger.LogWarning($"Monitor error: {e.GetException().Message}");
};

// Check current state
if (monitor.IsWatcherActive)
{
    Console.WriteLine("Using efficient FileSystemWatcher");
}
else if (monitor.IsPolling)
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
