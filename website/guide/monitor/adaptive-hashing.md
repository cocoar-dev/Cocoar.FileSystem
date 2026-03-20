# Adaptive Hashing

Adaptive hashing adds content-level verification to the audit timer, detecting changes even when file metadata (size, timestamps) remains identical.

## The Problem

Some file modifications don't change the file size or last-write timestamp. For example:
- Overwriting a file with content of the exact same length
- File systems with low-resolution timestamps (1-second granularity)
- In-place modifications that don't update metadata

The standard audit timer compares file metadata. Adaptive hashing adds SHA256 content hashing to catch these edge cases.

## Enabling

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .WithAdaptiveHashing()
    .Build();

// Custom bytes per edge (default: 64 KB)
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .WithAdaptiveHashing(bytesPerEdge: 131072)  // 128 KB
    .Build();
```

## How It Works

When the audit timer ticks, it first compares the metadata fingerprint (sizes, timestamps) as usual. If the fingerprint matches, it reads the first and last N bytes of each file, computes a SHA256 hash, and compares against the stored hash. If the hash differs, a synthetic `Changed` event is emitted.

The "bytes per edge" setting controls how many bytes are read from the start and end of each file. The default of 64 KB is a good balance between detection accuracy and I/O cost.

## When to Use

::: tip
Most applications don't need adaptive hashing. The standard metadata-based audit catches 99%+ of real-world file changes. Enable it only if you have a specific scenario where metadata doesn't change.
:::

**Enable when:**
- Monitoring binary files that get overwritten in-place
- File system has low timestamp resolution
- You need cryptographic certainty that files haven't changed

**Don't enable when:**
- Monitoring text/config files (metadata always changes)
- Watching many large files (I/O overhead)
- Standard metadata detection is sufficient

## Options Pattern

```csharp
var monitor = new ResilientFileSystemMonitor(
    new ResilientFileSystemMonitor.Options
    {
        Path = @"C:\data",
        EnableAdaptiveHashOnReconcile = true,
        AdaptiveHashBytesPerEdge = 65536,  // 64 KB
    });
```
