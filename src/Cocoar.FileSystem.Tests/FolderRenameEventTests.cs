using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

/// <summary>
/// Tests for folder rename event detection when IncludeFolderEvents is enabled.
/// Validates that ResilientFileSystemMonitor can detect directory renames containing matching files.
/// </summary>
public sealed class FolderRenameEventTests : IDisposable
{
    private readonly string _testRoot;
    private readonly List<IDisposable> _disposables = [];

    public FolderRenameEventTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"FolderRename_Tests_{Guid.NewGuid():N}");
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

    private string GetTestPath(string relativePath) => Path.Combine(_testRoot, relativePath);

    #region Folder Rename Detection Tests

    [Fact]
    public async Task FolderRename_WithMatchingFiles_ShouldEmitRenamedEvent()
    {
        // Arrange: Create a folder with a matching .pfx file
        var oldFolder = GetTestPath("certificates-old");
        Directory.CreateDirectory(oldFolder);
        var certFile = Path.Combine(oldFolder, "certificate.pfx");
        await File.WriteAllTextAsync(certFile, "fake certificate data");

        bool eventFired = false;
        RenamedEventArgs? capturedEvent = null;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) =>
            {
                capturedEvent = e;
                eventFired = true;
            })
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the folder
        var newFolder = GetTestPath("certificates-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should receive renamed event for the folder
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "folder rename event");

        Assert.NotNull(capturedEvent);
        Assert.Contains("certificates-old", capturedEvent.OldFullPath);
        Assert.Contains("certificates-new", capturedEvent.FullPath);
    }

    [Fact]
    public async Task FolderRename_WithoutMatchingFiles_ShouldNotEmitEvent()
    {
        // Arrange: Create a folder with only non-matching files
        var oldFolder = GetTestPath("documents-old");
        Directory.CreateDirectory(oldFolder);
        var txtFile = Path.Combine(oldFolder, "readme.txt");
        await File.WriteAllTextAsync(txtFile, "some text");

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the folder
        var newFolder = GetTestPath("documents-new");
        Directory.Move(oldFolder, newFolder);

        // Wait to ensure no event is fired
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert: Should NOT receive renamed event (no .pfx files in folder)
        Assert.False(eventFired);
    }

    [Fact]
    public async Task FolderRename_AlwaysEnabled_EmitsEventForMatchingFiles()
    {
        // Folder rename detection is always enabled - this is core functionality for correctness
        // Arrange: Create a folder with a matching .pfx file
        var oldFolder = GetTestPath("certs-old");
        Directory.CreateDirectory(oldFolder);
        var certFile = Path.Combine(oldFolder, "test.pfx");
        await File.WriteAllTextAsync(certFile, "certificate");

        bool eventFired = false;
        RenamedEventArgs? capturedEvent = null;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            .OnRenamed((_, e) => 
            {
                capturedEvent = e;
                eventFired = true;
            })
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the folder
        var newFolder = GetTestPath("certs-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should receive event because folder contains matching files
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "folder rename event");

        Assert.True(eventFired);
        Assert.NotNull(capturedEvent);
        Assert.Contains("certs-old", capturedEvent.OldFullPath);
        Assert.Contains("certs-new", capturedEvent.FullPath);
    }

    [Fact]
    public async Task FolderRename_WithMultipleMatchingFiles_ShouldEmitSingleEvent()
    {
        // Arrange: Create folder with multiple matching files
        var oldFolder = GetTestPath("multi-certs-old");
        Directory.CreateDirectory(oldFolder);
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "cert1.pfx"), "cert1");
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "cert2.pfx"), "cert2");
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "cert3.pfx"), "cert3");

        int eventCount = 0;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => Interlocked.Increment(ref eventCount))
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the folder
        var newFolder = GetTestPath("multi-certs-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should receive a single renamed event for the folder
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventCount > 0,
            timeout: TimeSpan.FromSeconds(5),
            description: "folder rename event");

        // Should only emit one event for the folder, not one per file
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task FolderRename_WithNestedMatchingFiles_ShouldEmitEvent()
    {
        // Arrange: Create folder with nested structure containing matching files
        var oldFolder = GetTestPath("root-old");
        Directory.CreateDirectory(oldFolder);
        var subFolder = Path.Combine(oldFolder, "subfolder");
        Directory.CreateDirectory(subFolder);
        await File.WriteAllTextAsync(Path.Combine(subFolder, "nested.pfx"), "nested cert");

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the root folder (which contains nested matching files)
        var newFolder = GetTestPath("root-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should detect the nested .pfx file and emit event
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "nested folder rename event");

        Assert.True(eventFired);
    }

    [Fact]
    public async Task FolderRename_WithMultiplePatterns_ShouldDetectAnyMatch()
    {
        // Arrange: Create folder with files matching one of multiple patterns
        var oldFolder = GetTestPath("keys-old");
        Directory.CreateDirectory(oldFolder);
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "key.p12"), "p12 file");
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "readme.txt"), "text file");

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx", "*.p12", "*.cer")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename folder
        var newFolder = GetTestPath("keys-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should detect .p12 file matches pattern
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "folder rename with .p12 file");

        Assert.True(eventFired);
    }

    [Fact]
    public async Task FolderRename_WithMixedFileTypes_ShouldEmitEventIfAnyMatch()
    {
        // Arrange: Create folder with both matching and non-matching files
        var oldFolder = GetTestPath("mixed-old");
        Directory.CreateDirectory(oldFolder);
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "cert.pfx"), "certificate");
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "readme.txt"), "readme");
        await File.WriteAllTextAsync(Path.Combine(oldFolder, "config.json"), "config");

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename folder
        var newFolder = GetTestPath("mixed-new");
        Directory.Move(oldFolder, newFolder);

        // Assert: Should emit event because cert.pfx matches
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "folder rename with mixed files");

        Assert.True(eventFired);
    }

    [Fact]
    public async Task FolderRename_EmptyFolder_ShouldNotEmitEvent()
    {
        // Arrange: Create empty folder
        var oldFolder = GetTestPath("empty-old");
        Directory.CreateDirectory(oldFolder);

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename empty folder
        var newFolder = GetTestPath("empty-new");
        Directory.Move(oldFolder, newFolder);

        // Wait to ensure no event
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert: Should not emit event (no matching files)
        Assert.False(eventFired);
    }

    [Fact]
    public async Task FolderRename_WithMaxDepthLimit_ShouldRespectDepthSetting()
    {
        // Arrange: Create nested structure deeper than maxDepth
        var oldFolder = GetTestPath("depth-old");
        Directory.CreateDirectory(oldFolder);
        var level1 = Path.Combine(oldFolder, "level1");
        Directory.CreateDirectory(level1);
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);
        
        // Put matching file at depth 2
        await File.WriteAllTextAsync(Path.Combine(level2, "deep.pfx"), "deep cert");

        bool eventFired = false;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(maxDepth: 1) // Only search 1 level deep
            
            .OnRenamed((_, e) => eventFired = true)
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the root folder
        var newFolder = GetTestPath("depth-new");
        Directory.Move(oldFolder, newFolder);

        // Wait to ensure no event (file is beyond maxDepth)
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert: Should NOT emit event because matching file is at depth 2 (beyond limit of 1)
        Assert.False(eventFired);
    }

    [Fact]
    public async Task FileRename_StillWorks_WithFolderEventsEnabled()
    {
        // Ensure that enabling folder events doesn't break normal file rename detection
        // Arrange
        var testFile = GetTestPath("original.pfx");
        await File.WriteAllTextAsync(testFile, "certificate");

        bool eventFired = false;
        RenamedEventArgs? capturedEvent = null;

        using var monitor = ResilientFileSystemMonitor
            .Watch(_testRoot)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) =>
            {
                capturedEvent = e;
                eventFired = true;
            })
            .Build();

        _disposables.Add(monitor);

        // Act: Rename the file (not folder)
        var renamedFile = GetTestPath("renamed.pfx");
        File.Move(testFile, renamedFile);

        // Assert: Should still detect file renames
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "file rename event");

        Assert.NotNull(capturedEvent);
        Assert.Contains("original.pfx", capturedEvent.OldName);
        Assert.Contains("renamed.pfx", capturedEvent.Name);
    }

    #endregion

    #region Certificate Rotation Scenario (Real-World Use Case)

    [Fact]
    public async Task CertificateRotation_FolderRename_ShouldEnableAtomicKeySwitch()
    {
        // Real-world scenario: Certificate management with atomic folder rename for key rotation
        // Arrange: Set up initial certificate structure
        var kid1Folder = GetTestPath("certificates/kid1");
        Directory.CreateDirectory(kid1Folder);
        await File.WriteAllTextAsync(Path.Combine(kid1Folder, "cert.pfx"), "production key 1");

        var kid2StagingFolder = GetTestPath("certificates/kid2-staging");
        Directory.CreateDirectory(kid2StagingFolder);
        await File.WriteAllTextAsync(Path.Combine(kid2StagingFolder, "cert.pfx"), "new key 2");

        bool eventFired = false;
        RenamedEventArgs? capturedEvent = null;
        var basePath = GetTestPath("certificates");

        using var monitor = ResilientFileSystemMonitor
            .Watch(basePath)
            .WithFilter("*.pfx")
            .IncludeSubdirectories(true)
            
            .OnRenamed((_, e) =>
            {
                capturedEvent = e;
                eventFired = true;
            })
            .Build();

        _disposables.Add(monitor);

        // Act: Atomic key rotation - rename staging folder to activate new key
        var kid2Folder = GetTestPath("certificates/kid2");
        Directory.Move(kid2StagingFolder, kid2Folder);

        // Assert: Monitor should detect folder rename and can trigger certificate inventory refresh
        await ActiveWaitHelpers.WaitUntilAsync(
            () => eventFired,
            timeout: TimeSpan.FromSeconds(5),
            description: "certificate rotation event");

        Assert.NotNull(capturedEvent);
        Assert.Contains("kid2-staging", capturedEvent.OldFullPath);
        Assert.Contains("kid2", capturedEvent.FullPath);
        Assert.DoesNotContain("staging", capturedEvent.FullPath);
    }

    #endregion
}
