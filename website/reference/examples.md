# Examples

## Configuration Hot-Reload Service

Complete ASP.NET Core hosted service:

```csharp
using Cocoar.FileSystem;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class HotReloadService : IHostedService, IDisposable
{
    private readonly ILogger<HotReloadService> _logger;
    private ResilientFileSystemMonitor? _monitor;

    public HotReloadService(ILogger<HotReloadService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var watchPath = Path.Combine(AppContext.BaseDirectory, "configs");

        _monitor = ResilientFileSystemMonitor
            .Watch(watchPath, "*.json")
            .WithDebounce(500)
            .WithPollingFallback(TimeSpan.FromSeconds(5))
            .OnChanged((s, e) =>
                _logger.LogInformation("Config changed: {Name}", e.Name))
            .OnCreated((s, e) =>
                _logger.LogInformation("Config created: {Name}", e.Name))
            .OnError((s, e) =>
                _logger.LogWarning(e.GetException(), "Monitor error"))
            .OnModeChanged((s, e) =>
                _logger.LogInformation("Monitor mode: {Mode}", e.Mode))
            .Build();

        _logger.LogInformation("Hot reload started for {Path}", watchPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose() => _monitor?.Dispose();
}
```

## Certificate Watcher

Monitor multiple certificate formats with folder rename support:

```csharp
using Cocoar.FileSystem;

public class CertificateWatcher : IDisposable
{
    private readonly ILogger<CertificateWatcher> _logger;
    private ResilientFileSystemMonitor? _monitor;

    public CertificateWatcher(ILogger<CertificateWatcher> logger)
    {
        _logger = logger;
    }

    public void Watch(string certPath)
    {
        _monitor = ResilientFileSystemMonitor
            .Watch(certPath)
            .WithFilter("*.pfx", "*.p12", "*.cer")
            .IncludeSubdirectories(1)
            .WithDebounce(TimeSpan.FromSeconds(2))
            .OnChanged((s, e) =>
            {
                _logger.LogInformation("Certificate changed: {Path}", e.FullPath);
                ReloadCertificate(e.FullPath);
            })
            .OnRenamed((s, e) =>
            {
                _logger.LogInformation("Certificate renamed: {Old} -> {New}",
                    e.OldFullPath, e.FullPath);
                RefreshCertificateCache();
            })
            .OnModeChanged((s, e) =>
                _logger.LogInformation("Watcher mode: {Mode} - {Reason}",
                    e.Mode, e.Reason))
            .Build();
    }

    private void ReloadCertificate(string path) { /* ... */ }
    private void RefreshCertificateCache() { /* ... */ }
    public void Dispose() => _monitor?.Dispose();
}
```

## Docker Volume Monitoring

Handle volumes that appear after container start:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch("/app/data", "*.dat")
    .WithPollingFallback(TimeSpan.FromSeconds(5))
    .OnModeChanged((s, e) =>
    {
        if (e.Mode == WatcherMode.Native)
            Console.WriteLine("Volume mounted, using native watcher");
    })
    .OnCreated((s, e) => ProcessFile(e.FullPath))
    .Build();
```

## Network Share Monitoring

Resilient monitoring of UNC paths:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"\\server\share\configs", "*.json")
    .WithPollingFallback(TimeSpan.FromSeconds(15))
    .WithDebounce(1000)
    .OnChanged((s, e) => Console.WriteLine($"Changed: {e.Name}"))
    .OnError((s, e) =>
        Console.WriteLine($"Network error: {e.GetException().Message}"))
    .OnModeChanged((s, e) =>
        Console.WriteLine($"Mode: {e.Mode} - {e.Reason}"))
    .Build();
```

## Async Event Stream Processing

Using the ChannelReader for async iteration:

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch(@"C:\data")
    .Build();

await foreach (var evt in monitor.Events.ReadAllAsync())
{
    switch (evt.Kind)
    {
        case FileSystemEventKind.Created:
            await ProcessNewFileAsync(evt.FullPath);
            break;
        case FileSystemEventKind.Changed:
            await ReprocessFileAsync(evt.FullPath);
            break;
        case FileSystemEventKind.Deleted:
            await CleanupAsync(evt.FullPath);
            break;
    }
}
```

## Code Repository Scanner

Find source files with exclusions and depth limits:

```csharp
var sourceFiles = FileSearcher
    .InDirectory(@"C:\repos\myapp")
    .WithFilter("*.cs", "*.csproj", "*.razor")
    .Excluding("bin", "obj", "node_modules", ".git", "packages")
    .IncludeSubdirectories(5)
    .ToList();

Console.WriteLine($"Found {sourceFiles.Count} source files");
```

## Large File Finder

Lazy evaluation with LINQ:

```csharp
var largeFiles = FileSearcher
    .InDirectory(@"C:\data")
    .WithPattern("*.log")
    .Recursively()
    .Where(f => new FileInfo(f).Length > 10_000_000)
    .OrderByDescending(f => new FileInfo(f).Length)
    .Take(10)
    .ToList();

foreach (var file in largeFiles)
    Console.WriteLine($"{file}: {new FileInfo(file).Length:N0} bytes");
```

## Secure Secret Reading

Read sensitive data with proper cleanup:

```csharp
byte[]? secret = null;
try
{
    secret = FileReader.ReadAllBytes(@"C:\secrets\api-key.dat");
    CallApi(secret);
}
finally
{
    if (secret != null)
        Array.Clear(secret, 0, secret.Length);
}
```

## Test Helper

Detect file changes in integration tests:

```csharp
[Fact]
public async Task ShouldDetectFileCreation()
{
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempDir);

    var tcs = new TaskCompletionSource<string>();

    using var monitor = ResilientFileSystemMonitor
        .Watch(tempDir, "*.txt")
        .OnCreated((s, e) => tcs.TrySetResult(e.Name!))
        .Build();

    await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "content");

    var fileName = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal("test.txt", fileName);

    Directory.Delete(tempDir, true);
}
```
