# SearchBuilder API

The `SearchBuilder` provides a fluent API for configuring file searches.

## Creating a Builder

```csharp
// With initial pattern
var builder = FileSearcher.Search(@"C:\data", "*.json");

// Without pattern
var builder = FileSearcher.InDirectory(@"C:\data");
```

## File Patterns

### Single Pattern

```csharp
.WithPattern("*.cs")
```

`WithPattern` sets a single pattern, clearing any previous filters.

### Multiple Patterns

```csharp
.WithFilter("*.cs", "*.csproj", "*.json")
```

`WithFilter` is additive — calling it multiple times adds patterns:

```csharp
.WithFilter("*.cs")          // *.cs
.WithFilter("*.csproj")      // *.cs, *.csproj
.WithFilter("*.json", "*.xml") // *.cs, *.csproj, *.json, *.xml
```

### Clearing Patterns

```csharp
.ClearFilters()
```

Removes all configured patterns. Useful for builder reuse.

## Recursion & Depth

```csharp
// Current directory only
.IncludeSubdirectories(false)
.IncludeSubdirectories(0)
.WithMaxDepth(0)

// Unlimited depth
.IncludeSubdirectories()
.IncludeSubdirectories(true)
.IncludeSubdirectories(-1)
.Recursively()
.WithMaxDepth(null)

// Specific depth
.IncludeSubdirectories(3)
.WithMaxDepth(3)
```

## Folder Exclusion

```csharp
.Excluding("bin", "obj", "node_modules", ".git")
```

Folder names are matched **case-insensitively**. The search skips excluded directories entirely — it doesn't descend into them.

## Terminal Operations

```csharp
// Lazy (returns IEnumerable<string>)
var files = builder.Enumerate();
foreach (var file in files) { ... }

// Eager
var list = builder.ToList();
var array = builder.ToArray();
var count = builder.Count();
var exists = builder.Any();
var first = builder.First();
var firstOrNull = builder.FirstOrDefault();
```

## IEnumerable Implementation

`SearchBuilder` implements `IEnumerable<string>`, so you can use it directly in `foreach` and LINQ:

```csharp
foreach (var file in FileSearcher.Search(@"C:\data", "*.log").Recursively())
{
    Console.WriteLine(file);
}

var recent = FileSearcher
    .InDirectory(@"C:\data")
    .WithPattern("*.*")
    .Recursively()
    .Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddDays(-1))
    .ToList();
```

## Full Example

```csharp
var projectFiles = FileSearcher
    .InDirectory(@"C:\repos\myapp")
    .WithFilter("*.cs", "*.csproj")
    .WithFilter("*.json")             // Additive
    .Excluding("bin", "obj", "node_modules", ".git", "packages")
    .IncludeSubdirectories(5)          // Limit depth
    .ToList();

Console.WriteLine($"Found {projectFiles.Count} project files");
```
