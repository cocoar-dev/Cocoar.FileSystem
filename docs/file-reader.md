# FileReader

`FileReader` provides secure file reading operations with shared access support, particularly useful when reading sensitive content as bytes to avoid immutable strings in memory.

## Why Use FileReader?

### Key Benefits

- ✅ **Shared Access** - Opens files with `FileShare.ReadWrite` - works even when other processes have the file open for writing
- ✅ **BOM Handling** - Optional UTF-8 BOM (Byte Order Mark) stripping for text files
- ✅ **Security** - Byte arrays can be zeroed out after use (unlike strings which are immutable in .NET)
- ✅ **Try-pattern** - `TryReadAllBytes` returns `null` instead of throwing for missing files
- ✅ **Validation** - Proper argument validation and meaningful error messages

## API Overview

### ReadAllBytes

Reads all bytes from a file with shared read/write access.

```csharp
public static byte[] ReadAllBytes(string path, bool stripUtf8Bom = false)
```

**Parameters:**
- `path` - Path to the file to read (required)
- `stripUtf8Bom` - If `true`, removes UTF-8 BOM (`EF BB BF`) from the beginning if present (default: `false`)

**Returns:**
- Byte array containing the file contents

**Exceptions:**
- `ArgumentNullException` - When `path` is `null`
- `ArgumentException` - When `path` is empty or whitespace
- `FileNotFoundException` - When file does not exist
- `IOException` - When file cannot be read completely or file is larger than `int.MaxValue` bytes

### TryReadAllBytes

Attempts to read all bytes from a file with shared read/write access. Returns `null` if the file does not exist.

```csharp
public static byte[]? TryReadAllBytes(string path, bool stripUtf8Bom = false)
```

**Parameters:**
- `path` - Path to the file to read (required)
- `stripUtf8Bom` - If `true`, removes UTF-8 BOM (`EF BB BF`) from the beginning if present (default: `false`)

**Returns:**
- Byte array containing the file contents, or `null` if file does not exist

**Exceptions:**
- `ArgumentNullException` - When `path` is `null`
- `ArgumentException` - When `path` is empty or whitespace
- `IOException` - When file exists but cannot be read completely

## Usage Examples

### Basic File Reading

```csharp
using Cocoar.FileSystem;

// Read any file
byte[] data = FileReader.ReadAllBytes(@"C:\data\file.bin");
Console.WriteLine($"Read {data.Length} bytes");

// Read with BOM stripping
byte[] textData = FileReader.ReadAllBytes(@"C:\data\file.txt", stripUtf8Bom: true);
string text = Encoding.UTF8.GetString(textData);
```

### Secure Credential Reading

Read sensitive data and clear it from memory after use:

```csharp
using Cocoar.FileSystem;

byte[]? passwordBytes = null;
try
{
    passwordBytes = FileReader.ReadAllBytes(@"C:\secrets\password.dat");
    
    // Use the password
    Authenticate(passwordBytes);
}
finally
{
    // CRITICAL: Zero out the byte array to remove from memory
    if (passwordBytes != null)
    {
        Array.Clear(passwordBytes, 0, passwordBytes.Length);
    }
}
```

### Try-Read Pattern

Handle optional files gracefully:

```csharp
using Cocoar.FileSystem;

// Try to read optional configuration
byte[]? configData = FileReader.TryReadAllBytes(@"C:\config\override.json");

if (configData != null)
{
    Console.WriteLine("Override configuration found");
    ApplyOverride(configData);
}
else
{
    Console.WriteLine("No override, using defaults");
}
```

### Reading Files with Shared Access

Read files that are currently open by other processes:

```csharp
using Cocoar.FileSystem;

// This works even if another process has the file open for writing
// (e.g., a logging process writing to an active log file)
byte[] logData = FileReader.ReadAllBytes(@"C:\logs\application.log");

// Process the log data
AnalyzeLogs(logData);
```

### UTF-8 BOM Handling

Properly handle text files with or without BOM:

```csharp
using Cocoar.FileSystem;
using System.Text;

// Strip BOM if present - ensures clean UTF-8 text
byte[] textBytes = FileReader.ReadAllBytes(@"C:\data\textfile.txt", stripUtf8Bom: true);
string text = Encoding.UTF8.GetString(textBytes);

// File with BOM (EF BB BF + "Hello"):     Returns "Hello"
// File without BOM ("Hello"):             Returns "Hello"
// Empty file:                             Returns ""
// Only BOM (EF BB BF):                    Returns ""
```

## Best Practices

### 1. Always Clear Sensitive Data

When reading sensitive information, always zero out the byte array:

```csharp
byte[]? sensitiveData = null;
try
{
    sensitiveData = FileReader.ReadAllBytes(secretFilePath);
    ProcessSecret(sensitiveData);
}
finally
{
    if (sensitiveData != null)
    {
        Array.Clear(sensitiveData, 0, sensitiveData.Length);
    }
}
```

### 2. Use Try-Pattern for Optional Files

Prefer `TryReadAllBytes` over catching `FileNotFoundException`:

```csharp
// ❌ Bad
byte[]? data = null;
try
{
    data = FileReader.ReadAllBytes(optionalPath);
}
catch (FileNotFoundException)
{
    // File doesn't exist
}

// ✅ Good
byte[]? data = FileReader.TryReadAllBytes(optionalPath);
if (data != null)
{
    ProcessData(data);
}
```

### 3. Strip BOM for Text Files

When reading text files, enable BOM stripping to ensure clean text:

```csharp
// ✅ Good - handles files with or without BOM
byte[] textBytes = FileReader.ReadAllBytes(textPath, stripUtf8Bom: true);
string text = Encoding.UTF8.GetString(textBytes);
```

### 4. Shared Access Benefits

`FileReader` uses `FileShare.ReadWrite`, allowing you to read files that are:
- Currently being written by another process
- Open in another application
- Locked for writing (but not exclusive access)

```csharp
// This works even if the log file is actively being written
byte[] currentLog = FileReader.ReadAllBytes(@"C:\logs\active.log");
```

## Common Scenarios

### Configuration Management

```csharp
public class ConfigReader
{
    public Config LoadConfig(string path)
    {
        byte[] data = FileReader.ReadAllBytes(path, stripUtf8Bom: true);
        string json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<Config>(json)!;
    }
    
    public Config? LoadOptionalConfig(string path)
    {
        byte[]? data = FileReader.TryReadAllBytes(path, stripUtf8Bom: true);
        if (data == null) return null;
        
        string json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<Config>(json);
    }
}
```

### Log Analysis

```csharp
public class LogAnalyzer
{
    public LogStats AnalyzeActiveLog(string logPath)
    {
        // Can read even while logger is writing to it
        byte[] logData = FileReader.ReadAllBytes(logPath);
        
        // Parse and analyze
        return ParseLogEntries(logData);
    }
}
```

### Secret Management

```csharp
public class SecretManager
{
    public void UseSecret(string secretPath, Action<byte[]> action)
    {
        byte[]? secret = null;
        try
        {
            secret = FileReader.ReadAllBytes(secretPath);
            action(secret);
        }
        finally
        {
            // Always clean up
            if (secret != null)
            {
                Array.Clear(secret, 0, secret.Length);
            }
        }
    }
}
```

## Performance Considerations

- **Memory**: Loads entire file into memory. For very large files (> 100 MB), consider streaming approaches.
- **I/O**: Single synchronous read operation - very fast for local files, may be slower for network paths.
- **Shared Access**: Opening with `FileShare.ReadWrite` is as fast as exclusive access on modern file systems.
- **BOM Stripping**: Zero-copy slice operation (`bytes[3..]`) when BOM is detected - negligible overhead.

## Thread Safety

`FileReader` methods are thread-safe. Multiple threads can safely call `ReadAllBytes` or `TryReadAllBytes` concurrently, even on the same file path.

## Comparison with BCL Methods

| Feature | `FileReader.ReadAllBytes` | `File.ReadAllBytes` |
|---------|---------------------------|---------------------|
| Shared access | ✅ Yes (`FileShare.ReadWrite`) | ❌ No (exclusive) |
| BOM stripping | ✅ Built-in | ❌ Manual |
| Try-pattern | ✅ `TryReadAllBytes` | ❌ Must catch exception |
| Validation | ✅ Comprehensive | ⚠️ Basic |
| Error messages | ✅ Detailed | ⚠️ Generic |
| File size limit | ✅ Checked (`int.MaxValue`) | ✅ Same |

## When to Use FileReader

**Use FileReader when:**
- You need shared read access (file may be open by other processes)
- Reading sensitive data that should be cleared from memory
- Working with text files that may have UTF-8 BOM
- You want try-pattern for optional files
- You need detailed error messages

**Use `File.ReadAllBytes` when:**
- You need exclusive access to ensure file doesn't change during read
- File is guaranteed not to be open elsewhere
- You want absolutely minimal dependencies (FileReader is in Cocoar.FileSystem)

## See Also

- [ResilientFileSystemMonitor](resilient-file-system-monitor.md) - Monitor file changes with auto-recovery
- [FileSearcher](examples.md#high-performance-file-search) - High-performance file traversal
- [Examples](examples.md#secure-file-reading) - More FileReader examples
