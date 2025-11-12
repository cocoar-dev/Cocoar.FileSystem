# Cocoar.FileSystem

[![NuGet](https://img.shields.io/nuget/v/Cocoar.FileSystem.svg)](https://www.nuget.org/packages/Cocoar.FileSystem/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

> Production-ready file system utilities for .NET with automatic error recovery and multi-platform support.

## 🚀 Features

- **🛡️ Resilient File System Monitoring** - Production-ready FileSystemWatcher with automatic fallback
- **🔄 Auto-Recovery** - Handles directory disappearing/reappearing, permission errors, watcher crashes
- **⚡ Efficient & Reliable** - Automatic switching between FileSystemWatcher (efficient) and polling (resilient)
- **🎯 Debouncing** - Built-in debouncing to reduce noise from rapid file changes
- **🚀 High-Performance File Search** - Fast directory traversal with lazy evaluation
- **🔐 Secure File Reading** - Read files as bytes with shared access support (prevent immutable strings in memory)
- **🌍 Cross-Platform** - Tested on Windows, Linux, and macOS
- **📦 Zero Dependencies** - Lightweight with no external dependencies

## 📥 Installation

```bash
dotnet add package Cocoar.FileSystem
```

## 📖 Quick Start

### Resilient File System Monitoring

Monitor directories with automatic error recovery and fallback:

```csharp
using Cocoar.FileSystem;

var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\configs", "*.json")
    .WithDebounce(500) // Optional: reduce noise from rapid changes
    .OnChanged((sender, e) => Console.WriteLine($"Changed: {e.Name}"))
    .OnCreated((sender, e) => Console.WriteLine($"Created: {e.Name}"))
    .Build();

// With subdirectory monitoring (depth control):
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\configs", "*.json")
    .IncludeSubdirectories(2) // Monitor up to 2 levels deep
    .OnChanged((sender, e) => Console.WriteLine($"Changed: {e.FullPath}"))
    .Build();
```

**Key Features:** Auto-recovery, debouncing, depth control, reactive streams, health checks  
📖 **[Full Guide](docs/resilient-file-system-monitor.md)** | **[Reactive Examples](docs/reactive-examples.md)**

---

### High-Performance File Search

Fast, lazy-evaluated directory traversal:

```csharp
using Cocoar.FileSystem;

var codeFiles = FileSearcher
    .Search(@"C:\repos\myproject", "*.cs")
    .Excluding("bin", "obj", "node_modules")
    .WithMaxDepth(5)
    .ToList();
```

**Key Features:** Lazy evaluation, depth limits, exclusion patterns, LINQ support  
📖 **[Examples](docs/examples.md#high-performance-file-search)**

---

### Secure File Reading

Read files with shared access and security features:

```csharp
using Cocoar.FileSystem;

byte[] content = FileReader.ReadAllBytes(@"C:\config.dat");
try 
{
    ProcessSensitiveData(content);
}
finally
{
    Array.Clear(content, 0, content.Length); // Zero out when done
}
```

**Key Features:** Shared read/write access, BOM stripping, try-pattern, byte array zeroing  
📖 **[Full Guide](docs/file-reader.md)**

---

## 📚 Documentation

- **[ResilientFileSystemMonitor Guide](docs/resilient-file-system-monitor.md)** - Detailed usage guide
- **[Subdirectory Depth Control](docs/subdirectory-depth-control.md)** - Fine-grained recursive monitoring
- **[FileReader Guide](docs/file-reader.md)** - Secure file reading with shared access
- **[Examples](docs/examples.md)** - More usage examples
- **[Reactive Examples](docs/reactive-examples.md)** - Advanced reactive patterns with Rx.NET
- **[Event Ordering Guarantees](docs/event-ordering-guarantee.md)** - Thread-safety and event ordering
- **[Test Coverage](docs/test-coverage.md)** - Comprehensive test documentation

## 🔑 Key Benefits Over Raw FileSystemWatcher

- ✅ No crashes when directory doesn't exist initially
- ✅ No silent failures when watcher encounters errors
- ✅ Handles Docker volume mounts appearing after container start
- ✅ Handles network share availability issues
- ✅ Built-in debouncing (no need for manual throttling)
- ✅ Production-ready error handling
- ✅ Cross-platform compatibility

## 🛠️ Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Path` | `string` | (required) | Directory path to monitor |
| `Filter` | `string` | `"*"` | File filter pattern (e.g., "*.json", "*.txt") |
| `IncludeSubdirectories` | `bool` | `false` | Monitor subdirectories recursively |
| `MaxDepth` | `int` | `0` | Maximum subdirectory depth (0=root only, -1=unlimited) |
| `NotifyFilter` | `NotifyFilters` | `LastWrite \| FileName \| Size` | Types of changes to watch for |
| `EnablePollingFallback` | `bool` | `true` | Enable automatic polling fallback |
| `PollingInterval` | `TimeSpan` | `5 seconds` | How often to poll when in fallback mode |
| `AutoRecoverFromErrors` | `bool` | `true` | Automatically switch to polling on errors |
| `DebounceTime` | `TimeSpan?` | `null` | Optional debouncing to reduce event noise |

### Subdirectory Depth Control

Control how deep to monitor subdirectories:

```csharp
// Don't monitor subdirectories (default)
.Watch(@"C:\certs")  // Only watches C:\certs\*.pfx

// Monitor all subdirectories (unlimited depth)
.Watch(@"C:\certs")
.IncludeSubdirectories()  // or .IncludeSubdirectories(true) or .IncludeSubdirectories(-1)

// Monitor with depth limit
.Watch(@"C:\certs")
.IncludeSubdirectories(1)  // Only direct children: C:\certs\prod\*.pfx
.IncludeSubdirectories(2)  // Two levels: C:\certs\prod\2025\*.pfx
```

## 🧵 Thread Safety

All operations are thread-safe. Events are raised sequentially on a background thread, guaranteeing:
- ✅ Events are delivered in the order they occurred
- ✅ No concurrent event handler invocations (unless you subscribe multiple handlers)
- ✅ When using the `Events` channel, you have full control over concurrency

## 🗑️ Disposal

Always dispose when done:

```csharp
using var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .Build();
// Use monitor...
// Automatically disposed at end of scope
```

## 🤝 Contributing & Versioning

- SemVer (additive MINOR, breaking MAJOR)
- PRs & issues welcome

## 📄 License & Trademark

This project is licensed under the [Apache License, Version 2.0](LICENSE). See [`NOTICE`](NOTICE) for attribution.

"Cocoar" and related marks are trademarks of COCOAR e.U. Use of the name in forks or derivatives should preserve attribution and avoid implying official endorsement. See [TRADEMARKS](TRADEMARKS.md) for permitted and restricted uses.
