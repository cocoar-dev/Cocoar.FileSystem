# Builder API

The `MonitorBuilder` provides a fluent API for configuring `ResilientFileSystemMonitor`.

## Creating a Builder

```csharp
// With initial filter
var builder = ResilientFileSystemMonitor.Watch(@"C:\data", "*.json");

// Without filter (default: all files)
var builder = ResilientFileSystemMonitor.Watch(@"C:\data");
```

## File Patterns

```csharp
// Single filter via constructor
.Watch(@"C:\data", "*.json")

// Multiple patterns (additive)
.WithFilter("*.pfx", "*.p12", "*.cer")

// Add more patterns later
.WithFilter("*.pem")

// Clear all patterns
.ClearFilters()
```

Patterns use DOS-style wildcards: `*` matches any characters, `?` matches a single character. Matching is against the filename only, not the full path.

## Subdirectory Monitoring

```csharp
// Root only (default)
.Watch(@"C:\data")

// Unlimited depth
.IncludeSubdirectories()
.IncludeSubdirectories(true)
.IncludeSubdirectories(-1)

// Specific depth
.IncludeSubdirectories(1)  // Direct children only
.IncludeSubdirectories(2)  // Two levels deep
```

See [Depth Control](/guide/monitor/depth-control) for details.

## Debouncing

```csharp
// Milliseconds
.WithDebounce(500)

// TimeSpan
.WithDebounce(TimeSpan.FromMilliseconds(500))
```

See [Debouncing](/guide/monitor/debouncing) for details.

## Polling Fallback

```csharp
// Milliseconds
.WithPollingFallback(5000)

// TimeSpan
.WithPollingFallback(TimeSpan.FromSeconds(5))

// Disable polling fallback
.WithoutPollingFallback()
```

See [Polling Fallback](/guide/monitor/polling-fallback) for details.

## Advanced Options

```csharp
// Disable auto-recovery from errors
.WithoutAutoRecovery()

// Customize notify filters
.WithNotifyFilters(NotifyFilters.LastWrite | NotifyFilters.FileName)

// Increase internal buffer
.WithInternalBufferSize(128 * 1024)  // 128 KB

// Customize timers
.WithHealthCheckInterval(TimeSpan.FromSeconds(2))
.WithAuditInterval(TimeSpan.FromMinutes(5))

// Enable adaptive hashing for audit
.WithAdaptiveHashing(bytesPerEdge: 65536)
```

## Event Handlers

```csharp
.OnCreated((sender, e) => Console.WriteLine($"Created: {e.Name}"))
.OnChanged((sender, e) => Console.WriteLine($"Changed: {e.Name}"))
.OnDeleted((sender, e) => Console.WriteLine($"Deleted: {e.Name}"))
.OnRenamed((sender, e) => Console.WriteLine($"Renamed: {e.OldName} -> {e.Name}"))
.OnError((sender, e) => Console.WriteLine($"Error: {e.GetException().Message}"))
.OnModeChanged((sender, e) => Console.WriteLine($"Mode: {e.Mode} - {e.Reason}"))
```

## Building

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\configs", "*.json")
    .WithDebounce(500)
    .IncludeSubdirectories(2)
    .OnChanged((s, e) => Reload(e.FullPath))
    .Build();  // <-- Creates and starts the monitor
```

`Build()` constructs the monitor, attaches all configured event handlers, and starts monitoring immediately.

## Full Example

```csharp
using var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs")
    .WithFilter("*.pfx", "*.p12", "*.cer")
    .IncludeSubdirectories(1)
    .WithDebounce(TimeSpan.FromSeconds(2))
    .WithPollingFallback(TimeSpan.FromSeconds(5))
    .WithHealthCheckInterval(TimeSpan.FromSeconds(1))
    .WithAuditInterval(TimeSpan.FromMinutes(1))
    .WithAdaptiveHashing()
    .OnChanged((s, e) => ReloadCertificate(e.FullPath))
    .OnCreated((s, e) => LoadNewCertificate(e.FullPath))
    .OnDeleted((s, e) => RemoveCertificate(e.FullPath))
    .OnRenamed((s, e) => RenameCertificate(e.OldFullPath, e.FullPath))
    .OnError((s, e) => _logger.LogWarning(e.GetException(), "Monitor error"))
    .OnModeChanged((s, e) => _logger.LogInformation("Monitor: {Mode}", e.Mode))
    .Build();
```
