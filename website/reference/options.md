# Configuration Options

## ResilientFileSystemMonitor.Options

All configuration options for the monitor.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Path` | `string` | (required) | Directory path to monitor |
| `Filter` | `string` | `"*"` | Single file filter pattern |
| `Filters` | `string[]?` | `null` | Multiple file filter patterns |
| `IncludeSubdirectories` | `bool` | `false` | Monitor subdirectories |
| `MaxDepth` | `int` | `0` | Max subdirectory depth (0=root, -1=unlimited) |
| `NotifyFilter` | `NotifyFilters` | `LastWrite \| FileName \| Size` | Types of changes to watch |
| `EnablePollingFallback` | `bool` | `true` | Enable automatic polling fallback |
| `PollingInterval` | `TimeSpan` | 5 seconds | Polling interval in fallback mode |
| `AutoRecoverFromErrors` | `bool` | `true` | Auto-switch to polling on errors |
| `HealthCheckInterval` | `TimeSpan` | 1 second | Directory existence check interval |
| `AuditInterval` | `TimeSpan` | 60 seconds | Metadata fingerprint audit interval |
| `DebounceTime` | `TimeSpan?` | `null` | Event debounce window |
| `InternalBufferSize` | `int` | 65536 (64 KB) | FileSystemWatcher buffer size |
| `EnableAdaptiveHashOnReconcile` | `bool` | `false` | Enable content hashing on audit |
| `AdaptiveHashBytesPerEdge` | `int` | 65536 (64 KB) | Bytes to hash from file start/end |
| `TrackSymlinkTargets` | `bool` | `false` | Detect a watched symlink's resolved-target swap (ConfigMap/Secret reload) |

## Depth Values

| Value | Meaning |
|-------|---------|
| `0` | Root directory only (default) |
| `1` | Direct children |
| `2` | Children and grandchildren |
| `n` | `n` levels deep |
| `-1` | Unlimited depth |

## NotifyFilters

Default: `NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size`

Available flags from `System.IO.NotifyFilters`:

| Filter | Description |
|--------|-------------|
| `FileName` | File name changes (create, delete, rename) |
| `DirectoryName` | Directory name changes |
| `Attributes` | File attribute changes |
| `Size` | File size changes |
| `LastWrite` | Last write time changes |
| `LastAccess` | Last access time changes |
| `CreationTime` | Creation time changes |
| `Security` | Security setting changes |

## Builder-to-Options Mapping

| Builder Method | Options Property |
|---------------|-----------------|
| `Watch(path)` | `Path` |
| `Watch(path, filter)` | `Path`, `Filter` |
| `WithFilter(...)` | `Filters` |
| `IncludeSubdirectories(bool)` | `IncludeSubdirectories` |
| `IncludeSubdirectories(int)` | `MaxDepth` + `IncludeSubdirectories` |
| `WithDebounce(...)` | `DebounceTime` |
| `WithPollingFallback(...)` | `PollingInterval` + `EnablePollingFallback` |
| `WithoutPollingFallback()` | `EnablePollingFallback = false` |
| `WithoutAutoRecovery()` | `AutoRecoverFromErrors = false` |
| `WithNotifyFilters(...)` | `NotifyFilter` |
| `WithInternalBufferSize(...)` | `InternalBufferSize` |
| `WithHealthCheckInterval(...)` | `HealthCheckInterval` |
| `WithAuditInterval(...)` | `AuditInterval` |
| `WithAdaptiveHashing(...)` | `EnableAdaptiveHashOnReconcile` + `AdaptiveHashBytesPerEdge` |
| `WithSymlinkTargetTracking()` | `TrackSymlinkTargets` |

## Example: Full Options

```csharp
var options = new ResilientFileSystemMonitor.Options
{
    Path = @"C:\certs",
    Filters = new[] { "*.pfx", "*.p12", "*.cer" },
    IncludeSubdirectories = true,
    MaxDepth = 2,
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
    EnablePollingFallback = true,
    PollingInterval = TimeSpan.FromSeconds(5),
    AutoRecoverFromErrors = true,
    HealthCheckInterval = TimeSpan.FromSeconds(1),
    AuditInterval = TimeSpan.FromMinutes(1),
    DebounceTime = TimeSpan.FromMilliseconds(500),
    InternalBufferSize = 65536,
    EnableAdaptiveHashOnReconcile = false,
    AdaptiveHashBytesPerEdge = 65536,
    TrackSymlinkTargets = false,
};

var monitor = new ResilientFileSystemMonitor(options);
```
