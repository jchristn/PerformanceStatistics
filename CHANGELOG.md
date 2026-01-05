# Change Log

## Current Version

v2.0.0

Major release adding cross-platform support for Linux and macOS.

### New Features

- **Cross-platform support**: Now works on Windows, Linux, and macOS
- **Factory pattern**: `PerformanceStatisticsFactory.Create()` automatically detects platform and creates appropriate implementation
- **New interfaces**: `IPerformanceStatistics`, `ISystemCounters`, `IProcessCounters` for platform-agnostic code
- **Platform detection**: `PerformanceStatisticsFactory.CurrentPlatform` and `IsPlatformSupported` properties
- **`PlatformTypeEnum` enum**: Explicit platform identification (`Windows`, `Linux`, `Mac`, `Unknown`)
- **Automated test suite**: `Test.Automated` project with 54 comprehensive tests

### Platform Implementations

- **Linux**: Uses `/proc` filesystem (`/proc/stat`, `/proc/meminfo`, `/proc/diskstats`, `/proc/[pid]/stat`)
- **macOS**: Uses system commands (`top`, `vm_stat`, `iostat`) and `DriveInfo`
- **Windows**: Unchanged from v1.x, uses Windows Performance Counters

### Platform-Specific Limitations

**macOS:**
- `TotalDiskReadQueue` and `TotalDiskWriteQueue` return `null` (not available)
- `TotalDiskReadOperations` and `TotalDiskWriteOperations` are approximated from combined disk transfers
- `HandleCount` returns `0` (not tracked)
- `PrivateMemory` and `PeakWorkingSetMemory` may return `0`
- `ProcessTitle` returns empty string (no window concept)
- `NonPagedSystemMemory`, `PagedSystemMemory`, `PeakPagedSystemMemory` return `null`

**Linux:**
- `TotalDiskReadQueue` and `TotalDiskWriteQueue` are approximated from I/O in progress
- `HandleCount` returns file descriptor count from `/proc/[pid]/fd`
- `ProcessTitle` returns empty string (no window concept)
- `NonPagedSystemMemory`, `PagedSystemMemory`, `PeakPagedSystemMemory` return `null`

**CPU Metrics Note:**
- CPU utilization requires two samples to calculate
- First call returns `0`; subsequent calls return actual percentage
- Allow ~100ms between samples for accurate readings

### Breaking Changes

- **Namespace change**: Windows classes moved to `PerformanceStatistics.Windows` namespace
  - Add `using PerformanceStatistics.Windows;` to existing code using `WindowsPerformanceStatistics` directly
  - No API changes to the classes themselves

### Migration Guide

**For existing Windows-only code** using `WindowsPerformanceStatistics` directly, add the namespace:

```csharp
// Before (v1.x)
using PerformanceStatistics;
using var stats = new WindowsPerformanceStatistics();

// After (v2.0.0)
using PerformanceStatistics.Windows;  // Add this namespace
using var stats = new WindowsPerformanceStatistics();
```

**To adopt cross-platform support:**

```csharp
// Cross-platform (v2.0.0)
using PerformanceStatistics;
using var stats = PerformanceStatisticsFactory.Create();
double? cpu = stats.System.CpuUtilizationPercent; // Note: nullable type
```

### File Structure

All platform implementations are now organized in their own namespaces:

- `Interfaces/IPerformanceStatistics.cs`
- `Interfaces/ISystemCounters.cs`
- `Interfaces/IProcessCounters.cs`
- `PerformanceStatisticsFactory.cs`
- `PlatformTypeEnum.cs`
- `Windows/WindowsPerformanceStatistics.cs` (moved, implements interfaces)
- `Windows/WindowsSystemCounters.cs` (moved, implements interfaces)
- `Windows/WindowsProcessCounters.cs` (moved, implements interfaces)
- `Linux/LinuxPerformanceStatistics.cs`
- `Linux/LinuxSystemCounters.cs`
- `Linux/LinuxProcessCounters.cs`
- `Linux/ProcFileParser.cs`
- `Mac/MacPerformanceStatistics.cs`
- `Mac/MacSystemCounters.cs`
- `Mac/MacProcessCounters.cs`
- `Mac/MacSystemInfo.cs`

---

## Previous Versions

### v1.1.6

- Implemented `IDisposable` interface on `WindowsPerformanceStatistics`, `WindowsProcessCounters`, and `WindowsSystemCounters`
- Fixed critical memory leak: `PerformanceCounter` objects in `WindowsSystemCounters` are now properly disposed after each use
- Fixed critical memory leak: `PerformanceCounter` object in `WindowsProcessCounters.CpuUtilizationPercent` is now properly disposed
- Fixed memory leak: `Process` objects from `Process.GetProcessesByName()` are now properly disposed when `MonitoredProcesses` is refreshed or when the parent object is disposed
- Added thread-safe caching for `MonitoredProcesses` property with automatic cleanup of previous process handles
- All classes now follow the standard `IDisposable` pattern with `Dispose(bool disposing)` for proper resource cleanup

### v1.1.5

- Package maintenance release

### v1.1.x

- Added monitored processes feature
- Added `MonitoredProcessNames` property to specify which processes to track
- Added `MonitoredProcesses` property returning detailed statistics for each monitored process
- Added `WindowsProcessCounters` class with CPU, memory, thread, and handle metrics per process

### v1.0.x

- Initial release
- Windows platform support only
- System-wide performance counters (CPU, memory, disk)
- Active TCP connection monitoring
- `WindowsPerformanceStatistics` main entry point class
- `WindowsSystemCounters` for system-level metrics
