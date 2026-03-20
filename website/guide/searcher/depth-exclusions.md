# Depth & Exclusions

Fine-tune `FileSearcher` results by controlling recursion depth and excluding directories.

## Depth Control

### No Recursion (Default)

```csharp
var files = FileSearcher
    .Search(@"C:\app", "*.json")
    .WithMaxDepth(0)
    .ToArray();
// Only files in C:\app\, not subdirectories
```

### Limited Depth

```csharp
var files = FileSearcher
    .Search(@"C:\repos\myproject", "*.cs")
    .WithMaxDepth(3)
    .ToList();

// Depth 0: C:\repos\myproject\*.cs
// Depth 1: C:\repos\myproject\src\*.cs
// Depth 2: C:\repos\myproject\src\Controllers\*.cs
// Depth 3: C:\repos\myproject\src\Controllers\Api\*.cs
// NOT:     C:\repos\myproject\src\Controllers\Api\V1\*.cs
```

### Unlimited Depth

```csharp
var files = FileSearcher
    .Search(@"C:\repos", "*.cs")
    .Recursively()
    .ToList();
// All *.cs files at any depth
```

## Folder Exclusion

### Basic Exclusion

```csharp
var files = FileSearcher
    .Search(@"C:\repos\myproject", "*.cs")
    .Excluding("bin", "obj", "node_modules")
    .Recursively()
    .ToList();
```

Excluded directories are **not descended into** — the search doesn't even enumerate their contents. This makes exclusion very efficient for skipping large directories like `node_modules`.

### Case Insensitivity

Folder name matching is **case-insensitive**:

```csharp
.Excluding("bin")
// Matches: bin, Bin, BIN, biN
```

### Common Exclusion Sets

```csharp
// .NET project
.Excluding("bin", "obj", "packages", ".vs")

// Node.js project
.Excluding("node_modules", "dist", ".next", "coverage")

// General
.Excluding(".git", ".svn", ".hg")
```

## Combining Depth and Exclusions

```csharp
var sourceFiles = FileSearcher
    .InDirectory(@"C:\repos\myapp")
    .WithFilter("*.cs", "*.razor")
    .Excluding("bin", "obj", "node_modules", ".git")
    .IncludeSubdirectories(4)
    .ToList();

// Efficient: limited depth AND excluded folders
// Won't descend into bin/obj (exclusion)
// Won't go deeper than 4 levels (depth limit)
```

## Performance Tips

1. **Exclude early**: Large excluded directories like `node_modules` are skipped entirely
2. **Limit depth**: Prevents scanning deeply nested structures
3. **Use patterns**: Narrower patterns mean less work per directory
4. **Lazy evaluation**: Use `Take()` or `First()` to stop early

```csharp
// Efficient: stops after finding one match
var hasTests = FileSearcher
    .InDirectory(@"C:\repos\myapp")
    .WithPattern("*Tests.cs")
    .Recursively()
    .Excluding("bin", "obj")
    .Any();
```

## Inaccessible Directories

`FileSearcher` silently skips directories it cannot access (permission denied, etc.). No exceptions are thrown — inaccessible directories are simply ignored.
