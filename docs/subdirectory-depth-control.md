# Subdirectory Depth Control

Control how deeply `ResilientFileSystemMonitor` monitors subdirectories with flexible depth limiting.

## Overview

By default, `ResilientFileSystemMonitor` only watches files in the specified directory (non-recursive). You can enable subdirectory monitoring with fine-grained control over depth:

- **Default**: Root directory only (depth 0)
- **Level 1**: Direct children only
- **Level 2+**: Specific depth limits
- **Unlimited**: All subdirectories (depth -1)

## Quick Examples

### Default Behavior (Non-Recursive)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .Build();

// Only monitors: C:\certs\*.pfx
// Ignores: C:\certs\prod\*.pfx, C:\certs\prod\2025\*.pfx, etc.
```

### Unlimited Depth

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .IncludeSubdirectories()  // Parameterless = unlimited
    .Build();

// Monitors all *.pfx files at any depth:
// - C:\certs\*.pfx
// - C:\certs\prod\*.pfx
// - C:\certs\prod\2025\*.pfx
// - C:\certs\prod\2025\archive\*.pfx
// - etc.
```

### Limited Depth (Recommended)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .IncludeSubdirectories(1)  // Only direct children
    .Build();

// Monitors:
// ✅ C:\certs\root.pfx (depth 0)
// ✅ C:\certs\prod\cert.pfx (depth 1)
// ❌ C:\certs\prod\2025\cert.pfx (depth 2 - too deep!)
```

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\projects", "*.cs")
    .IncludeSubdirectories(3)  // Up to 3 levels
    .Build();

// Good for avoiding node_modules, bin, obj folders
```

## Method Overloads

### 1. Boolean with Default (Simple On/Off)

```csharp
.IncludeSubdirectories()       // Default parameter = true (unlimited depth)
.IncludeSubdirectories(true)   // Unlimited depth (explicit)
.IncludeSubdirectories(false)  // No subdirectories (depth 0)
```

Simple toggle for enabling/disabling recursive monitoring. Parameterless call defaults to `true` for convenience.

### 2. Integer (Precise Depth)

```csharp
.IncludeSubdirectories(0)   // Root directory only (same as default)
.IncludeSubdirectories(1)   // Direct children only
.IncludeSubdirectories(2)   // Children and grandchildren
.IncludeSubdirectories(3)   // Three levels deep
.IncludeSubdirectories(-1)  // Unlimited depth
```

Precise control over monitoring depth.

## Depth Level Examples

### Example Directory Structure

```
C:\certs\
├── root-cert.pfx          (depth 0)
├── fallback.pfx           (depth 0)
├── prod\
│   ├── prod-2024.pfx      (depth 1)
│   ├── prod-2025.pfx      (depth 1)
│   └── 2025\
│       └── jan.pfx        (depth 2)
└── staging\
    └── archive\
        └── old.pfx        (depth 2)
```

### Depth 0 (Default)

```csharp
.Watch(@"C:\certs", "*.pfx")
// No .IncludeSubdirectories() call

// Monitored:
// ✅ root-cert.pfx
// ✅ fallback.pfx
// ❌ prod\prod-2024.pfx
// ❌ prod\prod-2025.pfx
// ❌ prod\2025\jan.pfx
// ❌ staging\archive\old.pfx
```

### Depth 1

```csharp
.Watch(@"C:\certs", "*.pfx")
.IncludeSubdirectories(1)

// Monitored:
// ✅ root-cert.pfx (depth 0)
// ✅ fallback.pfx (depth 0)
// ✅ prod\prod-2024.pfx (depth 1)
// ✅ prod\prod-2025.pfx (depth 1)
// ❌ prod\2025\jan.pfx (depth 2 - too deep!)
// ❌ staging\archive\old.pfx (depth 2 - too deep!)
```

### Depth 2

```csharp
.Watch(@"C:\certs", "*.pfx")
.IncludeSubdirectories(2)

// Monitored:
// ✅ root-cert.pfx (depth 0)
// ✅ fallback.pfx (depth 0)
// ✅ prod\prod-2024.pfx (depth 1)
// ✅ prod\prod-2025.pfx (depth 1)
// ✅ prod\2025\jan.pfx (depth 2)
// ✅ staging\archive\old.pfx (depth 2)
```

### Unlimited (-1)

```csharp
.Watch(@"C:\certs", "*.pfx")
.IncludeSubdirectories(-1)  // or .IncludeSubdirectories(true) or .IncludeSubdirectories()

// Monitored: ALL .pfx files at any depth
```

## Performance Considerations

### Depth Filtering Implementation

Events from **all subdirectories** are received by the underlying `FileSystemWatcher` (it only supports boolean on/off). Depth filtering happens **in managed code** by calculating the relative path depth and comparing against the configured limit.

```csharp
// Pseudo-code of the filtering logic:
if (pathDepth > configuredMaxDepth)
    return; // Event is silently dropped
```

### Performance Impact

**Minimal overhead:**
- Depth calculation is just string parsing and counting separators
- Filtering happens on background threads
- FileSystemWatcher is kernel-based (OS already watches entire tree)

**Recommendation:** Use depth limits when monitoring large directory trees to avoid:
- Processing events from `node_modules`, `.git`, `bin`, `obj` folders
- Event flooding from deeply nested dependency trees
- Unnecessary event processing overhead

### Example: Monitoring a Code Repository

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\repos\myproject", "*.cs")
    .IncludeSubdirectories(3)  // Limit depth to avoid bin/obj/node_modules
    .OnChanged((s, e) => TriggerRecompile(e.FullPath))
    .Build();

// Typical structure:
// C:\repos\myproject\
// ├── src\              (depth 1) ✅
// │   ├── Controllers\  (depth 2) ✅
// │   │   └── Api\      (depth 3) ✅
// │   │       └── V1\   (depth 4) ❌ Too deep
// │   └── bin\          (depth 2) ✅ But likely filtered by pattern
// └── node_modules\     (depth 1) ✅ But likely filtered by pattern
```

## Platform Support

Depth control works consistently across all platforms:

- ✅ **Windows** - Uses `ReadDirectoryChangesW` internally
- ✅ **Linux** - Uses `inotify` internally  
- ✅ **macOS** - Uses `FSEvents` internally

The filtering is implemented in **managed C# code**, so behavior is identical on all platforms.

## Validation

Invalid depth values throw `ArgumentException`:

```csharp
// ❌ Invalid - throws ArgumentException
.IncludeSubdirectories(-2)  // Must be >= -1
.IncludeSubdirectories(-10) // Must be >= -1
```

Valid values:
- `-1` = unlimited
- `0` = root only
- `1+` = specific depth limit

## Migration from v1.0.0

**BREAKING CHANGE**: Default behavior changed from recursive to non-recursive.

### Before (v1.0.0)

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .Build();
// ⚠️ Monitored ALL subdirectories by default
```

### After (v1.1.0+)

```csharp
// To maintain old behavior, explicitly enable:
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .IncludeSubdirectories()  // Now explicit
    .Build();

// Or use the new default (non-recursive):
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .Build();
// ✅ Only monitors root directory
```

## Use Cases

### 1. Certificate Management (Multi-Environment)

```csharp
// Monitor certificates in prod/staging/dev folders, but not deeper
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\certs", "*.pfx")
    .IncludeSubdirectories(1)
    .OnCreated((s, e) => RefreshCertCache())
    .Build();
```

### 2. Configuration Files (Organized by Environment)

```csharp
// Monitor configs in env folders (prod/test/dev), not nested archives
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\config", "*.json")
    .IncludeSubdirectories(1)
    .OnChanged((s, e) => ReloadConfig(e.FullPath))
    .Build();
```

### 3. Source Code Monitoring (Avoid Build Artifacts)

```csharp
// Monitor source files, but limit depth to avoid bin/obj/packages
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\src", "*.cs")
    .IncludeSubdirectories(4)  // Reasonable depth for source trees
    .OnChanged((s, e) => TriggerHotReload())
    .Build();
```

### 4. Log File Monitoring (Single Directory)

```csharp
// Only monitor root log directory, ignore rotated/archived logs in subfolders
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\logs", "*.log")
    // No .IncludeSubdirectories() - default depth 0
    .OnCreated((s, e) => ParseNewLog(e.FullPath))
    .Build();
```

## Best Practices

1. **Default to Non-Recursive**: Only enable subdirectory monitoring when needed
2. **Use Depth Limits**: Prefer specific depth over unlimited when possible
3. **Combine with Patterns**: Use file patterns to further reduce noise
4. **Consider Structure**: Design directory structures with monitoring in mind
5. **Monitor Selectively**: Create multiple monitors for specific subtrees if needed

## See Also

- [ResilientFileSystemMonitor Guide](resilient-file-system-monitor.md)
- [Examples](examples.md)
- [Event Ordering Guarantees](event-ordering-guarantee.md)
