# Test Coverage Summary

## Overview
The Cocoar.FileSystem library has comprehensive test coverage across all major components and platforms (Windows, Linux, macOS).

**Total Tests: 48**
- ResilientFileSystemMonitor: 28 tests
- FileSearcher: 20 tests

---

## ResilientFileSystemMonitor Tests (28)

### Constructor Validation (5 tests)
- `Constructor_WithNullOptions_ShouldThrow`
- `Constructor_WithNullPath_ShouldThrow`
- `Constructor_WithEmptyPath_ShouldThrow`
- `Constructor_WithWhitespacePath_ShouldThrow`
- `Constructor_WithValidOptions_ShouldNotThrow`

### Basic File Events - Steady State (4 tests)
- `FileCreation_ShouldTriggerCreatedEvent` - Tests file creation detection
- `FileChange_ShouldTriggerChangedEvent` - Tests file modification detection
- `FileDelete_ShouldTriggerDeletedEvent` - Tests file deletion detection
- `FileRenamed_ShouldTriggerRenamedEvent` - Tests file rename detection

### Filter & Subdirectories (3 tests)
- `Filter_OnlyMatchingFiles_ShouldTriggerEvents` - Tests file pattern filtering (*.txt, etc.)
- `IncludeSubdirectories_True_ShouldMonitorSubfolders` - Tests recursive monitoring
- `IncludeSubdirectories_False_ShouldNotMonitorSubfolders` - Tests non-recursive monitoring

### Debouncing (2 tests)
- `RapidFileChanges_WithoutDebouncing_ShouldReceiveAllEvents` - Tests lossless event delivery
- `RapidFileChanges_WithDebouncing_ShouldLimitEvents` - Tests debouncing reduces event flood

### Lossless Reconciliation - Core Feature (3 tests)
- `DirectoryDeleted_AndRecreatedWithFiles_ShouldDetectAllFiles` - Tests recovery with file reconciliation
- `DirectoryDeleted_AndRecreatedWithDifferentFiles_ShouldReconcile` - Tests differential reconciliation
- `FileModified_WhileInPollingMode_ShouldDetectChange` - Tests change detection during polling

### Health Check & Audit (2 tests)
- `HealthCheck_DirectoryDisappears_ShouldTransitionToPolling` - Tests directory deletion detection via health check
- `AuditInterval_DetectsSilentChanges_ShouldReconcile` - Tests metadata fingerprint auditing

### Polling Mode (2 tests)
- `PollingMode_ShouldDetectFileChanges` - Tests polling fallback when directory doesn't exist
- `PollingMode_DirectoryAppearsLater_ShouldSwitchToWatcher` - Tests automatic recovery to watcher mode

### Mode Changed Event (1 test)
- `ModeChanged_Event_ShouldProvideReasonAndMode` - Tests mode transition notifications

### Dispose (3 tests)
- `Constructor_WithExistingPath_ShouldStartWatcher` - Tests proper initialization
- `Dispose_ShouldCleanupResources` - Tests proper cleanup
- `Dispose_MultipleTimes_ShouldNotThrow` - Tests idempotent disposal

### Stress & Rapid Changes (2 tests)
- `MultipleFiles_RapidCreation_ShouldDetectAll` - Tests handling 20+ files created rapidly
- `Constructor_WithNonExistentPath_AndPollingEnabled_ShouldStartPolling` - Tests startup in polling mode

### Unified Event Stream API (1 test)
- `Events_ChannelReader_ShouldReceiveAllEventsInOrder` - Tests ChannelReader<FileSystemEvent> API

---

## FileSearcher Tests (20)

### Parameter Validation (5 tests)
- `EnumerateFiles_WithNullSearchPath_ShouldThrow`
- `EnumerateFiles_WithNullSearchPattern_ShouldThrow`
- `EnumerateFiles_WithEmptySearchPattern_ShouldThrow`
- `EnumerateFiles_WithWhitespaceSearchPattern_ShouldThrow`
- `EnumerateFiles_WithNonExistentDirectory_ShouldThrow`

### Basic Functionality (5 tests)
- `EnumerateFiles_WithEmptyDirectory_ShouldReturnEmpty`
- `EnumerateFiles_WithSingleFile_ShouldReturnThatFile`
- `EnumerateFiles_WithMultipleFiles_ShouldReturnAll`
- `EnumerateFiles_WithWildcardPattern_ShouldReturnAllFiles`
- `EnumerateFiles_WithComplexPattern_ShouldMatchCorrectly`

### Depth Control (3 tests)
- `EnumerateFiles_WithSubdirectories_NoMaxDepth_ShouldRecurseAll`
- `EnumerateFiles_WithMaxDepth_Zero_ShouldReturnOnlyRootFiles`
- `EnumerateFiles_WithMaxDepth_One_ShouldReturnRootAndFirstLevel`

### Exclusion (4 tests)
- `EnumerateFiles_WithExcludedFolders_ShouldSkipThem`
- `EnumerateFiles_WithNestedExcludedFolders_ShouldSkipEntireTree`
- `EnumerateFiles_WithNullExcludedFolders_ShouldIncludeAll`
- `EnumerateFiles_WithMultipleExcludedFolders_ShouldSkipAll`

### Performance (3 tests)
- `EnumerateFiles_WithDeepNesting_ShouldHandleEfficiently` - Tests 10 levels deep
- `EnumerateFiles_WithManyFiles_ShouldHandleEfficiently` - Tests 100 files
- `EnumerateFiles_IsLazyEvaluated_ShouldNotEnumerateAll` - Tests deferred execution

---

## Platform Coverage

All tests run on:
- ✅ **Windows** (latest)
- ✅ **Linux** (ubuntu-latest)
- ✅ **macOS** (latest)

---

## Key Features Tested

### 1. **Directory Identity Tracking** (FileSystemIdentity)
Indirectly tested through:
- Directory delete/recreate scenarios
- Health check transitions
- Automatic recovery tests

Platform-specific identity (Windows: FileId, Unix: inode) is verified through cross-platform CI.

### 2. **Lossless Event Delivery**
- Reconciliation after directory recreation
- Event ordering via Channel serialization
- No missed events during rapid file creation

### 3. **Resilience**
- Survives directory deletion
- Auto-recovery when directory reappears
- Graceful fallback to polling
- Health check detection of "zombie" watchers

### 4. **Performance**
- Debouncing to reduce event floods
- Efficient metadata fingerprint auditing
- Fast file enumeration with depth/exclusion control

### 5. **API Surface**
- Traditional .NET events (Created, Changed, Deleted, Renamed, Error, ModeChanged)
- Modern unified stream via `ChannelReader<FileSystemEvent>`
- Multiple concurrent consumers supported

---

## Test Utilities

### ActiveWaitHelpers
Custom test helpers to avoid flaky `Task.Delay` and improve test reliability:
- `WaitUntilAsync` - Polls condition until true or timeout
- `WaitForConditionAsync` - Alternative with custom polling interval

All file system tests use active waiting instead of fixed delays to handle platform timing differences.

---

## Known Platform Differences

Tests account for these platform-specific behaviors:
- **macOS**: FileSystemWatcher can "zombie" without raising Error when directory is deleted/recreated
- **Windows**: FileSystemWatcher typically raises Error on directory deletion
- **Linux**: Behavior varies by filesystem (ext4, btrfs, etc.)

Our tests validate the **resilient** behavior works correctly on all platforms despite these differences.
