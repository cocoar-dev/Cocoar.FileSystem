namespace Cocoar.FileSystem.Tests;

public class ResilientFileSystemMonitorTests
{
    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = Path.GetTempPath()
        };

        var exception = Record.Exception(() => new ResilientFileSystemMonitor(options));

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new ResilientFileSystemMonitor(null!));
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = null!
        };

        Assert.Throws<ArgumentNullException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithNonExistentPath_AndPollingEnabled_ShouldStartPolling()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = nonExistentPath,
            EnablePollingFallback = true
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        Assert.True(monitor.IsPolling);
        Assert.False(monitor.IsWatcherActive);
    }

    [Fact]
    public void Constructor_WithExistingPath_ShouldStartWatcher()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = Path.GetTempPath()
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        Assert.True(monitor.IsWatcherActive);
        Assert.False(monitor.IsPolling);
    }

    [Fact]
    public async Task FileCreation_ShouldTriggerCreatedEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt"
            };

            var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Created += (s, e) => eventReceived.TrySetResult(e);

            await Task.Delay(100);

            var testFile = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(testFile, "test content");

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(2000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            Assert.Equal("test.txt", eventArgs.Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = Path.GetTempPath()
        };

        var monitor = new ResilientFileSystemMonitor(options);
        monitor.Dispose();

        Assert.False(monitor.IsWatcherActive);
        Assert.False(monitor.IsPolling);
    }
}
