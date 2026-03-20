# FileSearcher Overview

`FileSearcher` provides high-performance, lazy-evaluated directory traversal with a fluent API. It uses `FileSystemEnumerable<string>` under the hood for memory-efficient enumeration.

## Quick Start

```csharp
using Cocoar.FileSystem;

// Find all C# files, excluding build folders
var files = FileSearcher
    .Search(@"C:\repos\myproject", "*.cs")
    .Excluding("bin", "obj", "node_modules")
    .Recursively()
    .ToList();
```

## Two Entry Points

```csharp
// With initial pattern
var builder = FileSearcher.Search(@"C:\data", "*.json");

// Without pattern (configure via WithFilter/WithPattern)
var builder = FileSearcher.InDirectory(@"C:\data");
```

## Key Features

### Lazy Evaluation

Results are yielded one at a time. No files are loaded into memory until you iterate:

```csharp
// No I/O happens here
var query = FileSearcher
    .InDirectory(@"C:\Windows")
    .WithPattern("*.dll")
    .Recursively();

// I/O happens here, stops after finding 5
var firstFive = query.Take(5).ToList();
```

### LINQ Integration

`SearchBuilder` implements `IEnumerable<string>`, so LINQ works naturally:

```csharp
var largeFiles = FileSearcher
    .InDirectory(@"C:\data")
    .WithPattern("*.log")
    .Recursively()
    .Where(file => new FileInfo(file).Length > 1_000_000)
    .OrderByDescending(file => new FileInfo(file).Length)
    .Take(10);
```

### Folder Exclusion

Skip specific directories:

```csharp
var files = FileSearcher
    .Search(@"C:\repos", "*.cs")
    .Excluding("bin", "obj", "packages", "node_modules", ".git")
    .Recursively()
    .ToList();
```

Exclusion is **case-insensitive** — `"bin"` matches `Bin`, `BIN`, etc.

### Depth Control

```csharp
.WithMaxDepth(0)     // Current directory only
.WithMaxDepth(3)     // Up to 3 levels deep
.Recursively()       // Unlimited depth (same as WithMaxDepth(null))
```

### Multiple Patterns

```csharp
var files = FileSearcher
    .InDirectory(@"C:\repos")
    .WithFilter("*.cs", "*.csproj", "*.json")
    .Recursively()
    .ToList();
```

## Terminal Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `Enumerate()` | `IEnumerable<string>` | Lazy enumeration |
| `ToList()` | `List<string>` | Eager load into list |
| `ToArray()` | `string[]` | Eager load into array |
| `Count()` | `int` | Count matching files |
| `Any()` | `bool` | Check if any files match |
| `First()` | `string` | First match (throws if none) |
| `FirstOrDefault()` | `string?` | First match or null |

## API Harmony

`FileSearcher`'s `SearchBuilder` uses the same method names and semantics as `MonitorBuilder`:

- `WithFilter(params string[])` — same additive pattern behavior
- `ClearFilters()` — same clearing behavior
- `IncludeSubdirectories(int maxDepth)` — same depth semantics
- `IncludeSubdirectories(bool)` — same on/off toggle

This means switching between monitoring and searching uses the same vocabulary.
