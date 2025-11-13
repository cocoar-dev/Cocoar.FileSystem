using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

public sealed class MultiplePatternTests : IDisposable
{
    private readonly string _testRoot;
    
    public MultiplePatternTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"CocoarFS_MultiPattern_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }
    
    public void Dispose()
    {
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

    [Fact]
    public async Task MultiplePatterns_MatchesAllSpecified()
    {
        // Arrange
        var createdFiles = new List<string>();
        
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("*.pfx", "*.p12", "*.cer")
            .Build();
        
        monitor.Created += (_, e) => createdFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(200);
        
        // Act
        File.WriteAllText(Path.Combine(_testRoot, "cert.pfx"), "pfx");
        File.WriteAllText(Path.Combine(_testRoot, "cert.p12"), "p12");
        File.WriteAllText(Path.Combine(_testRoot, "cert.cer"), "cer");
        File.WriteAllText(Path.Combine(_testRoot, "readme.txt"), "txt");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 3,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert - Only certificate files detected
        Assert.Contains("cert.pfx", createdFiles);
        Assert.Contains("cert.p12", createdFiles);
        Assert.Contains("cert.cer", createdFiles);
        Assert.DoesNotContain("readme.txt", createdFiles);
    }

    [Fact]
    public async Task MultiplePatterns_WorksWithWildcards()
    {
        // Arrange
        var createdFiles = new List<string>();
        
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("test-*.txt", "backup-*.log")
            .Build();
        
        monitor.Created += (_, e) => createdFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(200);
        
        // Act
        File.WriteAllText(Path.Combine(_testRoot, "test-1.txt"), "1");
        File.WriteAllText(Path.Combine(_testRoot, "test-2.txt"), "2");
        File.WriteAllText(Path.Combine(_testRoot, "backup-old.log"), "old");
        File.WriteAllText(Path.Combine(_testRoot, "data.csv"), "csv");
        File.WriteAllText(Path.Combine(_testRoot, "other.txt"), "other");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 3,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert
        Assert.Contains("test-1.txt", createdFiles);
        Assert.Contains("test-2.txt", createdFiles);
        Assert.Contains("backup-old.log", createdFiles);
        Assert.DoesNotContain("data.csv", createdFiles);
        Assert.DoesNotContain("other.txt", createdFiles);
    }

    [Fact]
    public async Task MultiplePatterns_WorksWithSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(subDir);
        
        var createdFiles = new List<string>();
        
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("*.json", "*.xml")
            .IncludeSubdirectories()
            .Build();
        
        monitor.Created += (_, e) => createdFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(200);
        
        // Act
        File.WriteAllText(Path.Combine(_testRoot, "root.json"), "{}");
        File.WriteAllText(Path.Combine(subDir, "sub.xml"), "<xml/>");
        File.WriteAllText(Path.Combine(subDir, "sub.txt"), "txt");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 2,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert
        Assert.Contains("root.json", createdFiles);
        Assert.Contains("sub.xml", createdFiles);
        Assert.DoesNotContain("sub.txt", createdFiles);
    }

    [Fact]
    public async Task MultiplePatterns_SnapshotFindsExistingFiles()
    {
        // Arrange - Create files BEFORE starting monitor
        File.WriteAllText(Path.Combine(_testRoot, "existing.pfx"), "pfx");
        File.WriteAllText(Path.Combine(_testRoot, "existing.p12"), "p12");
        File.WriteAllText(Path.Combine(_testRoot, "existing.txt"), "txt");
        
        var changedFiles = new List<string>();
        
        // Act - Start monitor (should snapshot existing files)
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("*.pfx", "*.p12")
            .Build();
        
        monitor.Changed += (_, e) => changedFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(500); // Give time for initial snapshot
        
        // Modify files to trigger change events
        File.WriteAllText(Path.Combine(_testRoot, "existing.pfx"), "modified-pfx");
        File.WriteAllText(Path.Combine(_testRoot, "existing.p12"), "modified-p12");
        File.WriteAllText(Path.Combine(_testRoot, "existing.txt"), "modified-txt");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => changedFiles.Count >= 2,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert - Should detect changes to pfx/p12 but not txt
        Assert.Contains("existing.pfx", changedFiles);
        Assert.Contains("existing.p12", changedFiles);
        Assert.DoesNotContain("existing.txt", changedFiles);
    }

    [Fact]
    public void WithFilter_EmptyArray_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            ResilientFileSystemMonitor.Watch(_testRoot).WithFilter([]));
    }

    [Fact]
    public void WithFilter_NullPattern_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            ResilientFileSystemMonitor.Watch(_testRoot).WithFilter((string[])null!));
    }

    [Fact]
    public void WithFilter_ContainsNullOrWhitespace_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            ResilientFileSystemMonitor.Watch(_testRoot).WithFilter("*.txt", null!, "*.log"));
    }

    [Fact]
    public async Task WithFilter_MultipleCalls_AddsPatterns()
    {
        // Arrange
        var createdFiles = new List<string>();
        
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("*.txt")           // First call
            .WithFilter("*.pfx", "*.p12")  // Second call - should ADD, not replace
            .Build();
        
        monitor.Created += (_, e) => createdFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(200);
        
        // Act
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "txt");
        File.WriteAllText(Path.Combine(_testRoot, "test.pfx"), "pfx");
        File.WriteAllText(Path.Combine(_testRoot, "test.p12"), "p12");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 3,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert - Should match ALL patterns (additive behavior)
        Assert.Contains("test.txt", createdFiles);
        Assert.Contains("test.pfx", createdFiles);
        Assert.Contains("test.p12", createdFiles);
    }

    [Fact]
    public async Task ClearFilters_RemovesAllPatterns()
    {
        // Arrange
        var createdFiles = new List<string>();
        
        using var monitor = ResilientFileSystemMonitor.Watch(_testRoot)
            .WithFilter("*.txt")
            .WithFilter("*.pfx")
            .ClearFilters()  // Should clear all patterns
            .Build();
        
        monitor.Created += (_, e) => createdFiles.Add(Path.GetFileName(e.FullPath));
        
        await Task.Delay(200);
        
        // Act
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "txt");
        File.WriteAllText(Path.Combine(_testRoot, "test.pfx"), "pfx");
        
        await ActiveWaitHelpers.WaitUntilAsync(
            () => createdFiles.Count >= 2,
            timeout: TimeSpan.FromSeconds(3));
        
        // Assert - Should match ALL files (no filters)
        Assert.Contains("test.txt", createdFiles);
        Assert.Contains("test.pfx", createdFiles);
    }
}
