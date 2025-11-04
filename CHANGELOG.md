# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of Cocoar.FileSystem
- `ResilientFileSystemMonitor` - Production-ready FileSystemWatcher with automatic fallback and error recovery
- Automatic switching between FileSystemWatcher (efficient) and polling (resilient)
- Built-in debouncing support for reducing noise from rapid file changes
- Multi-platform support (Windows, Linux, macOS)

[Unreleased]: https://github.com/cocoar-dev/Cocoar.FileSystem/compare/v0.1.0...HEAD
