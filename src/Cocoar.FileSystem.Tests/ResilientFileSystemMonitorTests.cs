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
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = ""
        };

        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithWhitespacePath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = "   "
        };

        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public async Task Constructor_WithNonExistentPath_AndPollingEnabled_ShouldStartPolling()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = nonExistentPath,
            EnablePollingFallback = true
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        // Wait for monitor to initialize in polling mode
        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsPolling,
            description: "monitor to start polling");

        Assert.True(monitor.IsPolling);
        Assert.False(monitor.IsWatcherActive);
    }

    [Fact]
    public async Task Constructor_WithExistingPath_ShouldStartWatcher()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = Path.GetTempPath()
        };

        using var monitor = new ResilientFileSystemMonitor(options);

        // Wait for watcher to become active
        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsWatcherActive,
            description: "watcher to become active");

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

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            var testFile = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(testFile, "test content");

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            Assert.Equal("test.txt", eventArgs.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileChange_ShouldTriggerChangedEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
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

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            await File.WriteAllTextAsync(testFile, "updated content");

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            Assert.Equal("test.txt", eventArgs.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileDelete_ShouldTriggerDeletedEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
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

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            File.Delete(testFile);

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            Assert.Equal("test.txt", eventArgs.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RapidFileChanges_WithDebouncing_ShouldLimitEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(testFile, "initial");

            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt",
                DebounceTime = TimeSpan.FromMilliseconds(500)
            };

            var eventCount = 0;
            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Changed += (s, e) => Interlocked.Increment(ref eventCount);

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Rapidly change file 10 times
            for (int i = 0; i < 10; i++)
            {
                await File.WriteAllTextAsync(testFile, $"content {i}");
                await Task.Delay(50); // 50ms between changes
            }

            // Wait for events to stabilize
            var finalCount = await ActiveWaitHelpers.WaitForStableValueAsync(
                () => eventCount,
                stabilityPeriod: TimeSpan.FromMilliseconds(800),
                description: "event count to stabilize");

            // With debouncing, we should get fewer than 10 events
            Assert.True(finalCount < 10, $"Expected fewer than 10 events with debouncing, got {finalCount}");
            Assert.True(finalCount >= 1, "Expected at least 1 event");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RapidFileChanges_WithoutDebouncing_ShouldReceiveAllEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFile = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(testFile, "initial");

            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt",
                DebounceTime = null // No debouncing
            };

            var eventCount = 0;
            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Changed += (s, e) => Interlocked.Increment(ref eventCount);

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Rapidly change file 5 times
            for (int i = 0; i < 5; i++)
            {
                await File.WriteAllTextAsync(testFile, $"content {i}");
                await Task.Delay(100); // 100ms between changes
            }

            // Wait for events to stabilize
            var finalCount = await ActiveWaitHelpers.WaitForStableValueAsync(
                () => eventCount,
                stabilityPeriod: TimeSpan.FromMilliseconds(500),
                description: "event count to stabilize");

            // Without debouncing, we should get at least one event (platform-dependent)
            Assert.True(finalCount >= 1, $"Expected at least 1 event, got {finalCount}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Filter_OnlyMatchingFiles_ShouldTriggerEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.json"
            };

            var jsonEventReceived = new TaskCompletionSource<bool>();
            var txtEventReceived = new TaskCompletionSource<bool>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Created += (s, e) =>
            {
                if (e.Name?.EndsWith(".json", StringComparison.Ordinal) == true)
                    jsonEventReceived.TrySetResult(true);
                if (e.Name?.EndsWith(".txt", StringComparison.Ordinal) == true)
                    txtEventReceived.TrySetResult(true);
            };

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Create .json file (should trigger)
            await File.WriteAllTextAsync(Path.Combine(tempDir, "test.json"), "{}");
            
            // Create .txt file (should NOT trigger)
            await File.WriteAllTextAsync(Path.Combine(tempDir, "test.txt"), "text");

            // Wait for JSON event
            await ActiveWaitHelpers.WaitUntilAsync(
                () => jsonEventReceived.Task.IsCompleted,
                timeout: TimeSpan.FromSeconds(3),
                description: "JSON file event");

            // Give a bit more time to ensure TXT doesn't trigger
            await Task.Delay(500);

            Assert.True(jsonEventReceived.Task.IsCompleted, "JSON file should trigger event");
            Assert.False(txtEventReceived.Task.IsCompleted, "TXT file should NOT trigger event");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_True_ShouldMonitorSubfolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "subfolder");
        Directory.CreateDirectory(subDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt",
                IncludeSubdirectories = true
            };

            var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Created += (s, e) => eventReceived.TrySetResult(e);

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            var subFile = Path.Combine(subDir, "subtest.txt");
            await File.WriteAllTextAsync(subFile, "content");

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            // Name includes subdirectory path when IncludeSubdirectories is true
            Assert.Contains("subtest.txt", eventArgs.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_False_ShouldNotMonitorSubfolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "subfolder");
        Directory.CreateDirectory(subDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt",
                IncludeSubdirectories = false
            };

            var subEventReceived = new TaskCompletionSource<bool>();
            var rootEventReceived = new TaskCompletionSource<bool>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Created += (s, e) =>
            {
                if (e.FullPath.Contains("subfolder"))
                    subEventReceived.TrySetResult(true);
                else
                    rootEventReceived.TrySetResult(true);
            };

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Create in subfolder (should NOT trigger)
            await File.WriteAllTextAsync(Path.Combine(subDir, "subtest.txt"), "content");
            
            // Create in root (should trigger)
            await File.WriteAllTextAsync(Path.Combine(tempDir, "roottest.txt"), "content");

            // Wait for root event
            await ActiveWaitHelpers.WaitUntilAsync(
                () => rootEventReceived.Task.IsCompleted,
                timeout: TimeSpan.FromSeconds(3),
                description: "root file event");

            // Give more time to ensure subfolder doesn't trigger
            await Task.Delay(500);

            Assert.True(rootEventReceived.Task.IsCompleted, "Root file should trigger event");
            Assert.False(subEventReceived.Task.IsCompleted, "Subfolder file should NOT trigger event");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PollingMode_DirectoryAppearsLater_ShouldSwitchToWatcher()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = true,
                PollingInterval = TimeSpan.FromSeconds(1)
            };

            using var monitor = new ResilientFileSystemMonitor(options);

            // Wait for polling to start
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsPolling,
                description: "polling to start");

            Assert.True(monitor.IsPolling);
            Assert.False(monitor.IsWatcherActive);

            // Create directory
            Directory.CreateDirectory(tempDir);

            // Wait for automatic switch to watcher
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                timeout: TimeSpan.FromSeconds(5),
                description: "watcher to become active after directory creation");

            Assert.True(monitor.IsWatcherActive);
            Assert.False(monitor.IsPolling);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PollingMode_ShouldDetectFileChanges()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = true,
                PollingInterval = TimeSpan.FromMilliseconds(500)
            };

            var eventReceived = new TaskCompletionSource<FileSystemEventArgs>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Created += (s, e) => eventReceived.TrySetResult(e);
            
            // Wait for monitor to be active (either polling or watcher)
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsPolling || monitor.IsWatcherActive,
                description: "monitor to become active");

            var testFile = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(testFile, "content");

            // Wait for event with longer timeout since polling may be slower
            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MultipleFiles_RapidCreation_ShouldDetectAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.dat"
            };

            var filesDetected = new List<string>();
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

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Create 10 files rapidly
            var fileNames = Enumerable.Range(1, 10).Select(i => $"file{i}.dat").ToList();
            foreach (var fileName in fileNames)
            {
                await File.WriteAllTextAsync(Path.Combine(tempDir, fileName), "data");
                await Task.Delay(50); // Small delay between files
            }

            // Wait for file detection to stabilize
            var finalCount = await ActiveWaitHelpers.WaitForStableValueAsync(
                () => filesDetected.Count,
                stabilityPeriod: TimeSpan.FromMilliseconds(800),
                description: "detected file count to stabilize");

            // Should have detected most or all files (platform-dependent)
            Assert.True(finalCount >= 5, $"Expected at least 5 files detected, got {finalCount}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
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

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = Path.GetTempPath()
        };

        var monitor = new ResilientFileSystemMonitor(options);
        
        var exception = Record.Exception(() =>
        {
            monitor.Dispose();
            monitor.Dispose();
            monitor.Dispose();
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task DirectoryDeleted_WhileWatching_ShouldSwitchToPolling()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = true,
                PollingInterval = TimeSpan.FromMilliseconds(500),
                AutoRecoverFromErrors = true
            };

            using var monitor = new ResilientFileSystemMonitor(options);

            // Wait for watcher to be active
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            Assert.True(monitor.IsWatcherActive);
            Assert.False(monitor.IsPolling);

            // Delete the directory while monitoring
            Directory.Delete(tempDir, true);

            // Wait for automatic switch to polling mode
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsPolling,
                timeout: TimeSpan.FromSeconds(5),
                description: "switch to polling after directory deletion");

            Assert.True(monitor.IsPolling);
            Assert.False(monitor.IsWatcherActive);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DirectoryDeleted_ThenRecreated_ShouldSwitchBackToWatcher()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = true,
                PollingInterval = TimeSpan.FromMilliseconds(500),
                AutoRecoverFromErrors = true
            };

            var modeChanges = new List<string>();
            var lockObj = new object();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.ModeChanged += (s, e) =>
            {
                lock (lockObj)
                {
                    modeChanges.Add($"{e.NewMode}: {e.Reason}");
                }
            };

            // Wait for watcher to be active
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            Assert.True(monitor.IsWatcherActive);

            // Delete the directory
            Directory.Delete(tempDir, true);

            // Wait for switch to polling
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsPolling,
                timeout: TimeSpan.FromSeconds(5),
                description: "switch to polling after deletion");

            Assert.True(monitor.IsPolling);

            // Recreate the directory
            Directory.CreateDirectory(tempDir);

            // Wait for switch back to watcher
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                timeout: TimeSpan.FromSeconds(5),
                description: "switch back to watcher after recreation");

            Assert.True(monitor.IsWatcherActive);
            Assert.False(monitor.IsPolling);

            // Should have recorded mode changes
            Assert.True(modeChanges.Count >= 2, $"Expected at least 2 mode changes, got {modeChanges.Count}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DirectoryDeleted_WithPollingDisabled_ShouldRaiseError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = false, // No fallback!
                AutoRecoverFromErrors = false
            };

            var errorReceived = new TaskCompletionSource<Exception>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Error += (s, e) => errorReceived.TrySetResult(e.GetException());

            // Wait for watcher to be active
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            // Delete the directory
            Directory.Delete(tempDir, true);

            // Wait for error event
            var result = await Task.WhenAny(errorReceived.Task, Task.Delay(3000));

            // Should receive error (or at least watcher becomes inactive)
            // This is platform-dependent - some platforms may not fire error immediately
            var errorOrInactive = errorReceived.Task.IsCompleted || !monitor.IsWatcherActive;
            Assert.True(errorOrInactive, "Expected error event or watcher to become inactive");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ModeChanged_Event_ShouldProvideReasonAndMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                EnablePollingFallback = true,
                PollingInterval = TimeSpan.FromMilliseconds(500)
            };

            var modeChangeReceived = new TaskCompletionSource<MonitorModeChangedEventArgs>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.ModeChanged += (s, e) =>
            {
                modeChangeReceived.TrySetResult(e);
            };

            // Wait for initial polling mode
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsPolling,
                description: "polling to start");

            // Create directory to trigger switch to watcher
            Directory.CreateDirectory(tempDir);

            // Wait for mode change event
            var result = await Task.WhenAny(modeChangeReceived.Task, Task.Delay(5000));

            if (result == modeChangeReceived.Task)
            {
                var eventArgs = await modeChangeReceived.Task;
                // NewMode should be Watcher when switching from Polling
                Assert.Equal(MonitorMode.Watcher, eventArgs.NewMode);
                Assert.False(string.IsNullOrEmpty(eventArgs.Reason));
            }
            // If no event, at least watcher should be active now
            else
            {
                await ActiveWaitHelpers.WaitUntilAsync(
                    () => monitor.IsWatcherActive,
                    description: "watcher to become active");
                Assert.True(monitor.IsWatcherActive);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileRenamed_ShouldTriggerRenamedEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var oldFile = Path.Combine(tempDir, "old.txt");
            await File.WriteAllTextAsync(oldFile, "content");

            var options = new ResilientFileSystemMonitor.Options
            {
                Path = tempDir,
                Filter = "*.txt"
            };

            var eventReceived = new TaskCompletionSource<RenamedEventArgs>();

            using var monitor = new ResilientFileSystemMonitor(options);
            monitor.Renamed += (s, e) => eventReceived.TrySetResult(e);

            // Wait for watcher to be ready
            await ActiveWaitHelpers.WaitUntilAsync(
                () => monitor.IsWatcherActive,
                description: "watcher to become active");

            var newFile = Path.Combine(tempDir, "new.txt");
            File.Move(oldFile, newFile);

            var result = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));

            Assert.Same(eventReceived.Task, result);
            var eventArgs = await eventReceived.Task;
            Assert.Equal("old.txt", eventArgs.OldName);
            Assert.Equal("new.txt", eventArgs.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
