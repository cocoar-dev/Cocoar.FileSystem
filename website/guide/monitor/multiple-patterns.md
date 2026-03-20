# Multiple File Patterns

Monitor multiple file extensions or patterns simultaneously.

## Basic Usage

```csharp
// Monitor multiple certificate formats
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs")
    .WithFilter("*.pfx", "*.p12", "*.cer")
    .OnChanged((s, e) => ReloadCertificate(e.FullPath))
    .Build();
```

## Additive Behavior

Calling `WithFilter()` multiple times **adds** patterns — it doesn't replace:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\logs")
    .WithFilter("*.log")          // Add .log files
    .WithFilter("*.txt")          // Add .txt files
    .WithFilter("error-*.json")   // Add error JSON files
    .Build();
// Monitors: *.log, *.txt, error-*.json
```

## Clearing Patterns

Use `ClearFilters()` to reset and start fresh:

```csharp
var builder = ResilientFileSystemMonitor.Watch(@"C:\data");
builder.WithFilter("*.tmp");
builder.ClearFilters();              // Remove all patterns
builder.WithFilter("*.dat", "*.bin"); // Start fresh
var monitor = builder.Build();
```

## Pattern Syntax

Uses DOS-style wildcards (same as `FileSystemName.MatchesSimpleExpression`):

| Pattern | Matches |
|---------|---------|
| `*.pfx` | All `.pfx` files |
| `*.p12` | All `.p12` files |
| `config-*.json` | `config-dev.json`, `config-prod.json` |
| `backup-????.log` | `backup-2025.log`, `backup-abcd.log` |
| `*.*` | All files with extensions |
| `*` | All files |

Matching is against the **filename only** (not the full path), for performance.

## Constructor Filter

The initial filter passed to `Watch()` is treated as a single pattern:

```csharp
// "*.json" is the initial pattern
var builder = ResilientFileSystemMonitor.Watch(@"C:\data", "*.json");

// Adding more patterns
builder.WithFilter("*.yaml");
// Now monitors: *.json, *.yaml
```

## Options Pattern

When using the `Options` class directly:

```csharp
var monitor = new ResilientFileSystemMonitor(
    new ResilientFileSystemMonitor.Options
    {
        Path = @"C:\certs",
        Filters = new[] { "*.pfx", "*.p12", "*.cer" },
    });
```

Use the `Filters` property (string array) for multiple patterns, or `Filter` (string) for a single pattern.
