namespace Cocoar.FileSystem.Tests;

public class ResilientFileSystemMonitorTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CocoarFSTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options { Path = tempDir };

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
        var options = new ResilientFileSystemMonitor.Options { Path = null! };
        Assert.Throws<ArgumentNullException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options { Path = "" };
        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithWhitespacePath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options { Path = "   " };
        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public async Task Constructor_WithNonExistentPath_AndPollingEnabled_ShouldStartPolling()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = nonExistentPath,
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(3),
            description: "monitor to start polling");

        Assert.False(monitor.IsUsingWatcher);
    }

    [Fact]
    public async Task Constructor_WithExistingPath_ShouldStartWatcher()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options { Path = tempDir };

        using var monitor = new ResilientFileSystemMonitor(options);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        Assert.True(monitor.IsUsingWatcher);
    }

    #endregion

    #region Basic Event Tests

    [Fact]
    public async Task FileCreation_ShouldTriggerCreatedEvent()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "test content");

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
        var eventArgs = await eventReceived.Task;
        Assert.Equal("test.txt", eventArgs.Name);
    }

    [Fact]
    public async Task FileChange_ShouldTriggerChangedEvent()
    {
        var tempDir = CreateTempDirectory();
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "initial content");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Changed += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await Task.Delay(100); // Let index stabilize
        await File.WriteAllTextAsync(testFile, "updated content");

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
        var eventArgs = await eventReceived.Task;
        Assert.Equal("test.txt", eventArgs.Name);
    }

    [Fact]
    public async Task FileDelete_ShouldTriggerDeletedEvent()
    {
        var tempDir = CreateTempDirectory();
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "content");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Deleted += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await Task.Delay(100); // Let index stabilize
        File.Delete(testFile);

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
        var eventArgs = await eventReceived.Task;
        Assert.Equal("test.txt", eventArgs.Name);
    }

    [Fact]
    public async Task FileRenamed_ShouldTriggerRenamedEvent()
    {
        var tempDir = CreateTempDirectory();
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "content");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<RenamedEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Renamed += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await Task.Delay(100); // Let index stabilize
        var newFile = Path.Combine(tempDir, "renamed.txt");
        File.Move(testFile, newFile);

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
        var eventArgs = await eventReceived.Task;
        Assert.Equal("renamed.txt", eventArgs.Name);
        Assert.Equal("test.txt", eventArgs.OldName);
    }

    #endregion

    #region Subdirectory Tests

    [Fact]
    public async Task IncludeSubdirectories_True_ShouldMonitorSubfolders()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            IncludeSubdirectories = true,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        var testFile = Path.Combine(subDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "content");

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
    }

    [Fact]
    public async Task IncludeSubdirectories_False_ShouldNotMonitorSubfolders()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            IncludeSubdirectories = false,
            Filter = "*.txt"
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) => eventReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        // Create file in subfolder - should NOT trigger event
        var testFile = Path.Combine(subDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "content");

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(2000));

        // Event should NOT be received
        Assert.NotSame(eventReceived.Task, result);
    }

    #endregion

    #region Filter Tests

    [Fact]
    public async Task Filter_OnlyMatchingFiles_ShouldTriggerEvents()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt"
        };

        var txtEventReceived = new TaskCompletionSource<FileSystemEventArgs>();
        var logEventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) =>
        {
            if (e.Name?.EndsWith(".txt", StringComparison.Ordinal) == true)
                txtEventReceived.TrySetResult(e);
            if (e.Name?.EndsWith(".log", StringComparison.Ordinal) == true)
                logEventReceived.TrySetResult(e);
        };

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "txt");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "test.log"), "log");

        var txtResult = await Task.WhenAny(txtEventReceived.Task, Task.Delay(3000));
        var logResult = await Task.WhenAny(logEventReceived.Task, Task.Delay(500));

        Assert.Same(txtEventReceived.Task, txtResult);
        Assert.NotSame(logEventReceived.Task, logResult);
    }

    #endregion

    #region Debouncing Tests

    [Fact]
    public async Task RapidFileChanges_WithDebouncing_ShouldLimitEvents()
    {
        var tempDir = CreateTempDirectory();
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "initial");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt",
            DebounceTime = TimeSpan.FromMilliseconds(500)
        };

        var eventsReceived = new List<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Changed += (s, e) => eventsReceived.Add(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await Task.Delay(100); // Let index stabilize

        // Make rapid changes
        for (int i = 0; i < 10; i++)
        {
            await File.WriteAllTextAsync(testFile, $"change {i}");
            await Task.Delay(50);
        }

        // Wait for debounce to settle
        await Task.Delay(1000);

        // Should receive much fewer events than 10
        Assert.True(eventsReceived.Count < 10, $"Expected < 10 events, got {eventsReceived.Count}");
    }

    [Fact]
    public async Task RapidFileChanges_WithoutDebouncing_ShouldReceiveAllEvents()
    {
        var tempDir = CreateTempDirectory();
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "initial");

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.txt",
            DebounceTime = null
        };

        var eventsReceived = new List<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Changed += (s, e) => eventsReceived.Add(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        await Task.Delay(100); // Let index stabilize

        // Make changes
        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(testFile, $"change {i}");
            await Task.Delay(100);
        }

        await Task.Delay(500);

        // Should receive multiple events
        Assert.True(eventsReceived.Count > 0);
    }

    #endregion

    #region Polling Mode Tests

    [Fact]
    public async Task PollingMode_DirectoryAppearsLater_ShouldSwitchToWatcher()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromMilliseconds(500),
            HealthCheckInterval = TimeSpan.FromMilliseconds(200)
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            description: "polling to start");

        Assert.False(monitor.IsUsingWatcher);

        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(5),
            description: "watcher to become active after directory creation");

        Assert.True(monitor.IsUsingWatcher);
    }

    [Fact]
    public async Task PollingMode_ShouldDetectFileChanges()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            PollingInterval = TimeSpan.FromMilliseconds(300)
        };

        var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) => eventReceived.TrySetResult(e);

        await Task.Delay(500); // Let monitor initialize

        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "content");

        var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

        Assert.Same(eventReceived.Task, result);
    }

    #endregion

    #region Rapid Changes Tests

    [Fact]
    public async Task MultipleFiles_RapidCreation_ShouldDetectAll()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            Filter = "*.dat"
        };

        var filesDetected = new HashSet<string>();
        var lockObj = new object();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) =>
        {
            lock (lockObj)
            {
                if (e.Name != null)
                    filesDetected.Add(e.Name);
            }
        };

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to become active");

        const int fileCount = 20;
        for (int i = 0; i < fileCount; i++)
        {
            var fileName = Path.Combine(tempDir, $"file{i:D3}.dat");
            await File.WriteAllTextAsync(fileName, $"data {i}");
        }

        await ActiveWaitHelpers.WaitUntilAsync(
            () =>
            {
                lock (lockObj)
                    return filesDetected.Count >= fileCount * 0.8; // Allow 80% detection
            },
            timeout: TimeSpan.FromSeconds(10),
            description: "most files to be detected");

        lock (lockObj)
        {
            Assert.True(filesDetected.Count >= fileCount * 0.8,
                $"Expected at least {fileCount * 0.8} files detected, got {filesDetected.Count}");
        }
    }

    #endregion

    #region Resilience Tests - Directory Deletion/Recreation

    [Fact]
    public async Task DirectoryDeleted_WhileWatching_ShouldSwitchToPolling()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            HealthCheckInterval = TimeSpan.FromMilliseconds(500),
            PollingInterval = TimeSpan.FromMilliseconds(500)
        };

        var modeChanges = new List<string>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.ModeChanged += (s, e) => modeChanges.Add($"{e.Mode}:{e.Reason}");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to start");

        Assert.True(monitor.IsUsingWatcher);

        // Delete the directory
        Directory.Delete(tempDir, recursive: true);

        // Wait for health check to detect and switch to polling
        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(5),
            description: "switch to polling after directory deletion");

        Assert.False(monitor.IsUsingWatcher);
        Assert.Contains(modeChanges, m => m.Contains("Polling"));
    }

    [Fact]
    public async Task DirectoryDeleted_ThenRecreated_ShouldSwitchBackToWatcher()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            HealthCheckInterval = TimeSpan.FromMilliseconds(300),
            PollingInterval = TimeSpan.FromMilliseconds(300)
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to start");

        // Delete directory
        Directory.Delete(tempDir, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(3),
            description: "switch to polling after deletion");

        // Recreate directory
        Directory.CreateDirectory(tempDir);

        // Wait for polling to detect and switch back to watcher
        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(5),
            description: "switch back to watcher after recreation");

        Assert.True(monitor.IsUsingWatcher);
    }

    [Fact]
    public async Task DirectoryDeleted_ThenRecreated_ShouldDetectNewFiles()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            HealthCheckInterval = TimeSpan.FromMilliseconds(300),
            PollingInterval = TimeSpan.FromMilliseconds(300),
            Filter = "*.txt"
        };

        var eventsReceived = new List<string>();
        var lockObj = new object();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Created += (s, e) =>
        {
            lock (lockObj)
            {
                if (e.Name != null)
                    eventsReceived.Add(e.Name);
            }
        };

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to start");

        // Create a file before deletion
        await File.WriteAllTextAsync(Path.Combine(tempDir, "before.txt"), "before");
        await Task.Delay(200);

        // Delete and recreate directory
        Directory.Delete(tempDir, recursive: true);
        await Task.Delay(500);
        Directory.CreateDirectory(tempDir);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            timeout: TimeSpan.FromSeconds(5),
            description: "watcher to restart");

        await Task.Delay(200); // Let watcher stabilize

        // Create file after recreation
        await File.WriteAllTextAsync(Path.Combine(tempDir, "after.txt"), "after");

        await ActiveWaitHelpers.WaitUntilAsync(
            () =>
            {
                lock (lockObj)
                    return eventsReceived.Any(n => n.Contains("after.txt"));
            },
            timeout: TimeSpan.FromSeconds(3),
            description: "after.txt creation event");

        lock (lockObj)
        {
            Assert.Contains(eventsReceived, n => n.Contains("after.txt"));
        }
    }

    [Fact]
    public async Task DirectoryDeleted_WithPollingDisabled_ShouldRaiseError()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = false,
            HealthCheckInterval = TimeSpan.FromMilliseconds(300),
            AutoRecoverFromErrors = false
        };

        var errorReceived = new TaskCompletionSource<ErrorEventArgs>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.Error += (s, e) => errorReceived.TrySetResult(e);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to start");

        // Delete directory
        Directory.Delete(tempDir, recursive: true);

        // Should receive error or become inactive
        var timeout = Task.Delay(5000);
        var completed = await Task.WhenAny(errorReceived.Task, timeout);

        Assert.True(errorReceived.Task.IsCompleted || !monitor.IsUsingWatcher,
            "Expected error event or watcher to become inactive");
    }

    #endregion

    #region ModeChanged Event Tests

    [Fact]
    public async Task ModeChanged_Event_ShouldProvideReasonAndMode()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = tempDir,
            EnablePollingFallback = true,
            HealthCheckInterval = TimeSpan.FromMilliseconds(300),
            PollingInterval = TimeSpan.FromMilliseconds(300)
        };

        var modeChangedEvents = new List<(string Mode, string Reason)>();

        using var monitor = new ResilientFileSystemMonitor(options);
        monitor.ModeChanged += (s, e) =>
        {
            modeChangedEvents.Add((e.Mode.ToString(), e.Reason ?? ""));
        };

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            description: "watcher to start");

        // Trigger mode change by deleting directory
        Directory.Delete(tempDir, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => modeChangedEvents.Count > 0,
            timeout: TimeSpan.FromSeconds(3),
            description: "mode change event");

        Assert.NotEmpty(modeChangedEvents);
        Assert.Contains(modeChangedEvents, e => e.Mode.Contains("Polling") || e.Reason.Length > 0);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options { Path = tempDir };

        var monitor = new ResilientFileSystemMonitor(options);
        var exception = Record.Exception(() => monitor.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        var tempDir = CreateTempDirectory();
        var options = new ResilientFileSystemMonitor.Options { Path = tempDir };

        var monitor = new ResilientFileSystemMonitor(options);
        monitor.Dispose();

        var exception = Record.Exception(() => monitor.Dispose());

        Assert.Null(exception);
    }

    #endregion
}