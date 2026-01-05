# Change Log

## Current Version

v1.1.6

- Implemented `IDisposable` interface on `WindowsPerformanceStatistics`, `WindowsProcessCounters`, and `WindowsSystemCounters`
- Fixed critical memory leak: `PerformanceCounter` objects in `WindowsSystemCounters` are now properly disposed after each use
- Fixed critical memory leak: `PerformanceCounter` object in `WindowsProcessCounters.CpuUtilizationPercent` is now properly disposed
- Fixed memory leak: `Process` objects from `Process.GetProcessesByName()` are now properly disposed when `MonitoredProcesses` is refreshed or when the parent object is disposed
- Added thread-safe caching for `MonitoredProcesses` property with automatic cleanup of previous process handles
- All classes now follow the standard `IDisposable` pattern with `Dispose(bool disposing)` for proper resource cleanup

### Breaking Changes

None. Existing code will continue to work, but consumers should update to use `using` statements or call `Dispose()` to benefit from the memory leak fixes.

### Migration Guide

Update your code to properly dispose of `WindowsPerformanceStatistics` instances:

```csharp
// Before (v1.1.5 and earlier) - still works but may leak resources
var stats = new WindowsPerformanceStatistics();
Console.WriteLine(stats.System.CpuUtilizationPercent);

// After (v1.1.6+) - recommended pattern
using var stats = new WindowsPerformanceStatistics();
Console.WriteLine(stats.System.CpuUtilizationPercent);
```

## Previous Versions

v1.1.5

- Package maintenance release

v1.1.x

- Added monitored processes feature
- Added `MonitoredProcessNames` property to specify which processes to track
- Added `MonitoredProcesses` property returning detailed statistics for each monitored process
- Added `WindowsProcessCounters` class with CPU, memory, thread, and handle metrics per process

v1.0.x

- Initial release
- Windows platform support only
- System-wide performance counters (CPU, memory, disk)
- Active TCP connection monitoring
- `WindowsPerformanceStatistics` main entry point class
- `WindowsSystemCounters` for system-level metrics
