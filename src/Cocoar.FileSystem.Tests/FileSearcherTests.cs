namespace Cocoar.FileSystem.Tests;

public class FileSearcherTests : IDisposable
{
    private readonly string _testRoot;

    public FileSearcherTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"FileSearcherTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EnumerateFiles_WithNullSearchPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FileSearcher.EnumerateFiles(null!, "*.txt").ToList());
    }

    [Fact]
    public void EnumerateFiles_WithNullSearchPattern_ShouldThrow()
    {
        var dir = new DirectoryInfo(_testRoot);
        Assert.Throws<ArgumentNullException>(() =>
            FileSearcher.EnumerateFiles(dir, null!).ToList());
    }

    [Fact]
    public void EnumerateFiles_WithEmptySearchPattern_ShouldThrow()
    {
        var dir = new DirectoryInfo(_testRoot);
        Assert.Throws<ArgumentException>(() =>
            FileSearcher.EnumerateFiles(dir, string.Empty).ToList());
    }

    [Fact]
    public void EnumerateFiles_WithWhitespaceSearchPattern_ShouldThrow()
    {
        var dir = new DirectoryInfo(_testRoot);
        Assert.Throws<ArgumentException>(() =>
            FileSearcher.EnumerateFiles(dir, "   ").ToList());
    }

    [Fact]
    public void EnumerateFiles_WithNonExistentDirectory_ShouldThrow()
    {
        var nonExistentPath = Path.Combine(_testRoot, "does-not-exist");
        var dir = new DirectoryInfo(nonExistentPath);
        
        Assert.Throws<DirectoryNotFoundException>(() =>
            FileSearcher.EnumerateFiles(dir, "*.txt").ToList());
    }

    [Fact]
    public void EnumerateFiles_WithEmptyDirectory_ShouldReturnEmpty()
    {
        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*").ToList();
        
        Assert.Empty(results);
    }

    [Fact]
    public void EnumerateFiles_WithSingleFile_ShouldReturnThatFile()
    {
        var filePath = Path.Combine(_testRoot, "test.txt");
        File.WriteAllText(filePath, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt").ToList();

        Assert.Single(results);
        Assert.Equal(filePath, results[0]);
    }

    [Fact]
    public void EnumerateFiles_WithMultipleFiles_ShouldReturnAll()
    {
        var file1 = Path.Combine(_testRoot, "test1.txt");
        var file2 = Path.Combine(_testRoot, "test2.txt");
        var file3 = Path.Combine(_testRoot, "test3.log");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt").OrderBy(f => f).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.DoesNotContain(file3, results);
    }

    [Fact]
    public void EnumerateFiles_WithWildcardPattern_ShouldReturnAllFiles()
    {
        var file1 = Path.Combine(_testRoot, "test1.txt");
        var file2 = Path.Combine(_testRoot, "test2.log");
        var file3 = Path.Combine(_testRoot, "data.json");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*").ToList();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void EnumerateFiles_WithSubdirectories_NoMaxDepth_ShouldRecurseAll()
    {
        var subDir1 = Path.Combine(_testRoot, "sub1");
        var subDir2 = Path.Combine(_testRoot, "sub1", "sub2");
        
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(subDir1, "sub1.txt");
        var file3 = Path.Combine(subDir2, "sub2.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt").OrderBy(f => f).ToList();

        Assert.Equal(3, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.Contains(file3, results);
    }

    [Fact]
    public void EnumerateFiles_WithMaxDepth_Zero_ShouldReturnOnlyRootFiles()
    {
        var subDir = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(subDir, "sub.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", maxDepth: 0).ToList();

        Assert.Single(results);
        Assert.Contains(file1, results);
        Assert.DoesNotContain(file2, results);
    }

    [Fact]
    public void EnumerateFiles_WithMaxDepth_One_ShouldReturnRootAndFirstLevel()
    {
        var subDir1 = Path.Combine(_testRoot, "sub1");
        var subDir2 = Path.Combine(subDir1, "sub2");
        
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(subDir1, "sub1.txt");
        var file3 = Path.Combine(subDir2, "sub2.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", maxDepth: 1).OrderBy(f => f).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.DoesNotContain(file3, results);
    }

    [Fact]
    public void EnumerateFiles_WithExcludedFolders_ShouldSkipThem()
    {
        var subDir1 = Path.Combine(_testRoot, "include");
        var subDir2 = Path.Combine(_testRoot, "exclude");
        
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(subDir1, "include.txt");
        var file3 = Path.Combine(subDir2, "exclude.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var excluded = new HashSet<string> { "exclude" };
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", excluded).OrderBy(f => f).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.DoesNotContain(file3, results);
    }

    [Fact]
    public void EnumerateFiles_WithNestedExcludedFolders_ShouldSkipEntireTree()
    {
        var excluded = Path.Combine(_testRoot, "excluded");
        var nested = Path.Combine(excluded, "nested");
        
        Directory.CreateDirectory(excluded);
        Directory.CreateDirectory(nested);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(excluded, "excluded.txt");
        var file3 = Path.Combine(nested, "nested.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var excludedFolders = new HashSet<string> { "excluded" };
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", excludedFolders).ToList();

        Assert.Single(results);
        Assert.Contains(file1, results);
    }

    [Fact]
    public void EnumerateFiles_WithNullExcludedFolders_ShouldIncludeAll()
    {
        var subDir = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(_testRoot, "root.txt");
        var file2 = Path.Combine(subDir, "sub.txt");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", excludedFolders: null).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void EnumerateFiles_WithComplexPattern_ShouldMatchCorrectly()
    {
        var file1 = Path.Combine(_testRoot, "test.config");
        var file2 = Path.Combine(_testRoot, "app.config");
        var file3 = Path.Combine(_testRoot, "data.json");
        
        File.WriteAllText(file1, "content");
        File.WriteAllText(file2, "content");
        File.WriteAllText(file3, "content");

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.config").OrderBy(f => f).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(file1, results);
        Assert.Contains(file2, results);
        Assert.DoesNotContain(file3, results);
    }

    [Fact]
    public void EnumerateFiles_WithDeepNesting_ShouldHandleEfficiently()
    {
        var currentDir = _testRoot;
        var files = new List<string>();

        for (int i = 0; i < 10; i++)
        {
            currentDir = Path.Combine(currentDir, $"level{i}");
            Directory.CreateDirectory(currentDir);
            
            var filePath = Path.Combine(currentDir, $"file{i}.txt");
            File.WriteAllText(filePath, "content");
            files.Add(filePath);
        }

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt").ToList();

        Assert.Equal(10, results.Count);
        foreach (var file in files)
        {
            Assert.Contains(file, results);
        }
    }

    [Fact]
    public void SearchBuilder_ImplementsIEnumerable_CanUseForeach()
    {
        var file1 = Path.Combine(_testRoot, "test1.txt");
        var file2 = Path.Combine(_testRoot, "test2.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        var builder = FileSearcher.InDirectory(_testRoot).WithPattern("*.txt");
        
        var collected = new List<string>();
        foreach (var file in builder)
        {
            collected.Add(file);
        }

        Assert.Equal(2, collected.Count);
        Assert.Contains(file1, collected);
        Assert.Contains(file2, collected);
    }

    [Fact]
    public void SearchBuilder_ImplementsIEnumerable_CanUseLinqDirectly()
    {
        var file1 = Path.Combine(_testRoot, "test1.txt");
        var file2 = Path.Combine(_testRoot, "test2.log");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        var builder = FileSearcher.InDirectory(_testRoot);
        
        var txtFiles = builder.Where(f => f.EndsWith(".txt", StringComparison.Ordinal)).ToList();

        Assert.Single(txtFiles);
        Assert.Equal(file1, txtFiles[0]);
    }

    [Fact]
    public void EnumerateFiles_WithManyFiles_ShouldHandleEfficiently()
    {
        const int fileCount = 100;
        
        for (int i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(_testRoot, $"file{i:D3}.txt");
            File.WriteAllText(filePath, "content");
        }

        var dir = new DirectoryInfo(_testRoot);
        var results = FileSearcher.EnumerateFiles(dir, "*.txt").ToList();

        Assert.Equal(fileCount, results.Count);
    }

    [Fact]
    public void EnumerateFiles_IsLazyEvaluated_ShouldNotEnumerateAll()
    {
        for (int i = 0; i < 10; i++)
        {
            var filePath = Path.Combine(_testRoot, $"file{i}.txt");
            File.WriteAllText(filePath, "content");
        }

        var dir = new DirectoryInfo(_testRoot);
        var enumerable = FileSearcher.EnumerateFiles(dir, "*.txt");

        var firstThree = enumerable.Take(3).ToList();

        Assert.Equal(3, firstThree.Count);
    }

    [Fact]
    public void EnumerateFiles_WithMultipleExcludedFolders_ShouldSkipAll()
    {
        var sub1 = Path.Combine(_testRoot, "excluded1");
        var sub2 = Path.Combine(_testRoot, "excluded2");
        var sub3 = Path.Combine(_testRoot, "included");
        
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);
        Directory.CreateDirectory(sub3);

        File.WriteAllText(Path.Combine(sub1, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(sub2, "file2.txt"), "content");
        var includedFile = Path.Combine(sub3, "file3.txt");
        File.WriteAllText(includedFile, "content");

        var dir = new DirectoryInfo(_testRoot);
        var excluded = new HashSet<string> { "excluded1", "excluded2" };
        var results = FileSearcher.EnumerateFiles(dir, "*.txt", excluded).ToList();

        Assert.Single(results);
        Assert.Equal(includedFile, results[0]);
    }
}
