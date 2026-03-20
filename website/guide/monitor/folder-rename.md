# Folder Rename Detection

`ResilientFileSystemMonitor` automatically detects when directories containing matching files are renamed, and emits a `Renamed` event.

## Why This Matters

In production, certificate rotation often works by atomically renaming folders:

```
kid2-staging/      →  kid2/
  └── cert.pfx         └── cert.pfx
```

A raw `FileSystemWatcher` reports this as a directory rename, but doesn't tell you it contained files you care about. Cocoar checks the renamed directory for matching files and emits a `Renamed` event so you can react.

## How It Works

1. A directory rename event is received from `FileSystemWatcher`
2. Cocoar uses `FileSearcher` to check if the renamed directory contains files matching your filter patterns
3. If matching files exist **and** the folder is within `MaxDepth`, a single `Renamed` event is emitted with the folder path
4. If no matching files exist, the rename is silently ignored

## Usage

No configuration needed — folder rename detection works out of the box:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs")
    .WithFilter("*.pfx")
    .IncludeSubdirectories()
    .OnRenamed((s, e) =>
    {
        // Fires for:
        // - Individual .pfx files being renamed
        // - Folders containing .pfx files being renamed
        Console.WriteLine($"Renamed: {e.OldFullPath} -> {e.FullPath}");
        ReloadCertificates();
    })
    .Build();
```

## Behavior Details

- Only fires if the renamed directory **contains matching files** at any depth within it
- Respects `MaxDepth` — if the renamed folder is deeper than your configured depth, it's ignored
- Respects filter patterns — only triggers when files matching your patterns exist in the folder
- Emits a **single** `Renamed` event per folder rename (not one per file inside)
- Uses `FileSearcher` internally for efficient, lazy directory scanning

## Example: Certificate Rotation

```csharp
// Directory structure:
// C:\certs\
//   ├── kid1\cert.pfx
//   └── kid2-staging\cert.pfx    ← Will be renamed to kid2\

var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs")
    .WithFilter("*.pfx", "*.p12")
    .IncludeSubdirectories(1)
    .OnRenamed((s, e) =>
    {
        _logger.LogInformation("Certificate folder renamed: {Old} -> {New}",
            e.OldFullPath, e.FullPath);
        RefreshCertificateCache();
    })
    .Build();

// When kid2-staging is renamed to kid2:
// → Renamed event: C:\certs\kid2-staging -> C:\certs\kid2
```
