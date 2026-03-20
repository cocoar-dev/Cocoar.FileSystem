# FileReader

`FileReader` provides secure file reading with shared access support. It reads files as byte arrays — unlike strings, byte arrays can be zeroed out after use.

## Why Not File.ReadAllBytes?

| Feature | `FileReader.ReadAllBytes` | `File.ReadAllBytes` |
|---------|---------------------------|---------------------|
| Shared access | `FileShare.ReadWrite` | Exclusive |
| BOM stripping | Built-in | Manual |
| Try-pattern | `TryReadAllBytes` | Must catch exception |
| Validation | Comprehensive | Basic |
| Error messages | Detailed | Generic |

The key difference: `FileReader` opens files with `FileShare.ReadWrite`, allowing it to read files that are currently being written by other processes (loggers, other services, etc.).

## API

### ReadAllBytes

```csharp
public static byte[] ReadAllBytes(string path, bool stripUtf8Bom = false)
```

Reads all bytes from a file. Throws if the file doesn't exist.

### TryReadAllBytes

```csharp
public static byte[]? TryReadAllBytes(string path, bool stripUtf8Bom = false)
```

Returns `null` instead of throwing if the file doesn't exist. Still throws for I/O errors.

## Reading Sensitive Data

The primary use case: read credentials or secrets as bytes, process them, then zero the array:

```csharp
byte[]? secret = null;
try
{
    secret = FileReader.ReadAllBytes(@"C:\secrets\key.dat");
    ProcessSecret(secret);
}
finally
{
    if (secret != null)
        Array.Clear(secret, 0, secret.Length);
}
```

Unlike `File.ReadAllText()` which returns an immutable `string` that stays in memory until GC collects it, byte arrays can be cleared immediately.

## UTF-8 BOM Stripping

Text files saved with UTF-8 BOM (`EF BB BF`) can cause issues when parsing. Enable automatic stripping:

```csharp
byte[] text = FileReader.ReadAllBytes(@"C:\config.json", stripUtf8Bom: true);
string json = Encoding.UTF8.GetString(text);
// Clean JSON, no BOM prefix
```

| Input | `stripUtf8Bom: false` | `stripUtf8Bom: true` |
|-------|----------------------|---------------------|
| `EF BB BF` + "Hello" | `EF BB BF 48 65 6C 6C 6F` | `48 65 6C 6C 6F` |
| "Hello" (no BOM) | `48 65 6C 6C 6F` | `48 65 6C 6C 6F` |
| Empty file | `[]` | `[]` |
| Only BOM | `EF BB BF` | `[]` |

## Try-Pattern

Prefer `TryReadAllBytes` over catching `FileNotFoundException`:

```csharp
// Preferred
byte[]? data = FileReader.TryReadAllBytes(@"C:\config\override.json");
if (data != null)
{
    ApplyOverride(data);
}

// Avoid
try
{
    var data = FileReader.ReadAllBytes(@"C:\config\override.json");
    ApplyOverride(data);
}
catch (FileNotFoundException) { }
```

## Shared Access

`FileReader` opens files with `FileShare.ReadWrite`:

```csharp
// Works even if a logger is actively writing to this file
byte[] logData = FileReader.ReadAllBytes(@"C:\logs\application.log");
```

This makes it safe to read:
- Active log files
- Database files being written
- Files locked by other processes (non-exclusive)

## Thread Safety

All `FileReader` methods are thread-safe. Multiple threads can read the same file concurrently.

## Common Patterns

### Configuration Reader

```csharp
public Config LoadConfig(string path)
{
    byte[] data = FileReader.ReadAllBytes(path, stripUtf8Bom: true);
    string json = Encoding.UTF8.GetString(data);
    return JsonSerializer.Deserialize<Config>(json)!;
}
```

### Optional Config with Fallback

```csharp
byte[]? override = FileReader.TryReadAllBytes(@"C:\config\override.json");
byte[] main = FileReader.ReadAllBytes(@"C:\config\main.json");

if (override != null)
    ApplyOverride(override);

ApplyMain(main);
```

### Secret Manager

```csharp
public void UseSecret(string path, Action<byte[]> action)
{
    byte[]? secret = null;
    try
    {
        secret = FileReader.ReadAllBytes(path);
        action(secret);
    }
    finally
    {
        if (secret != null)
            Array.Clear(secret, 0, secret.Length);
    }
}
```
