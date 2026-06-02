using Cocoar.FileSystem.Tests.TestUtilities;

namespace Cocoar.FileSystem.Tests;

/// <summary>
/// Tests for the opt-in <c>WithSymlinkTargetTracking()</c> capability.
///
/// Scenario: a Kubernetes ConfigMap/Secret volume mounts each key as a symlink whose content is
/// updated by an atomic swap of a sibling directory symlink (kubelet uses "..data" / "..&lt;timestamp&gt;").
/// The user-visible file (e.g. config.json) is never modified — only the intermediate link's target
/// changes — so the monitor's metadata-only fingerprint misses the update on the snapshot/audit path.
///
/// With tracking on, the monitor folds the file's fully-resolved final target into the fingerprint,
/// so a target swap is detected and surfaced as a Changed event on the user-visible path.
///
/// These tests create real symlinks and are skipped on hosts that cannot create them (e.g. Windows
/// without Developer Mode / elevation).
/// </summary>
public sealed class SymlinkTargetTrackingTests : IDisposable
{
    private static readonly bool SymlinksSupported = CheckSymlinkSupport();

    private readonly string _testRoot;
    private readonly List<IDisposable> _disposables = new();

    public SymlinkTargetTrackingTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"SymlinkTracking_Tests_{Guid.NewGuid():N}");
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

    [Fact]
    public async Task ConfigMapStyleSwap_WithTracking_EmitsChangedForUserVisiblePath()
    {
        if (!EnsureSymlinkCapability())
            return; // skipped on Windows without symlink privilege (not a k8s target platform)

        var root = NewRoot();
        // Pin a single mtime so the ONLY difference between the two revisions is the resolved
        // target path — proving detection rides on the resolved target, not on length/mtime.
        var pinnedMtime = DateTime.UtcNow.AddMinutes(-5);
        BuildConfigMapLayout(root, "AAAAAA", pinnedMtime);

        var monitor = ResilientFileSystemMonitor
            .Watch(root, "config.json")
            .WithSymlinkTargetTracking()
            .WithHealthCheckInterval(TimeSpan.FromMilliseconds(100))
            .WithAuditInterval(TimeSpan.FromSeconds(1))
            .Build();
        _disposables.Add(monitor);

        var changed = new List<string>();
        var gate = new object();
        monitor.Changed += (_, e) => { lock (gate) changed.Add(e.Name!); };

        // Let the initial index settle so the swap is detected as a change, not a create.
        await Task.Delay(300);

        // Atomic-ish swap: new revision, identical length + identical mtime, only the resolved
        // target differs. config.json itself is never touched.
        SwapConfigMapData(root, "revB", "BBBBBB", pinnedMtime);

        await ActiveWaitHelpers.WaitUntilAsync(
            () => { lock (gate) return changed.Contains("config.json"); },
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(50),
            "Changed event for config.json after ConfigMap-style symlink swap");

        lock (gate)
            Assert.Contains("config.json", changed);
    }

    [Fact]
    public async Task ConfigMapStyleSwap_WithoutTracking_DoesNotEmitForSymlinkedFile()
    {
        if (!EnsureSymlinkCapability())
            return; // skipped on Windows without symlink privilege (not a k8s target platform)

        var root = NewRoot();
        var pinnedMtime = DateTime.UtcNow.AddMinutes(-5);
        BuildConfigMapLayout(root, "AAAAAA", pinnedMtime);

        // No WithSymlinkTargetTracking(): default strict behaviour — reparse points are skipped,
        // so the per-key symlink is never indexed and a swap produces no event for config.json.
        var monitor = ResilientFileSystemMonitor
            .Watch(root, "config.json")
            .WithHealthCheckInterval(TimeSpan.FromMilliseconds(100))
            .WithAuditInterval(TimeSpan.FromSeconds(1))
            .Build();
        _disposables.Add(monitor);

        var changed = new List<string>();
        var gate = new object();
        monitor.Changed += (_, e) => { lock (gate) changed.Add(e.Name!); };

        await Task.Delay(300);
        SwapConfigMapData(root, "revB", "BBBBBB", pinnedMtime);

        // Give the audit several cycles to (not) fire for config.json.
        await Task.Delay(TimeSpan.FromSeconds(4));

        lock (gate)
            Assert.DoesNotContain("config.json", changed);
    }

    [Fact]
    public async Task RegularFile_WithTracking_StillEmitsChangedEvents()
    {
        // Tracking on must not regress ordinary (non-symlinked) file change detection.
        if (!EnsureSymlinkCapability())
            return; // skipped on Windows without symlink privilege (not a k8s target platform)

        var root = NewRoot();
        var file = Path.Combine(root, "plain.txt");
        File.WriteAllText(file, "initial");

        var monitor = ResilientFileSystemMonitor
            .Watch(root, "*.txt")
            .WithSymlinkTargetTracking()
            .WithHealthCheckInterval(TimeSpan.FromMilliseconds(100))
            .WithAuditInterval(TimeSpan.FromSeconds(1))
            .Build();
        _disposables.Add(monitor);

        var changed = new List<string>();
        var gate = new object();
        monitor.Changed += (_, e) => { lock (gate) changed.Add(e.Name!); };

        await Task.Delay(200);
        File.WriteAllText(file, "modified-content");

        await ActiveWaitHelpers.WaitUntilAsync(
            () => { lock (gate) return changed.Contains("plain.txt"); },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(50),
            "Changed event for a regular file with tracking enabled");

        lock (gate)
            Assert.Contains("plain.txt", changed);
    }

    [Fact]
    public void DanglingSymlink_WithTracking_DoesNotThrow()
    {
        if (!EnsureSymlinkCapability())
            return; // skipped on Windows without symlink privilege (not a k8s target platform)

        var root = NewRoot();
        // A symlink whose target does not exist (can occur transiently during a swap).
        File.CreateSymbolicLink(Path.Combine(root, "config.json"), Path.Combine("data", "config.json"));

        var monitor = ResilientFileSystemMonitor
            .Watch(root, "config.json")
            .WithSymlinkTargetTracking()
            .WithHealthCheckInterval(TimeSpan.FromMilliseconds(100))
            .WithAuditInterval(TimeSpan.FromSeconds(1))
            .Build();
        _disposables.Add(monitor);

        // Constructing + running the snapshot/audit over a dangling symlink must not throw.
        Assert.True(monitor.IsUsingWatcher);
    }

    // --- helpers -------------------------------------------------------------------------------

    private string NewRoot()
    {
        var root = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Builds a kubelet-style ConfigMap layout (using safe names; real k8s uses "..data" /
    /// "..&lt;timestamp&gt;"):
    /// <code>
    ///   config.json -> data/config.json   (per-key file symlink, stable)
    ///   data        -> revA               (intermediate dir symlink, the swapped one)
    ///   revA/config.json                  (the real file)
    /// </code>
    /// </summary>
    private static void BuildConfigMapLayout(string root, string content, DateTime mtimeUtc)
    {
        var revA = Path.Combine(root, "revA");
        Directory.CreateDirectory(revA);
        var realFile = Path.Combine(revA, "config.json");
        File.WriteAllText(realFile, content);
        File.SetLastWriteTimeUtc(realFile, mtimeUtc);

        Directory.CreateSymbolicLink(Path.Combine(root, "data"), revA);
        // Relative target, exactly as kubelet writes it.
        File.CreateSymbolicLink(Path.Combine(root, "config.json"), Path.Combine("data", "config.json"));
    }

    /// <summary>
    /// Performs the atomic-style update: writes a fresh revision (same length + same mtime to defeat
    /// metadata-only detection) and repoints the intermediate "data" symlink to it. The user-visible
    /// config.json symlink is never modified.
    /// </summary>
    private static void SwapConfigMapData(string root, string newRev, string content, DateTime mtimeUtc)
    {
        var revDir = Path.Combine(root, newRev);
        Directory.CreateDirectory(revDir);
        var realFile = Path.Combine(revDir, "config.json");
        File.WriteAllText(realFile, content);
        File.SetLastWriteTimeUtc(realFile, mtimeUtc);

        var dataLink = Path.Combine(root, "data");
        Directory.Delete(dataLink);
        Directory.CreateSymbolicLink(dataLink, revDir);
    }

    /// <summary>
    /// Symlink tests must really execute on the production-relevant platforms (Linux/macOS), where
    /// creating a symlink never needs elevation. On Windows it requires Developer Mode or an
    /// elevated process, and Windows is not a Kubernetes ConfigMap target — so a no-op skip is
    /// acceptable there but is NEVER allowed on Linux/macOS (that would be a false-green on the
    /// platform we actually ship to). Returns true if the test body should run.
    /// </summary>
    private static bool EnsureSymlinkCapability()
    {
        if (SymlinksSupported)
            return true;

        Assert.True(OperatingSystem.IsWindows(),
            "Symlink creation must be available on Linux/macOS test hosts, but it was not — " +
            "the symlink target-tracking tests would otherwise silently pass without testing anything.");

        return false; // Windows without symlink privilege: skip.
    }

    private static bool CheckSymlinkSupport()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"symlink_cap_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "t.txt");
            File.WriteAllText(target, "x");
            var link = Path.Combine(dir, "l.txt");
            File.CreateSymbolicLink(link, target);
            return File.Exists(link);
        }
        catch
        {
            return false;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
