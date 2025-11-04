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

All operations are thread-safe. Events are raised on background threads.

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