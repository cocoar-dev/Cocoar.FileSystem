# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.3.0] - 2026-06-02

### Added
- **Symlink Target Tracking** (opt-in) for `ResilientFileSystemMonitor`
  - `.WithSymlinkTargetTracking()` — detects when a watched symlink's resolved target swaps
  - Enables hot-reload of Kubernetes ConfigMap/Secret mounts (atomic `..data` symlink swap)
  - Surfaced as a `Changed` event on the user-visible (symlinked) path
  - **Off by default** — existing behavior unchanged; detected on both the audit and polling-fallback paths
  - Only the final target is resolved (loop-safe); resolution runs only for symlink entries, so ordinary files incur no extra cost
  - Works consistently on Linux/containers and Windows (also canonicalizes the target's parent directory)

## [2.2.0] - 2025-11-15

### Added
- **Folder Rename Detection** for `ResilientFileSystemMonitor`
  - Automatically emits `Renamed` events when directories containing matching files are renamed
  - No configuration needed — works out of the box
  - Uses `FileSearcher` for efficient directory enumeration
  - Respects `MaxDepth` and filter patterns when checking directory contents
  - Useful for certificate rotation scenarios (e.g., `kid2-staging` → `kid2`)
- **API Harmonization** for `FileSearcher`
  - `.WithFilter(params string[])` — Multiple patterns
  - `.ClearFilters()` — Remove all patterns
  - `.IncludeSubdirectories(int maxDepth)` — Depth control
  - `.IncludeSubdirectories(bool)` — Enable/disable recursion
  - Additive behavior for patterns
  - Fully backward compatible

## [2.1.0] - 2025-11-13

### Added
- **Multiple File Pattern Support** for `ResilientFileSystemMonitor`
  - `.WithFilter(params string[] patterns)` — Monitor multiple file patterns
  - Additive behavior — calling `.WithFilter()` multiple times adds patterns
  - `.ClearFilters()` — Remove all configured patterns
  - Uses `FileSystemName.MatchesSimpleExpression` for consistent pattern matching
  - Supports DOS-style wildcards: `*` and `?`

## [2.0.0] - 2025-11-12

### Added
- **Subdirectory Depth Control** for `ResilientFileSystemMonitor`
  - `.IncludeSubdirectories(int maxDepth)` — Monitor up to a specific depth
  - `.IncludeSubdirectories(bool)` — Simple on/off toggle
  - `-1` for unlimited depth

### Changed
- **BREAKING**: Default behavior changed from recursive to non-recursive
  - `IncludeSubdirectories` now defaults to `false`
  - Call `.IncludeSubdirectories()` to restore previous behavior

## [1.0.0] - 2025-11-05

### Added
- **ResilientFileSystemMonitor** — Production-ready FileSystemWatcher with automatic fallback
  - Automatic switching between FileSystemWatcher and polling
  - Built-in debouncing
  - Periodic health checks for silent watcher failures
  - Directory identity tracking (delete + recreate detection)
  - Fluent builder API
  - Reactive event stream via `ChannelReader<FileSystemEvent>`
  - Full event support: Created, Changed, Deleted, Renamed, Error, ModeChanged
  - Multi-platform support (Windows, Linux, macOS)
- **FileSearcher** — High-performance file search
  - Lazy-evaluated directory traversal
  - Fluent API with depth limits, exclusion patterns, custom filters
- **FileReader** — Secure file reading
  - `FileShare.ReadWrite` support
  - Optional UTF-8 BOM stripping
  - Try-pattern (`TryReadAllBytes`)
  - Byte arrays for secure memory handling
- 98+ tests across all platforms
- Zero external dependencies
