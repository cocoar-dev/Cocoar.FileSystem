# Examples

## Configuration File Monitoring

Monitor configuration files and reload when they change:

```csharp
using Cocoar.FileSystem;

public class ConfigurationService
{
    private ResilientFileSystemMonitor? _monitor;
    
    public void StartMonitoring(string configDirectory)
    {
        _monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = configDirectory,
            Filter = "*.json",
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromSeconds(10),
            DebounceTime = TimeSpan.FromMilliseconds(500)
        });
        
        _monitor.Changed += OnConfigChanged;
        _monitor.Created += OnConfigChanged;
        _monitor.Deleted += OnConfigDeleted;
        _monitor.Renamed += OnConfigRenamed;
    }
    
    private void OnConfigChanged(object? sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Configuration changed: {e.FullPath}");
        ReloadConfiguration(e.FullPath);
    }
    
    private void OnConfigDeleted(object? sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Configuration deleted: {e.FullPath}");
        RemoveConfiguration(e.FullPath);
    }
    
    private void OnConfigRenamed(object? sender, RenamedEventArgs e)
    {
        Console.WriteLine($"Configuration renamed: {e.OldFullPath} → {e.FullPath}");
        RemoveConfiguration(e.OldFullPath);
        ReloadConfiguration(e.FullPath);
    }
    
    private void ReloadConfiguration(string filePath)
    {
        // Reload logic here
    }
    
    private void RemoveConfiguration(string filePath)
    {
        // Cleanup logic here
    }
}
```

## Certificate Monitoring

Monitor a certificate directory with error handling:

```csharp
using Cocoar.FileSystem;

public class CertificateWatcher
{
    private readonly ILogger<CertificateWatcher> _logger;
    private ResilientFileSystemMonitor? _monitor;
    
    public CertificateWatcher(ILogger<CertificateWatcher> logger)
    {
        _logger = logger;
    }
    
    public void Watch(string certificatePath)
    {
        _monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = certificatePath,
            Filter = "*.pfx",
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromSeconds(30),
            DebounceTime = TimeSpan.FromSeconds(2)
        });
        
        _monitor.Changed += OnCertificateChanged;
        _monitor.Error += OnError;
        _monitor.ModeChanged += OnModeChanged;
    }
    
    private void OnCertificateChanged(object? sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Certificate changed: {Path}", e.FullPath);
        ReloadCertificate(e.FullPath);
    }
    
    private void OnError(object? sender, ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(), "Certificate watcher error");
    }
    
    private void OnModeChanged(object? sender, MonitorModeChangedEventArgs e)
    {
        _logger.LogInformation("Monitor mode changed to {Mode}: {Reason}", 
            e.NewMode, e.Reason);
    }
    
    private void ReloadCertificate(string filePath)
    {
        // Certificate reload logic
    }
}
```

## Docker Volume Monitoring

Monitor a directory that might not exist at startup (Docker volume):

```csharp
using Cocoar.FileSystem;

public class DockerVolumeWatcher
{
    public void MonitorDockerVolume()
    {
        var volumePath = "/app/data"; // Docker volume mount point
        
        var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = volumePath,
            Filter = "*.dat",
            EnablePollingFallback = true, // Will poll until volume appears
            PollingInterval = TimeSpan.FromSeconds(5),
            AutoRecoverFromErrors = true
        });
        
        monitor.ModeChanged += (s, e) => 
        {
            Console.WriteLine($"Mode: {e.NewMode} - {e.Reason}");
        };
        
        monitor.Created += (s, e) => 
        {
            Console.WriteLine($"File created: {e.Name}");
            ProcessNewFile(e.FullPath);
        };
    }
    
    private void ProcessNewFile(string filePath)
    {
        // Process file logic
    }
}
```

## Network Share Monitoring

Monitor a network share with resilient error handling:

```csharp
using Cocoar.FileSystem;

public class NetworkShareMonitor
{
    public void MonitorNetworkShare(string uncPath)
    {
        var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = uncPath, // e.g., @"\\server\share\folder"
            Filter = "*.*",
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromSeconds(15),
            AutoRecoverFromErrors = true,
            DebounceTime = TimeSpan.FromSeconds(1)
        });
        
        monitor.Changed += (s, e) => Console.WriteLine($"Changed: {e.Name}");
        monitor.Created += (s, e) => Console.WriteLine($"Created: {e.Name}");
        monitor.Deleted += (s, e) => Console.WriteLine($"Deleted: {e.Name}");
        monitor.Renamed += (s, e) => Console.WriteLine($"Renamed: {e.OldName} → {e.Name}");
        
        monitor.Error += (s, e) => 
        {
            Console.WriteLine($"Network error: {e.GetException().Message}");
            // Will automatically fall back to polling
        };
        
        monitor.ModeChanged += (s, e) => 
        {
            Console.WriteLine($"Switched to {e.NewMode} mode");
        };
    }
}
```

## Hot Reload Service

Complete service with dependency injection:

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
        
        _monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = watchPath,
            Filter = "*.json",
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromSeconds(5),
            DebounceTime = TimeSpan.FromMilliseconds(500)
        });
        
        _monitor.Changed += OnFileChanged;
        _monitor.Created += OnFileChanged;
        _monitor.Error += OnError;
        
        _logger.LogInformation("Hot reload service started for {Path}", watchPath);
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hot reload service stopping");
        return Task.CompletedTask;
    }
    
    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Configuration file changed: {Name}", e.Name);
        // Trigger configuration reload
    }
    
    private void OnError(object? sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File system monitor error");
    }
    
    public void Dispose()
    {
        _monitor?.Dispose();
    }
}
```

## Testing with Monitor

Unit testing with the monitor:

```csharp
using Cocoar.FileSystem;
using Xunit;

public class FileMonitorTests
{
    [Fact]
    public async Task ShouldDetectFileChanges()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var changedFile = "";
        var eventReceived = new TaskCompletionSource<bool>();
        
        var monitor = new ResilientFileSystemMonitor(new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        });
        
        monitor.Created += (s, e) => 
        {
            changedFile = e.Name;
            eventReceived.SetResult(true);
        };
        
        // Act
        await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "content");
        await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        
        // Assert
        Assert.Equal("test.txt", changedFile);
        
        // Cleanup
        monitor.Dispose();
        Directory.Delete(tempDir, true);
    }
}
```
