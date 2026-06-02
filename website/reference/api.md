# API Overview

## ResilientFileSystemMonitor

Production-ready file monitoring with automatic fallback and error recovery.

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Watch(string path)` | `MonitorBuilder` | Create a builder for the specified directory |
| `Watch(string path, string filter)` | `MonitorBuilder` | Create a builder with an initial filter |

### Constructor

```csharp
public ResilientFileSystemMonitor(Options options)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Events` | `ChannelReader<FileSystemEvent>` | Unified event stream |
| `IsUsingWatcher` | `bool` | `true` if using native FileSystemWatcher |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `Created` | `EventHandler<FileSystemEventArgs>` | File created |
| `Changed` | `EventHandler<FileSystemEventArgs>` | File changed |
| `Deleted` | `EventHandler<FileSystemEventArgs>` | File deleted |
| `Renamed` | `EventHandler<RenamedEventArgs>` | File or folder renamed |
| `Error` | `EventHandler<ErrorEventArgs>` | Watcher error |
| `ModeChanged` | `EventHandler<ModeChangedEventArgs>` | Switched between native/polling |

### Implements

`IDisposable`

---

## MonitorBuilder

Fluent API for configuring `ResilientFileSystemMonitor`.

| Method | Returns | Description |
|--------|---------|-------------|
| `WithFilter(params string[])` | `MonitorBuilder` | Add file patterns (additive) |
| `ClearFilters()` | `MonitorBuilder` | Remove all patterns |
| `IncludeSubdirectories(bool)` | `MonitorBuilder` | Enable/disable recursion |
| `IncludeSubdirectories(int maxDepth)` | `MonitorBuilder` | Set recursion depth |
| `WithDebounce(int ms)` | `MonitorBuilder` | Set debounce in milliseconds |
| `WithDebounce(TimeSpan)` | `MonitorBuilder` | Set debounce interval |
| `WithPollingFallback(int ms)` | `MonitorBuilder` | Set polling interval in ms |
| `WithPollingFallback(TimeSpan)` | `MonitorBuilder` | Set polling interval |
| `WithoutPollingFallback()` | `MonitorBuilder` | Disable polling |
| `WithoutAutoRecovery()` | `MonitorBuilder` | Disable auto-recovery |
| `WithNotifyFilters(NotifyFilters)` | `MonitorBuilder` | Set notify filters |
| `WithInternalBufferSize(int)` | `MonitorBuilder` | Set watcher buffer size |
| `WithHealthCheckInterval(TimeSpan)` | `MonitorBuilder` | Set health check interval |
| `WithAuditInterval(TimeSpan)` | `MonitorBuilder` | Set audit interval |
| `WithAdaptiveHashing(int)` | `MonitorBuilder` | Enable content hashing |
| `WithSymlinkTargetTracking()` | `MonitorBuilder` | Detect symlink target swaps (ConfigMap reload) |
| `OnCreated(EventHandler)` | `MonitorBuilder` | Subscribe to Created |
| `OnChanged(EventHandler)` | `MonitorBuilder` | Subscribe to Changed |
| `OnDeleted(EventHandler)` | `MonitorBuilder` | Subscribe to Deleted |
| `OnRenamed(EventHandler)` | `MonitorBuilder` | Subscribe to Renamed |
| `OnError(EventHandler)` | `MonitorBuilder` | Subscribe to Error |
| `OnModeChanged(EventHandler)` | `MonitorBuilder` | Subscribe to ModeChanged |
| `Build()` | `ResilientFileSystemMonitor` | Build and start the monitor |

---

## FileSearcher

High-performance lazy file enumeration.

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `InDirectory(string path)` | `SearchBuilder` | Start a search in the specified directory |
| `Search(string path, string pattern)` | `SearchBuilder` | Start a search with an initial pattern |
| `EnumerateFiles(DirectoryInfo, string, HashSet?, int?)` | `IEnumerable<string>` | Low-level enumeration |

---

## SearchBuilder

Fluent API for configuring file searches. Implements `IEnumerable<string>`.

| Method | Returns | Description |
|--------|---------|-------------|
| `WithFilter(params string[])` | `SearchBuilder` | Add file patterns (additive) |
| `WithPattern(string)` | `SearchBuilder` | Set single pattern (clears filters) |
| `ClearFilters()` | `SearchBuilder` | Remove all patterns |
| `Excluding(params string[])` | `SearchBuilder` | Exclude folder names |
| `IncludeSubdirectories(bool)` | `SearchBuilder` | Enable/disable recursion |
| `IncludeSubdirectories(int maxDepth)` | `SearchBuilder` | Set recursion depth |
| `WithMaxDepth(int?)` | `SearchBuilder` | Set max depth (null = unlimited) |
| `Recursively()` | `SearchBuilder` | Unlimited depth |
| `Enumerate()` | `IEnumerable<string>` | Lazy enumeration |
| `ToList()` | `List<string>` | Eager load |
| `ToArray()` | `string[]` | Eager load |
| `Count()` | `int` | Count matches |
| `Any()` | `bool` | Check for matches |
| `First()` | `string` | First match |
| `FirstOrDefault()` | `string?` | First match or null |

---

## FileReader

Secure byte-based file reading with shared access.

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadAllBytes(string path, bool stripUtf8Bom)` | `byte[]` | Read file bytes |
| `TryReadAllBytes(string path, bool stripUtf8Bom)` | `byte[]?` | Read or null if missing |

---

## FileSystemEvent

Unified event model.

| Property | Type | Description |
|----------|------|-------------|
| `Kind` | `FileSystemEventKind` | Event type |
| `FullPath` | `string` | Full file path |
| `Name` | `string` | File name |
| `ChangeType` | `WatcherChangeTypes` | Original change type |
| `OldFullPath` | `string?` | Previous path (Renamed) |
| `OldName` | `string?` | Previous name (Renamed) |
| `Exception` | `Exception?` | Error details |
| `Mode` | `WatcherMode?` | New mode (ModeChanged) |
| `Reason` | `string?` | Mode change reason |

### Factory Methods

| Method | Description |
|--------|-------------|
| `FileSystemEvent.Create(args)` | Created event |
| `FileSystemEvent.Change(args)` | Changed event |
| `FileSystemEvent.Delete(args)` | Deleted event |
| `FileSystemEvent.Rename(args)` | Renamed event |
| `FileSystemEvent.Error(args)` | Error event |
| `FileSystemEvent.ModeChange(args)` | Mode changed event |

---

## Enums

### FileSystemEventKind

`Created`, `Changed`, `Deleted`, `Renamed`, `Error`, `ModeChanged`

### WatcherMode

`Native`, `Polling`
