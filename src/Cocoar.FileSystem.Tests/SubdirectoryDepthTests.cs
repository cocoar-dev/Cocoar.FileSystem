using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

/// <summary>
/// Tests for subdirectory depth limiting functionality.
/// </summary>
public sealed class SubdirectoryDepthTests : IDisposable
{
    private readonly string _testRoot;
    private readonly List<IDisposable> _disposables = new();

    public SubdirectoryDepthTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"CocoarFSTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
        
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private ResilientFileSystemMonitor CreateMonitor(int? maxDepth = null, bool? includeSubdirectories = null)
    {
        var options = new ResilientFileSystemMonitor.Options
        {
            Path = _testRoot,
            HealthCheckInterval = TimeSpan.FromMilliseconds(100),
            AuditInterval = TimeSpan.FromSeconds(2),
            PollingInterval = TimeSpan.FromMilliseconds(500),
            Filter = "*.txt",
            IncludeSubdirectories = includeSubdirectories ?? (maxDepth.HasValue && maxDepth.Value != 0),
            MaxDepth = maxDepth ?? 0
        };

        var monitor = new ResilientFileSystemMonitor(options);
        _disposables.Add(monitor);
        return monitor;
    }

    [Fact]
    public async Task DefaultBehavior_ShouldBeNonRecursive()
    {
        // Arrange
        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .OnCreated((s, e) => { })
            .Build();
        _disposables.Add(monitor);

        var subDir = Path.Combine(_testRoot, "subdir");
        Directory.CreateDirectory(subDir);
        await Task.Delay(200);

        var createdFiles = new List<string>();
        var lockObj = new object();
        monitor.Created += (s, e) => { lock (lockObj) createdFiles.Add(e.Name!); };

        // Act - Create files in root and subdirectory
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");
        await Task.Delay(500);

        // Assert - Only root file should be detected
        lock (lockObj)
        {
            Assert.Single(createdFiles);
            Assert.Contains("root.txt", createdFiles);
            Assert.DoesNotContain("nested.txt", createdFiles);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_Parameterless_ShouldEnableUnlimited()
    {
        // Arrange
        var createdFiles = new List<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories() // No parameters = unlimited
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");
        Directory.CreateDirectory(level3);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2");
        File.WriteAllText(Path.Combine(level3, "l3.txt"), "l3");
        await Task.Delay(500);

        // Assert - All files should be detected
        lock (lockObj)
        {
            Assert.Equal(4, createdFiles.Count);
            Assert.Contains("root.txt", createdFiles);
            Assert.Contains("l1.txt", createdFiles);
            Assert.Contains("l2.txt", createdFiles);
            Assert.Contains("l3.txt", createdFiles);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_True_ShouldEnableUnlimited()
    {
        // Arrange
        var createdFiles = new List<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(true)
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2");
        await Task.Delay(500);

        // Assert
        lock (lockObj)
        {
            Assert.Equal(3, createdFiles.Count);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_False_ShouldDisableRecursion()
    {
        // Arrange
        var createdFiles = new HashSet<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(false)
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var subDir = Path.Combine(_testRoot, "subdir");
        Directory.CreateDirectory(subDir);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");
        await Task.Delay(500);

        // Assert - Only root file (use HashSet to handle macOS duplicate events)
        lock (lockObj)
        {
            Assert.Single(createdFiles);
            Assert.Contains("root.txt", createdFiles);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_WithDepth0_ShouldOnlyMonitorRoot()
    {
        // Arrange
        var createdFiles = new List<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(0) // Explicit depth 0
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var subDir = Path.Combine(_testRoot, "subdir");
        Directory.CreateDirectory(subDir);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");
        await Task.Delay(500);

        // Assert
        lock (lockObj)
        {
            Assert.Single(createdFiles);
            Assert.Contains("root.txt", createdFiles);
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_WithDepth1_ShouldMonitorDirectChildren()
    {
        // Arrange
        var createdFiles = new List<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(1) // Only direct children
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2"); // Too deep!
        await Task.Delay(500);

        // Assert
        lock (lockObj)
        {
            Assert.Equal(2, createdFiles.Count);
            Assert.Contains("root.txt", createdFiles);
            Assert.Contains("l1.txt", createdFiles);
            Assert.DoesNotContain("l2.txt", createdFiles); // Should be filtered out
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_WithDepth2_ShouldMonitorTwoLevels()
    {
        // Arrange
        var createdFiles = new HashSet<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(2) // Two levels
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");
        Directory.CreateDirectory(level3);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2");
        File.WriteAllText(Path.Combine(level3, "l3.txt"), "l3"); // Too deep!
        
        // Wait for expected events (3 files within depth limit)
        await ActiveWaitHelpers.WaitUntilAsync(
            () => { lock (lockObj) return createdFiles.Count >= 3; },
            timeout: TimeSpan.FromSeconds(3));
        
        // Give extra time to ensure no unexpected 4th event arrives
        await Task.Delay(300);

        // Assert
        lock (lockObj)
        {
            Assert.Equal(3, createdFiles.Count);
            Assert.Contains("root.txt", createdFiles);
            Assert.Contains("l1.txt", createdFiles);
            Assert.Contains("l2.txt", createdFiles);
            Assert.DoesNotContain("l3.txt", createdFiles); // Should be filtered out
        }
    }

    [Fact]
    public async Task IncludeSubdirectories_WithDepthNegative1_ShouldBeUnlimited()
    {
        // Arrange
        var createdFiles = new HashSet<string>();
        var lockObj = new object();

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(-1) // -1 = unlimited
            .OnCreated((s, e) => { lock (lockObj) createdFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");
        var level4 = Path.Combine(level3, "level4");
        Directory.CreateDirectory(level4);
        await Task.Delay(200);

        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2");
        File.WriteAllText(Path.Combine(level3, "l3.txt"), "l3");
        File.WriteAllText(Path.Combine(level4, "l4.txt"), "l4");
        await Task.Delay(500);

        // Assert - All levels detected (using HashSet to handle macOS duplicate events)
        lock (lockObj)
        {
            Assert.Equal(5, createdFiles.Count);
        }
    }

    [Fact]
    public void IncludeSubdirectories_WithInvalidDepth_ShouldThrow()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            ResilientFileSystemMonitor
                .Watch(_testRoot)
                .IncludeSubdirectories(-2) // Invalid: less than -1
                .Build());
        
        Assert.Contains("Max depth must be -1 (unlimited) or greater than or equal to 0", ex.Message);
    }

    [Fact]
    public async Task DepthLimit_ShouldApplyToAllEventTypes()
    {
        // Arrange
        var changedFiles = new List<string>();
        var deletedFiles = new List<string>();
        var lockObj = new object();

        // Create nested structure first
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);
        
        var file1 = Path.Combine(level1, "l1.txt");
        var file2 = Path.Combine(level2, "l2.txt");
        File.WriteAllText(file1, "initial");
        File.WriteAllText(file2, "initial");

        var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.txt")
            .IncludeSubdirectories(1) // Only depth 1
            .OnChanged((s, e) => { lock (lockObj) changedFiles.Add(e.Name!); })
            .OnDeleted((s, e) => { lock (lockObj) deletedFiles.Add(e.Name!); })
            .Build();
        _disposables.Add(monitor);

        await Task.Delay(200);

        // Act - Modify and delete files
        File.WriteAllText(file1, "modified"); // Depth 1 - should fire
        File.WriteAllText(file2, "modified"); // Depth 2 - should NOT fire
        await Task.Delay(500);

        File.Delete(file1); // Depth 1 - should fire
        File.Delete(file2); // Depth 2 - should NOT fire
        await Task.Delay(500);

        // Assert
        lock (lockObj)
        {
            Assert.Contains("l1.txt", changedFiles);
            Assert.DoesNotContain("l2.txt", changedFiles);

            Assert.Contains("l1.txt", deletedFiles);
            Assert.DoesNotContain("l2.txt", deletedFiles);
        }
    }
}
