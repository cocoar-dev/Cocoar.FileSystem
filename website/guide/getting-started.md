# Getting Started

## Installation

```bash
dotnet add package Cocoar.FileSystem
```

Cocoar.FileSystem targets **.NET 8.0** and has **zero external dependencies**.

## Quick Start

### Monitor a Directory

```csharp
using Cocoar.FileSystem;

var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\configs", "*.json")
    .WithDebounce(500)
    .OnChanged((sender, e) => Console.WriteLine($"Changed: {e.Name}"))
    .OnCreated((sender, e) => Console.WriteLine($"Created: {e.Name}"))
    .Build();
```

The monitor automatically handles:
- Directory not existing at startup (waits via polling)
- Watcher crashes (falls back to polling)
- Directory deletion and recreation (detects via identity tracking)

### Search for Files

```csharp
var files = FileSearcher
    .InDirectory(@"C:\repos\myproject")
    .WithFilter("*.cs", "*.csproj")
    .Excluding("bin", "obj", "node_modules")
    .IncludeSubdirectories(3)
    .ToList();
```

### Read Files Securely

```csharp
byte[] content = FileReader.ReadAllBytes(@"C:\secrets\key.dat");
try
{
    ProcessSensitiveData(content);
}
finally
{
    Array.Clear(content, 0, content.Length); // Zero out when done
}
```

## What's in the Package

| Class | Purpose |
|-------|---------|
| `ResilientFileSystemMonitor` | Production-ready file monitoring with auto-recovery |
| `MonitorBuilder` | Fluent API for configuring the monitor |
| `FileSearcher` | High-performance lazy file enumeration |
| `SearchBuilder` | Fluent API for configuring searches |
| `FileReader` | Secure byte-based file reading with shared access |
| `FileSystemEvent` | Unified event model for all event types |

## Minimal Example

```csharp
using Cocoar.FileSystem;

// Monitor + react in 3 lines
using var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .OnChanged((s, e) => Console.WriteLine($"{e.Name} changed"))
    .Build();

Console.ReadLine(); // Keep alive
```

## Next Steps

- [Why Cocoar.FileSystem?](/guide/why-cocoar-filesystem) — Problems this library solves
- [Monitor Overview](/guide/monitor/overview) — Deep dive into resilient monitoring
- [FileSearcher](/guide/searcher/overview) — High-performance file search
- [FileReader](/guide/reader/overview) — Secure file reading
