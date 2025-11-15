namespace Cocoar.FileSystem.Tests;

/// <summary>
/// Tests for harmonized API between SearchBuilder and MonitorBuilder.
/// </summary>
public sealed class SearchBuilderHarmonizationTests : IDisposable
{
    private readonly string _testRoot;

    public SearchBuilderHarmonizationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"CocoarFS_SearchHarmonization_{Guid.NewGuid():N}");
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
    public void WithFilter_MultiplePatterns_FindsAllMatches()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "file1.cs"), "code");
        File.WriteAllText(Path.Combine(_testRoot, "file2.csproj"), "project");
        File.WriteAllText(Path.Combine(_testRoot, "file3.txt"), "text");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithFilter("*.cs", "*.csproj")
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, f => Path.GetFileName(f) == "file1.cs");
        Assert.Contains(results, f => Path.GetFileName(f) == "file2.csproj");
        Assert.DoesNotContain(results, f => Path.GetFileName(f) == "file3.txt");
    }

    [Fact]
    public void WithFilter_Additive_AccumulatesPatterns()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "test.log"), "log");
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "text");
        File.WriteAllText(Path.Combine(_testRoot, "test.json"), "json");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithFilter("*.log")
            .WithFilter("*.txt")
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, f => Path.GetFileName(f) == "test.log");
        Assert.Contains(results, f => Path.GetFileName(f) == "test.txt");
        Assert.DoesNotContain(results, f => Path.GetFileName(f) == "test.json");
    }

    [Fact]
    public void ClearFilters_RemovesAllPatterns()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "test.log"), "log");
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "text");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithFilter("*.log")
            .ClearFilters()
            .ToList();

        // Assert - Should match all files (no filter)
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void IncludeSubdirectories_Int_ControlsDepth()
    {
        // Arrange
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");
        File.WriteAllText(Path.Combine(level2, "l2.txt"), "l2");

        // Act - Depth 1
        var resultsDepth1 = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .IncludeSubdirectories(1)
            .ToList();

        // Assert
        Assert.Equal(2, resultsDepth1.Count); // root.txt + l1.txt
        Assert.Contains(resultsDepth1, f => f.EndsWith("root.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resultsDepth1, f => f.EndsWith("l1.txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(resultsDepth1, f => f.EndsWith("l2.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludeSubdirectories_Bool_True_Unlimited()
    {
        // Arrange
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");
        Directory.CreateDirectory(level3);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level3, "deep.txt"), "deep");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .IncludeSubdirectories(true)
            .ToList();

        // Assert - Should find all files at any depth
        Assert.Equal(2, results.Count);
        Assert.Contains(results, f => f.EndsWith("root.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, f => f.EndsWith("deep.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludeSubdirectories_Bool_False_CurrentDirectoryOnly()
    {
        // Arrange
        var subdir = Path.Combine(_testRoot, "subdir");
        Directory.CreateDirectory(subdir);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subdir, "sub.txt"), "sub");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .IncludeSubdirectories(false)
            .ToList();

        // Assert - Only root directory
        Assert.Single(results);
        Assert.Contains(results, f => f.EndsWith("root.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludeSubdirectories_NegativeOne_Unlimited()
    {
        // Arrange
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level2, "deep.txt"), "deep");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .IncludeSubdirectories(-1)
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void WithPattern_ClearsMultipleFilters()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "test.log"), "log");
        File.WriteAllText(Path.Combine(_testRoot, "test.txt"), "text");

        // Act - WithPattern should clear filters set by WithFilter
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithFilter("*.log", "*.txt")
            .WithPattern("*.txt")  // Should clear previous filters
            .ToList();

        // Assert - Only .txt files
        Assert.Single(results);
        Assert.Contains(results, f => f.EndsWith("test.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultipleFilters_WithExclusions_WorksTogether()
    {
        // Arrange
        var binDir = Path.Combine(_testRoot, "bin");
        Directory.CreateDirectory(binDir);

        File.WriteAllText(Path.Combine(_testRoot, "app.cs"), "code");
        File.WriteAllText(Path.Combine(_testRoot, "app.csproj"), "project");
        File.WriteAllText(Path.Combine(binDir, "output.cs"), "compiled");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithFilter("*.cs", "*.csproj")
            .Excluding("bin", "obj")
            .Recursively()
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, f => f.EndsWith("app.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, f => f.EndsWith("app.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, f => f.Contains("bin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recursively_StillWorks_AfterHarmonization()
    {
        // Arrange
        var subdir = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(subdir);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subdir, "sub.txt"), "sub");

        // Act
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .Recursively()
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void WithMaxDepth_StillWorks_BackwardCompatibility()
    {
        // Arrange
        var level1 = Path.Combine(_testRoot, "level1");
        Directory.CreateDirectory(level1);

        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), "root");
        File.WriteAllText(Path.Combine(level1, "l1.txt"), "l1");

        // Act - Old API should still work
        var results = FileSearcher
            .InDirectory(_testRoot)
            .WithPattern("*.txt")
            .WithMaxDepth(0)
            .ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(results, f => f.EndsWith("root.txt", StringComparison.OrdinalIgnoreCase));
    }
}
