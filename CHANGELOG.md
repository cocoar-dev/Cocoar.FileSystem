# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.2.0] - 2025-11-15

### Added
- **Folder Rename Detection** for `ResilientFileSystemMonitor`
  - Automatically emits `Renamed` events when directories containing matching files are renamed
  - No configuration needed - works out of the box for correctness
  - Uses high-performance `FileSearcher` for efficient directory enumeration
  - Respects `MaxDepth` and filter patterns when checking directory contents
  - Useful for certificate rotation scenarios where entire folders are atomically renamed (e.g., `kid2-staging` → `kid2`)
  - When a folder containing matching files is renamed, a single `Renamed` event is emitted with the folder path
- **API Harmonization** for `FileSearcher` (matches `ResilientFileSystemMonitor` API)
  - `.WithFilter(params string[])` - Search for multiple file patterns (e.g., `"*.cs", "*.csproj"`)
  - `.ClearFilters()` - Remove all configured patterns
  - `.IncludeSubdirectories(int maxDepth)` - Control recursion depth (0 = root only, -1 = unlimited)
  - `.IncludeSubdirectories(bool)` - Enable/disable recursion (true = unlimited, false = root only)
  - Additive behavior - calling `.WithFilter()` multiple times adds patterns
  - All existing methods remain unchanged - fully backward compatible

## [2.1.0] - 2025-11-13

### Added
- **Multiple File Pattern Support** for `ResilientFileSystemMonitor`
  - `.WithFilter(params string[] patterns)` - Monitor multiple file patterns (e.g., `"*.pfx", "*.p12", "*.cer"`)
  - Additive behavior - calling `.WithFilter()` multiple times adds patterns
  - `.ClearFilters()` - Remove all configured patterns
  - Uses `FileSystemName.MatchesSimpleExpression` for consistent pattern matching with `FileSearcher`
  - Supports DOS-style wildcards: `*` (any characters) and `?` (single character)
  - Filename-only matching (not full path) for performance

## [2.0.0] - 2025-11-12

### Added
- **Subdirectory Depth Control** for `ResilientFileSystemMonitor`
  - `.IncludeSubdirectories(int maxDepth)` - Monitor subdirectories up to a specific depth (0, 1, 2, etc.)
  - `.IncludeSubdirectories(bool include = true)` - Simple on/off (true = unlimited, false = root only)
  - Use `-1` for unlimited depth

### Changed
- **BREAKING**: Default behavior changed from recursive to non-recursive
  - `IncludeSubdirectories` now defaults to `false` (previously `true`)
  - To maintain previous behavior, explicitly call `.IncludeSubdirectories()`
  - This improves security and performance by making recursive watching opt-in

## [1.0.0] - 2025-11-05

### Added
- Initial stable release of Cocoar.FileSystem
- **ResilientFileSystemMonitor** - Production-ready FileSystemWatcher with automatic fallback and error recovery
  - Automatic switching between FileSystemWatcher (efficient) and polling (resilient)
  - Built-in debouncing support for reducing noise from rapid file changes
  - Periodic health checks to detect silent watcher failures (especially on macOS)
  - Directory identity tracking to detect delete+recreate scenarios
  - Fluent API for clean, discoverable configuration
  - Reactive event stream via `ChannelReader<FileSystemEvent>` for ordered event processing
  - Full support for all event types: Created, Changed, Deleted, Renamed, Error, ModeChanged
  - Multi-platform support (Windows, Linux, macOS)
- **FileSearcher** - High-performance file search utility
  - Fast, lazy-evaluated directory traversal
  - Fluent API for search configuration
  - Support for depth limits, exclusion patterns, and custom filters
  - Efficient handling of large directory trees
- **FileReader** - Secure file reading with shared access
  - Read files as byte arrays with `FileShare.ReadWrite` support
  - Works even when other processes have files open
  - Optional UTF-8 BOM stripping for text files
  - Try-pattern support (`TryReadAllBytes`) for graceful handling of missing files
  - Byte arrays can be zeroed out after use for sensitive content
- Comprehensive test suite with 98 tests across all major platforms
- Complete documentation and usage examples
- GitHub Actions CI/CD pipelines for all major platforms

[1.0.0]: https://github.com/cocoar-dev/Cocoar.FileSystem/releases/tag/v1.0.0
