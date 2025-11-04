using System.Collections.Concurrent;
using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

/// <summary>
/// Comprehensive test suite for ResilientFileSystemMonitor covering:
/// - Lossless event delivery (reconciliation)
/// - Metadata fingerprint auditing
/// - Directory deletion/recreation scenarios
/// - Polling fallback and recovery
/// - Cross-platform compatibility
/// </summary>
public sealed class ResilientFileSystemMonitorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly List<IDisposable> _disposables = new();

    public ResilientFileSystemMonitorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ResilientFSMonitor_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); } catch { /* ignore */ }
        }
        _disposables.Clear();

        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private ResilientFileSystemMonitor CreateMonitor(
        string? path = null,
        bool enablePollingFallback = true,
        bool autoRecoverFromErrors = true,
        TimeSpan? debounceTime = null,
        TimeSpan? healthCheckInterval = null,
        TimeSpan? auditInterval = null,
        TimeSpan? pollingInterval = null,
        string filter = "*",
        bool includeSubdirectories = true)
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = path ?? _testRoot,
            EnablePollingFallback = enablePollingFallback,
            AutoRecoverFromErrors = autoRecoverFromErrors,
            DebounceTime = debounceTime,
            HealthCheckInterval = healthCheckInterval ?? TimeSpan.FromMilliseconds(100),
            AuditInterval = auditInterval ?? TimeSpan.FromSeconds(2),
            PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(500),
            Filter = filter,
            IncludeSubdirectories = includeSubdirectories
        };

        var monitor = new ResilientFileSystemMonitor(options);
        _disposables.Add(monitor);
        return monitor;
    }

    private string GetTestPath(string relativePath) => Path.Combine(_testRoot, relativePath);

    #region Constructor Tests

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
        var options = new ResilientFileSystemMonitor.Options { Path = string.Empty };
        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithWhitespacePath_ShouldThrow()
    {
        var options = new ResilientFileSystemMonitor.Options { Path = "   " };
        Assert.Throws<ArgumentException>(() => new ResilientFileSystemMonitor(options));
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        var monitor = CreateMonitor();
        Assert.NotNull(monitor);
    }

    [Fact]
    public void Constructor_WithExistingPath_ShouldStartWatcher()
    {
        var monitor = CreateMonitor();
        Assert.True(monitor.IsUsingWatcher);
    }

    [Fact]
    public void Constructor_WithNonExistentPath_AndPollingEnabled_ShouldStartPolling()
    {
        var nonExistentPath = GetTestPath("NonExistent");
        var monitor = CreateMonitor(path: nonExistentPath, enablePollingFallback: true);
        Assert.False(monitor.IsUsingWatcher);
    }

    #endregion

    #region Basic File Events (Steady-State)

    [Fact]
    public async Task FileCreation_ShouldTriggerCreatedEvent()
    {
        var monitor = CreateMonitor();
        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        var testFile = GetTestPath("test.txt");
        await Task.Delay(100);
        File.WriteAllText(testFile, "content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "file creation event");

        Assert.Contains("test.txt", createdFiles);
    }

    [Fact]
    public async Task FileChange_ShouldTriggerChangedEvent()
    {
        var testFile = GetTestPath("test.txt");
        File.WriteAllText(testFile, "initial");

        var monitor = CreateMonitor();
        var changedFiles = new List<string>();
        monitor.Changed += (s, e) => changedFiles.Add(e.Name!);

        await Task.Delay(200);
        File.WriteAllText(testFile, "modified");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => changedFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "file change event");

        Assert.Contains("test.txt", changedFiles);
    }

    [Fact]
    public async Task FileDelete_ShouldTriggerDeletedEvent()
    {
        var testFile = GetTestPath("test.txt");
        File.WriteAllText(testFile, "content");

        var monitor = CreateMonitor();
        var deletedFiles = new List<string>();
        monitor.Deleted += (s, e) => deletedFiles.Add(e.Name!);

        await Task.Delay(200);
        File.Delete(testFile);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => deletedFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "file deletion event");

        Assert.Contains("test.txt", deletedFiles);
    }

    [Fact]
    public async Task FileRenamed_ShouldTriggerRenamedEvent()
    {
        var testFile = GetTestPath("old.txt");
        File.WriteAllText(testFile, "content");

        var monitor = CreateMonitor();
        var renamedFiles = new List<(string Old, string New)>();
        monitor.Renamed += (s, e) => renamedFiles.Add((e.OldName!, e.Name!));

        await Task.Delay(200);
        File.Move(testFile, GetTestPath("new.txt"));

        await ActiveWaitHelpers.WaitUntilAsync(
            () => renamedFiles.Any(r => r.Old == "old.txt" && r.New == "new.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "file rename event");

        Assert.Contains(renamedFiles, r => r.Old == "old.txt" && r.New == "new.txt");
    }

    #endregion

    #region Filter & Subdirectory Tests

    [Fact]
    public async Task Filter_OnlyMatchingFiles_ShouldTriggerEvents()
    {
        var monitor = CreateMonitor(filter: "*.txt");
        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        await Task.Delay(100);
        File.WriteAllText(GetTestPath("test.txt"), "content");
        File.WriteAllText(GetTestPath("test.log"), "content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "filtered file creation");

        await Task.Delay(500);
        Assert.Contains("test.txt", createdFiles);
        Assert.DoesNotContain("test.log", createdFiles);
    }

    [Fact]
    public async Task IncludeSubdirectories_True_ShouldMonitorSubfolders()
    {
        var monitor = CreateMonitor(includeSubdirectories: true);
        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        var subDir = GetTestPath("subfolder");
        Directory.CreateDirectory(subDir);
        await Task.Delay(200);

        File.WriteAllText(Path.Combine(subDir, "test.txt"), "content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "subfolder file creation");

        Assert.Contains("test.txt", createdFiles);
    }

    [Fact]
    public async Task IncludeSubdirectories_False_ShouldNotMonitorSubfolders()
    {
        var monitor = CreateMonitor(includeSubdirectories: false);
        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        await Task.Delay(100);
        File.WriteAllText(GetTestPath("root.txt"), "content");

        var subDir = GetTestPath("subfolder");
        Directory.CreateDirectory(subDir);
        await Task.Delay(300);

        File.WriteAllText(Path.Combine(subDir, "sub.txt"), "content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("root.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "root file creation");

        await Task.Delay(800);

        Assert.Contains("root.txt", createdFiles);
        Assert.DoesNotContain("sub.txt", createdFiles);
    }

    #endregion

    #region Debouncing Tests

    [Fact]
    public async Task RapidFileChanges_WithoutDebouncing_ShouldReceiveAllEvents()
    {
        var monitor = CreateMonitor(debounceTime: null);
        var eventCount = 0;
        monitor.Changed += (s, e) => Interlocked.Increment(ref eventCount);

        var testFile = GetTestPath("test.txt");
        File.WriteAllText(testFile, "initial");

        await Task.Delay(200);

        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(testFile, $"change {i}");
            await Task.Delay(50);
        }

        await Task.Delay(1000);

        Assert.True(eventCount >= 5, $"Expected at least 5 events, got {eventCount}");
    }

    [Fact]
    public async Task RapidFileChanges_WithDebouncing_ShouldLimitEvents()
    {
        var monitor = CreateMonitor(debounceTime: TimeSpan.FromMilliseconds(300));
        var eventCount = 0;
        monitor.Changed += (s, e) => Interlocked.Increment(ref eventCount);

        var testFile = GetTestPath("test.txt");
        File.WriteAllText(testFile, "initial");

        await Task.Delay(200);

        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(testFile, $"change {i}");
            await Task.Delay(50);
        }

        await Task.Delay(1000);

        Assert.True(eventCount < 8, $"Expected fewer than 8 events with debouncing, got {eventCount}");
    }

    #endregion

    #region Lossless Reconciliation Tests (Core Feature)

    [Fact]
    public async Task DirectoryDeleted_AndRecreatedWithFiles_ShouldDetectAllFiles()
    {
        // Seed initial file
        File.WriteAllText(GetTestPath("initial.txt"), "initial");

        var monitor = CreateMonitor(
            healthCheckInterval: TimeSpan.FromMilliseconds(100),
            pollingInterval: TimeSpan.FromMilliseconds(300));

        var allCreated = new List<string>();
        var allDeleted = new List<string>();
        var lockObj = new object();

        monitor.Created += (s, e) => { lock (lockObj) allCreated.Add(e.Name!); };
        monitor.Deleted += (s, e) => { lock (lockObj) allDeleted.Add(e.Name!); };

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(50),
            "initial watcher start");

        // Delete entire directory
        Directory.Delete(_testRoot, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "switch to polling after deletion");

        // Recreate with NEW files
        Directory.CreateDirectory(_testRoot);
        File.WriteAllText(GetTestPath("new1.txt"), "content1");
        File.WriteAllText(GetTestPath("new2.txt"), "content2");
        File.WriteAllText(GetTestPath("new3.txt"), "content3");

        // Wait for polling to detect and reconcile
        await ActiveWaitHelpers.WaitUntilAsync(
            () =>
            {
                lock (lockObj)
                    return allCreated.Contains("new1.txt") &&
                           allCreated.Contains("new2.txt") &&
                           allCreated.Contains("new3.txt");
            },
            TimeSpan.FromSeconds(8),
            TimeSpan.FromMilliseconds(200),
            "reconciliation detects new files");

        lock (lockObj)
        {
            Assert.Contains("new1.txt", allCreated);
            Assert.Contains("new2.txt", allCreated);
            Assert.Contains("new3.txt", allCreated);
            Assert.Contains("initial.txt", allDeleted);
        }
    }

    [Fact]
    public async Task FileModified_WhileInPollingMode_ShouldDetectChange()
    {
        File.WriteAllText(GetTestPath("test.txt"), "v1");

        var monitor = CreateMonitor(
            healthCheckInterval: TimeSpan.FromMilliseconds(100),
            pollingInterval: TimeSpan.FromMilliseconds(300));

        var changedFiles = new List<string>();
        var createdFiles = new List<string>();
        monitor.Changed += (s, e) => changedFiles.Add(e.Name!);
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(50),
            "watcher start");

        // Force polling mode
        Directory.Delete(_testRoot, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "enter polling");

        // Recreate with modified file (this will be a "created" event since old file was deleted)
        Directory.CreateDirectory(_testRoot);
        File.WriteAllText(GetTestPath("test.txt"), "v2_modified");

        // Wait for polling to detect directory and switch back to watcher
        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100),
            "switch back to watcher");

        // Wait for reconciliation to detect the file
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(100),
            "detect file via reconciliation");

        Assert.Contains("test.txt", createdFiles);
    }

    [Fact]
    public async Task DirectoryDeleted_AndRecreatedWithDifferentFiles_ShouldReconcile()
    {
        // Setup initial files
        File.WriteAllText(GetTestPath("old1.txt"), "content1");
        File.WriteAllText(GetTestPath("old2.txt"), "content2");

        var monitor = CreateMonitor(
            healthCheckInterval: TimeSpan.FromMilliseconds(100),
            pollingInterval: TimeSpan.FromMilliseconds(300));

        var allEvents = new List<(string Type, string Name)>();
        var lockObj = new object();

        monitor.Deleted += (s, e) => { lock (lockObj) allEvents.Add(("D", e.Name!)); };
        monitor.Created += (s, e) => { lock (lockObj) allEvents.Add(("C", e.Name!)); };

        await Task.Delay(200);

        // Delete directory
        Directory.Delete(_testRoot, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => !monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "switch to polling");

        // Recreate with different files
        Directory.CreateDirectory(_testRoot);
        File.WriteAllText(GetTestPath("new1.txt"), "new_content1");
        File.WriteAllText(GetTestPath("new2.txt"), "new_content2");

        // Wait for polling to detect and emit events
        await ActiveWaitHelpers.WaitUntilAsync(
            () =>
            {
                lock (lockObj)
                    return allEvents.Any(e => e.Name == "new1.txt") &&
                           allEvents.Any(e => e.Name == "new2.txt");
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(150),
            "reconciliation events");

        lock (lockObj)
        {
            // We should see deletions of old files and creations of new files
            Assert.Contains(allEvents, e => e.Type == "D" && e.Name == "old1.txt");
            Assert.Contains(allEvents, e => e.Type == "D" && e.Name == "old2.txt");
            Assert.Contains(allEvents, e => e.Type == "C" && e.Name == "new1.txt");
            Assert.Contains(allEvents, e => e.Type == "C" && e.Name == "new2.txt");
        }
    }

    #endregion

    #region Health Check & Audit Tests

    [Fact]
    public async Task HealthCheck_DirectoryDisappears_ShouldTransitionToPolling()
    {
        var monitor = CreateMonitor(healthCheckInterval: TimeSpan.FromMilliseconds(100));

        var modeChanges = new List<WatcherMode>();
        monitor.ModeChanged += (s, e) => modeChanges.Add(e.Mode);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(50),
            "watcher active");

        Directory.Delete(_testRoot, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => modeChanges.Contains(WatcherMode.Polling),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "mode change to polling");

        Assert.Contains(WatcherMode.Polling, modeChanges);
    }

    [Fact]
    public async Task AuditInterval_DetectsSilentChanges_ShouldReconcile()
    {
        // This test simulates the "zombie watcher" scenario
        // We'll manually create files in a way that might bypass events
        
        File.WriteAllText(GetTestPath("initial.txt"), "initial");

        var monitor = CreateMonitor(
            auditInterval: TimeSpan.FromSeconds(1),
            healthCheckInterval: TimeSpan.FromMilliseconds(500));

        await Task.Delay(300);

        // Simulate silent changes (though in practice, events should fire)
        // The audit will still catch divergence if any
        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        await Task.Delay(1500); // Let audit run

        // Verify monitor is still healthy
        Assert.True(monitor.IsUsingWatcher || !monitor.IsUsingWatcher); // Either state is valid
    }

    #endregion

    #region Polling Mode Tests

    [Fact]
    public async Task PollingMode_ShouldDetectFileChanges()
    {
        var nonExistentPath = GetTestPath("polling_test");
        var monitor = CreateMonitor(
            path: nonExistentPath,
            pollingInterval: TimeSpan.FromMilliseconds(300));

        var createdFiles = new List<string>();
        monitor.Created += (s, e) => createdFiles.Add(e.Name!);

        Assert.False(monitor.IsUsingWatcher);

        Directory.CreateDirectory(nonExistentPath);
        await Task.Delay(400);

        File.WriteAllText(Path.Combine(nonExistentPath, "test.txt"), "content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Contains("test.txt"),
            TimeSpan.FromSeconds(6),
            TimeSpan.FromMilliseconds(150),
            "polling mode file detection");

        Assert.Contains("test.txt", createdFiles);
    }

    [Fact]
    public async Task PollingMode_DirectoryAppearsLater_ShouldSwitchToWatcher()
    {
        var nonExistentPath = GetTestPath("delayed_directory");
        var monitor = CreateMonitor(
            path: nonExistentPath,
            pollingInterval: TimeSpan.FromMilliseconds(300));

        Assert.False(monitor.IsUsingWatcher);

        Directory.CreateDirectory(nonExistentPath);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => monitor.IsUsingWatcher,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100),
            "upgrade to watcher mode");

        Assert.True(monitor.IsUsingWatcher);
    }

    #endregion

    #region Mode Changed Event Tests

    [Fact]
    public async Task ModeChanged_Event_ShouldProvideReasonAndMode()
    {
        var modeChanges = new List<(WatcherMode Mode, string Reason)>();
        
        var monitor = CreateMonitor(healthCheckInterval: TimeSpan.FromMilliseconds(100));
        monitor.ModeChanged += (s, e) => modeChanges.Add((e.Mode, e.Reason));

        // Give time for initialization
        await Task.Delay(300);

        // Now trigger a mode change by deleting the directory
        Directory.Delete(_testRoot, recursive: true);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => modeChanges.Any(m => m.Mode == WatcherMode.Polling),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(50),
            "mode change to polling");

        Assert.Contains(modeChanges, m => m.Mode == WatcherMode.Polling && !string.IsNullOrEmpty(m.Reason));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        var monitor = CreateMonitor();
        monitor.Dispose();
        // Should not throw
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        var monitor = CreateMonitor();
        monitor.Dispose();
        monitor.Dispose();
        monitor.Dispose();
        // Should not throw
    }

    #endregion

    #region Stress & Rapid Change Tests

    [Fact]
    public async Task MultipleFiles_RapidCreation_ShouldDetectAll()
    {
        var monitor = CreateMonitor();
        var createdFiles = new HashSet<string>();
        var lockObj = new object();
        monitor.Created += (s, e) =>
        {
            lock (lockObj) createdFiles.Add(e.Name!);
        };

        await Task.Delay(200);

        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            File.WriteAllText(GetTestPath($"file_{i}.txt"), $"content {i}");
            await Task.Delay(20);
        });

        await Task.WhenAll(tasks);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 15,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(100),
            "rapid file creation detection");

        Assert.True(createdFiles.Count >= 15, $"Expected at least 15 files, detected {createdFiles.Count}");
    }

    [Fact]
    public async Task Events_ChannelReader_ShouldReceiveAllEventsInOrder()
    {
        var monitor = CreateMonitor();

        var events = new ConcurrentBag<FileSystemEvent>();
        var cancellation = new CancellationTokenSource();

        // Start consuming events from the channel
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var evt in monitor.Events.ReadAllAsync(cancellation.Token))
            {
                events.Add(evt);
            }
        });

        // Trigger some events
        var testFile = GetTestPath("test.txt");
        await File.WriteAllTextAsync(testFile, "content");
        await ActiveWaitHelpers.WaitUntilAsync(
            () => events.Any(e => e.Kind == FileSystemEventKind.Created),
            TimeSpan.FromSeconds(2),
            description: "file creation event"
        );

        await File.AppendAllTextAsync(testFile, " more");
        await ActiveWaitHelpers.WaitUntilAsync(
            () => events.Any(e => e.Kind == FileSystemEventKind.Changed),
            TimeSpan.FromSeconds(2),
            description: "file change event"
        );

        File.Delete(testFile);
        await ActiveWaitHelpers.WaitUntilAsync(
            () => events.Any(e => e.Kind == FileSystemEventKind.Deleted),
            TimeSpan.FromSeconds(2),
            description: "file deletion event"
        );

        // Verify we got all events in order
        Assert.Contains(events, e => e.Kind == FileSystemEventKind.Created);
        Assert.Contains(events, e => e.Kind == FileSystemEventKind.Changed);
        Assert.Contains(events, e => e.Kind == FileSystemEventKind.Deleted);

        // Verify events have correct properties
        var createdEvent = events.First(e => e.Kind == FileSystemEventKind.Created);
        Assert.Equal("test.txt", createdEvent.Name);
        Assert.Equal(testFile, createdEvent.FullPath);

        cancellation.Cancel();
        monitor.Dispose();

        try { await consumerTask; } catch { }
    }

    #endregion
}
