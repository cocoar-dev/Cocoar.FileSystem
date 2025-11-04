using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

public class FluentApiTests : IDisposable
{
    private readonly string _testDirectory;

    public FluentApiTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CocoarFS_FluentTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
        
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void MonitorBuilder_Watch_ShouldCreateMonitor()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .Build();

        Assert.NotNull(monitor);
        Assert.True(monitor.IsUsingWatcher);
    }

    [Fact]
    public void MonitorBuilder_WatchWithFilter_ShouldCreateMonitor()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory, "*.json")
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void MonitorBuilder_WithDebounce_Milliseconds_ShouldAccept()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .WithDebounce(100)
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void MonitorBuilder_WithDebounce_TimeSpan_ShouldAccept()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .WithDebounce(TimeSpan.FromMilliseconds(200))
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void MonitorBuilder_WithPollingFallback_Milliseconds_ShouldAccept()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .WithPollingFallback(3000)
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void MonitorBuilder_WithPollingFallback_TimeSpan_ShouldAccept()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .WithPollingFallback(TimeSpan.FromSeconds(3))
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void MonitorBuilder_WithoutPollingFallback_ShouldAccept()
    {
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .WithoutPollingFallback()
            .Build();

        Assert.NotNull(monitor);
    }

    [Fact]
    public async Task MonitorBuilder_OnCreated_ShouldRegisterHandler()
    {
        bool eventFired = false;
        
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .OnCreated((sender, e) => eventFired = true)
            .Build();

        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test");

        await ActiveWaitHelpers.WaitUntilAsync(() => eventFired, TimeSpan.FromSeconds(3));
        
        Assert.True(eventFired);
    }

    [Fact]
    public async Task MonitorBuilder_OnChanged_ShouldRegisterHandler()
    {
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "initial");
        
        bool eventFired = false;
        
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .OnChanged((sender, e) => eventFired = true)
            .Build();

        Thread.Sleep(100);
        File.WriteAllText(testFile, "changed");

        await ActiveWaitHelpers.WaitUntilAsync(() => eventFired, TimeSpan.FromSeconds(3));
        
        Assert.True(eventFired);
    }

    [Fact]
    public async Task MonitorBuilder_OnDeleted_ShouldRegisterHandler()
    {
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test");
        
        bool eventFired = false;
        
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .OnDeleted((sender, e) => eventFired = true)
            .Build();

        Thread.Sleep(100);
        File.Delete(testFile);

        await ActiveWaitHelpers.WaitUntilAsync(() => eventFired, TimeSpan.FromSeconds(3));
        
        Assert.True(eventFired);
    }

    [Fact]
    public async Task MonitorBuilder_MultipleHandlers_ShouldRegisterAll()
    {
        bool createdFired = false;
        bool changedFired = false;
        bool deletedFired = false;
        
        using var monitor = ResilientFileSystemMonitor
            .Watch(_testDirectory)
            .OnCreated((sender, e) => createdFired = true)
            .OnChanged((sender, e) => changedFired = true)
            .OnDeleted((sender, e) => deletedFired = true)
            .Build();

        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test");

        await ActiveWaitHelpers.WaitUntilAsync(() => createdFired, TimeSpan.FromSeconds(3));
        Assert.True(createdFired);

        Thread.Sleep(100);
        File.WriteAllText(testFile, "changed");

        await ActiveWaitHelpers.WaitUntilAsync(() => changedFired, TimeSpan.FromSeconds(3));
        Assert.True(changedFired);

        Thread.Sleep(100);
        File.Delete(testFile);

        await ActiveWaitHelpers.WaitUntilAsync(() => deletedFired, TimeSpan.FromSeconds(3));
        Assert.True(deletedFired);
    }

    [Fact]
    public void MonitorBuilder_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => ResilientFileSystemMonitor.Watch(""));
    }

    [Fact]
    public void MonitorBuilder_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => ResilientFileSystemMonitor.Watch(null!));
    }

    [Fact]
    public void MonitorBuilder_WithNegativeDebounce_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => 
            ResilientFileSystemMonitor
                .Watch(_testDirectory)
                .WithDebounce(-100));
    }

    [Fact]
    public void MonitorBuilder_WithNegativePollingInterval_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => 
            ResilientFileSystemMonitor
                .Watch(_testDirectory)
                .WithPollingFallback(-1000));
    }

    [Fact]
    public void SearchBuilder_InDirectory_ShouldCreateBuilder()
    {
        var files = FileSearcher
            .InDirectory(_testDirectory)
            .ToList();

        Assert.NotNull(files);
    }

    [Fact]
    public void SearchBuilder_Search_WithPattern_ShouldCreateBuilder()
    {
        var files = FileSearcher
            .Search(_testDirectory, "*.txt")
            .ToList();

        Assert.NotNull(files);
    }

    [Fact]
    public void SearchBuilder_WithPattern_ShouldFindMatchingFiles()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "test1.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "test2.json"), "test");

        var files = FileSearcher
            .Search(_testDirectory, "*.txt")
            .ToList();

        Assert.Single(files);
        Assert.Contains("test1.txt", files[0]);
    }

    [Fact]
    public void SearchBuilder_Excluding_ShouldSkipFolders()
    {
        var subDir = Path.Combine(_testDirectory, "excluded");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "test.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "root.txt"), "test");

        var files = FileSearcher
            .Search(_testDirectory, "*.txt")
            .Excluding("excluded")
            .ToList();

        Assert.Single(files);
        Assert.Contains("root.txt", files[0]);
    }

    [Fact]
    public void SearchBuilder_WithMaxDepth_ShouldLimitDepth()
    {
        var subDir = Path.Combine(_testDirectory, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "root.txt"), "test");

        var files = FileSearcher
            .Search(_testDirectory, "*.txt")
            .WithMaxDepth(0)
            .ToList();

        Assert.Single(files);
        Assert.Contains("root.txt", files[0]);
    }

    [Fact]
    public void SearchBuilder_InCurrentDirectoryOnly_ShouldNotRecurse()
    {
        var subDir = Path.Combine(_testDirectory, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "root.txt"), "test");

        var files = FileSearcher
            .Search(_testDirectory, "*.txt")
            .InCurrentDirectoryOnly()
            .ToList();

        Assert.Single(files);
        Assert.Contains("root.txt", files[0]);
    }

    [Fact]
    public void SearchBuilder_Any_ShouldReturnTrueWhenFilesExist()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "test");

        bool hasFiles = FileSearcher
            .Search(_testDirectory, "*.txt")
            .Any();

        Assert.True(hasFiles);
    }

    [Fact]
    public void SearchBuilder_Any_ShouldReturnFalseWhenNoFilesExist()
    {
        bool hasFiles = FileSearcher
            .Search(_testDirectory, "*.txt")
            .Any();

        Assert.False(hasFiles);
    }

    [Fact]
    public void SearchBuilder_Count_ShouldReturnCorrectCount()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "test1.txt"), "test");
        File.WriteAllText(Path.Combine(_testDirectory, "test2.txt"), "test");

        int count = FileSearcher
            .Search(_testDirectory, "*.txt")
            .Count();

        Assert.Equal(2, count);
    }

    [Fact]
    public void SearchBuilder_First_ShouldReturnFirstFile()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "test");

        var first = FileSearcher
            .Search(_testDirectory, "*.txt")
            .First();

        Assert.Contains("test.txt", first);
    }

    [Fact]
    public void SearchBuilder_FirstOrDefault_ShouldReturnNullWhenNoFiles()
    {
        var first = FileSearcher
            .Search(_testDirectory, "*.txt")
            .FirstOrDefault();

        Assert.Null(first);
    }

    [Fact]
    public void SearchBuilder_WithEmptyPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileSearcher.InDirectory(""));
    }

    [Fact]
    public void SearchBuilder_WithNullPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FileSearcher.InDirectory(null!));
    }
}
