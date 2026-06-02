# Symlink Target Tracking

`WithSymlinkTargetTracking()` makes the monitor detect when a watched **symlink's resolved target changes** — even though the symlink itself is never modified. This enables hot-reload of Kubernetes **ConfigMap** and **Secret** volume mounts.

## The Problem

When Kubernetes mounts a ConfigMap or Secret as a volume, each key is a **symlink**, and updates are applied by an **atomic swap** of an intermediate `..data` symlink — the file you watch is never rewritten:

```
/etc/config/
  config.json   ->  ..data/config.json           (per-key symlink — created once, never touched)
  ..data        ->  ..2025_11_15_10_04_05.123…    (symlink — its TARGET is atomically swapped on update)
  ..2025_11_15_10_04_05.123…/config.json          (the real file)
```

On update, kubelet writes a new timestamped directory and atomically repoints `..data`. The watched `config.json` keeps the same name, and its resolved target can even have the same size and timestamp — so a metadata-based watcher sees nothing change.

## Enabling

```csharp
var monitor = ResilientFileSystemMonitor
    .Watch("/etc/config", "config.json")
    .WithSymlinkTargetTracking()
    .OnChanged((s, e) =>
    {
        // Fires when the ConfigMap is updated — keyed on the user-visible path.
        Console.WriteLine($"Config changed: {e.FullPath}");
        ReloadConfig();
    })
    .Build();
```

Off by default. When disabled, symlinks (reparse points) are skipped entirely, exactly as before.

## How It Works

When enabled, the monitor indexes symlink (reparse-point) entries instead of skipping them, and resolves each one to its **canonical final target**, folding that target into the change fingerprint. When the `..data` swap repoints the symlink, the fingerprint changes and a synthetic `Changed` event is emitted **on the user-visible path** (e.g. `config.json`) — so your existing handler and filter need no special cases.

Detection happens on the resilient reconcile paths:

- **Watching state:** the audit timer (`AuditInterval`, default 60s) detects the swap.
- **Polling fallback:** the polling tick (`PollingInterval`, default 5s) detects it if the native watcher is unavailable.

Lower `WithAuditInterval(...)` if you need faster reaction — though kubelet itself only propagates ConfigMap updates every tens of seconds, so it is usually the slower link.

::: tip Cross-platform & safety
Only the **final target** is resolved — the monitor never recurses into it, so there is no symlink-loop risk. Resolution runs **only for symlink entries**, so ordinary files incur no extra cost. On Linux/containers it additionally canonicalizes the target's parent directory, because `File.ResolveLinkTarget` does not resolve intermediate directory symlinks there — the same approach Go's `viper` uses for ConfigMap reload.
:::

## When to Use

**Enable when:**
- Reading configuration from a Kubernetes ConfigMap/Secret volume mount and you want hot-reload
- Watching any file that is updated by an atomic symlink swap (a common deployment pattern)

**Don't enable when:**
- You only watch ordinary files (no symlinks) — it adds nothing
- Using a `subPath` ConfigMap mount — those are plain files that Kubernetes does **not** update in place (a pod restart is required), so there is nothing to detect

## Example: Kubernetes ConfigMap hot-reload

```csharp
// Pod spec mounts a ConfigMap at /etc/config (whole-volume mount, not subPath).
var monitor = ResilientFileSystemMonitor
    .Watch("/etc/config", "appsettings.json")
    .WithSymlinkTargetTracking()
    .OnChanged((s, e) =>
    {
        _logger.LogInformation("ConfigMap updated: {Path}", e.FullPath);
        _settings = LoadSettings(e.FullPath);
    })
    .Build();

// When the ConfigMap is updated (kubectl apply / edit):
// → kubelet atomically swaps ..data
// → Changed event: /etc/config/appsettings.json
```

## Options Pattern

```csharp
var monitor = new ResilientFileSystemMonitor(
    new ResilientFileSystemMonitor.Options
    {
        Path = "/etc/config",
        Filter = "config.json",
        TrackSymlinkTargets = true,
    });
```
