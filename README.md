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
- **🌍 Cross-Platform** - Tested on Windows, Linux, and macOS
- **📦 Zero Dependencies** - Lightweight with no external dependencies

## 📥 Installation

```bash
dotnet add package Cocoar.FileSystem
```

## 📖 Quick Start

### Basic File Monitoring

```csharp
using Cocoar.FileSystem;

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

### With Debouncing

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data",
    Filter = "*.txt",
    EnablePollingFallback = true,
    DebounceTime = TimeSpan.FromMilliseconds(500) // Only fire once per file per 500ms
});

monitor.Changed += (sender, e) => 
{
    // This will only fire once even if file is saved multiple times rapidly
    ProcessFile(e.FullPath);
};
```

### Reactive Stream (ChannelReader)

For reactive scenarios, you can consume all events as a unified ordered stream:

```csharp
var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
{
    Path = @"C:\data"
});

// Consume events from the channel - all events in order!
await foreach (var evt in monitor.Events.ReadAllAsync(cancellationToken))
{
    switch (evt.Kind)
    {
        case FileSystemEventKind.Created:
            Console.WriteLine($"Created: {evt.FullPath}");
            break;
        case FileSystemEventKind.Changed:
            Console.WriteLine($"Changed: {evt.FullPath}");
            break;
        case FileSystemEventKind.Deleted:
            Console.WriteLine($"Deleted: {evt.FullPath}");
            break;
        case FileSystemEventKind.Renamed:
            Console.WriteLine($"Renamed: {evt.OldFullPath} → {evt.FullPath}");
            break;
        case FileSystemEventKind.Error:
            Console.WriteLine($"Error: {evt.Exception?.Message}");
            break;
        case FileSystemEventKind.ModeChanged:
            Console.WriteLine($"Mode changed to {evt.Mode}: {evt.Reason}");
            break;
    }
}
```

Benefits:
- ✅ All events (Created, Changed, Deleted, Renamed, Error, ModeChanged) in **one ordered stream**
- ✅ No race conditions - events are guaranteed to be delivered in order
- ✅ Works perfectly with LINQ, Rx.NET, or async iteration
- ✅ No need for `System.Reactive` dependency

```csharp
// Example: Throttle with LINQ
await foreach (var evt in monitor.Events.ReadAllAsync()
    .Where(e => e.Kind == FileSystemEventKind.Changed)
    .ToAsyncEnumerable())
{
    // Process only change events
}
```


### High-Performance File Search

```csharp
using Cocoar.FileSystem;

// Simple search - all text files
var files = FileSearcher.EnumerateFiles(
    new DirectoryInfo(@"C:\projects"),
    "*.txt");

foreach (var file in files)
{
    Console.WriteLine(file);
}

// With depth limit and excluded folders
var codeFiles = FileSearcher.EnumerateFiles(
    new DirectoryInfo(@"C:\repos\myproject"),
    "*.cs",
    excludedFolders: new HashSet<string> { "bin", "obj", "node_modules", ".git" },
    maxDepth: 5);

foreach (var file in codeFiles)
{
    ProcessCodeFile(file);
}

// Lazy evaluation - efficient for large directory trees
var largeSearch = FileSearcher.EnumerateFiles(
    new DirectoryInfo(@"C:\large-directory"),
    "*");

// Only enumerates first 10 files
var firstTen = largeSearch.Take(10).ToList();
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
    Console.WriteLine($"Monitor switched to {e.NewMode}: {e.Reason}");
};

monitor.Error += (sender, e) => 
{
    Console.WriteLine($"Monitor error: {e.GetException().Message}");
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

## 📚 Documentation

- **[ResilientFileSystemMonitor Guide](docs/resilient-file-system-monitor.md)** - Detailed usage guide
- **[Examples](docs/examples.md)** - More usage examples

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
| `NotifyFilter` | `NotifyFilters` | `LastWrite \| FileName \| CreationTime` | Types of changes to watch for |
| `EnablePollingFallback` | `bool` | `true` | Enable automatic polling fallback |
| `PollingInterval` | `TimeSpan` | `5 seconds` | How often to poll when in fallback mode |
| `AutoRecoverFromErrors` | `bool` | `true` | Automatically switch to polling on errors |
| `DebounceTime` | `TimeSpan?` | `null` | Optional debouncing to reduce event noise |

## 🧵 Thread Safety

All operations are thread-safe. Events are raised sequentially on a background thread, guaranteeing:
- ✅ Events are delivered in the order they occurred
- ✅ No concurrent event handler invocations (unless you subscribe multiple handlers)
- ✅ When using the `Events` channel, you have full control over concurrency

## 🗑️ Disposal

Always dispose when done:

```csharp
using var monitor = new ResilientFileSystemMonitor(options);
// Use monitor...
// Automatically disposed at end of scope
```

## 🤝 Contributing & Versioning

- SemVer (additive MINOR, breaking MAJOR)
- PRs & issues welcome
- Licensed under Apache License 2.0 (explicit patent grant & attribution via NOTICE)

## 📄 License & Trademark

This project is licensed under the [Apache License, Version 2.0](LICENSE). See [`NOTICE`](NOTICE) for attribution.

"Cocoar" and related marks are trademarks of COCOAR e.U. Use of the name in forks or derivatives should preserve attribution and avoid implying official endorsement. See [TRADEMARKS](TRADEMARKS.md) for permitted and restricted uses.