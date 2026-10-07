# Change Log

## Current Version

v2.2.0

Task-manager metrics and process actions, on the v2.1.0 sampling model, for the apps planned in USER_APPS.md. Additive: existing members are unchanged. Interfaces gained members, so code that implements `IPerformanceStatistics`, `ISystemCounters`, or `IProcessCounters` itself must add them.

### New

- **`IPerformanceStatistics.GetAllProcesses()`**: every visible process, ordered by PID. A running process keeps its counters across calls, like `MonitoredProcesses`.
- **`IPerformanceStatistics.SystemInfo`**: machine, platform, OS, architecture, CPU model, logical processors, total memory, and boot time.
- **New process fields**: `ParentProcessId`, `ExecutablePath`, `CommandLine`, `UserName`, `StartTimeUtc`, `State` (`ProcessStateEnum`), `Priority` (`ProcessPriorityEnum`), `TotalProcessorTime`, `DiskRead/WriteBytesPerSecond`, and `DiskRead/WriteBytesTotal`.
- **New system series**:
  - CPU and memory: `PerCoreCpuUtilizationPercent`, `MemoryTotalMegabytes`, `MemoryUsedMegabytes`, `SwapTotalMegabytes`, `SwapUsedMegabytes`.
  - Disk and network: `TotalDiskRead/WriteBytesPerSecond`, `NetworkReceived/SentBytesPerSecond`, `NetworkInterfaces` (per-interface bytes and packets), `Volumes`.
  - Load: `LoadAverage1/5/15`.
- **`ProcessActions`**: `Kill(pid, entireProcessTree)`, `SetPriority`, `Suspend`, and `Resume`. They return `ProcessActionResultEnum` and never throw.
- **`PERFSTATS_HOST_ROOT`**: on Linux, read `<root>/proc` and `<root>/sys`, so an agent in a container can report its host.

### Per-platform sources

| Field | Windows | Linux | macOS |
|---|---|---|---|
| Executable path | `QueryFullProcessImageName` | `/proc/<pid>/exe` | `proc_pidpath` |
| Command line | `NtQueryInformationProcess(ProcessCommandLineInformation)` | `/proc/<pid>/cmdline` | `KERN_PROCARGS2` |
| Parent, state, user | basic information, thread states, token | `/proc/<pid>/stat`, `status` | `PROC_PIDT_SHORTBSDINFO` |
| Disk I/O bytes | `GetProcessIoCounters` (all I/O) | `/proc/<pid>/io` (storage) | `proc_pid_rusage` (storage) |
| Per-core CPU | `NtQuerySystemInformation` | `/proc/stat` | `host_processor_info` |
| Swap | paging file beyond RAM | `SwapTotal`/`SwapFree` | `vm.swapusage` |

### Privilege notes

- On macOS, CPU, disk I/O, handle count, footprint, and command line for **other users' processes** (root and system daemons) are null unless running elevated. macOS restricts task information, and Activity Monitor uses a privileged helper for the same reason.
- On Linux, `/proc/<pid>/io` for other users' processes needs elevation.

---

## Previous Versions

### v2.1.0

Reliability and consistency release: every metric now means the same thing, and is measured the same way, on Windows, Linux, and macOS.

#### Architecture

- **Shared base classes**: `PerformanceStatisticsBase`, `SystemCountersBase`, and `ProcessCountersBase` own sampling, rounding, units, process tracking, formatting, and disposal. The platform classes (`Windows*`, `Linux*`, `Mac*`) supply only raw readings, so a fix now lands once instead of three times.
- **One sampling model for rates** (`Internal/DeltaSampler`): CPU and disk operation rates are averages over the interval since the previous read, timed with a monotonic clock. The first read waits at most ~100ms for a second sample instead of returning a placeholder `0`. Reads less than 100ms apart return the previous value instead of resetting the baseline.
- `[SupportedOSPlatform]` annotations on the platform classes (net8.0+). Constructing another OS's system counters or statistics now throws `PlatformNotSupportedException` (a `NotSupportedException`) at construction rather than from individual getters.
- Removed the unused `System.Text.Json` package dependency.
- Added a GitHub Actions matrix (`.github/workflows/ci.yml`) running the full suite on Windows, Linux, and macOS for net8.0 and net10.0.

#### Fixes

- **Process CPU was always 0 on Linux and macOS** when read through `MonitoredProcesses`, because every access disposed and recreated the counters. Running processes now keep their `IProcessCounters` instance across accesses (refreshed with current values), so CPU is measured between accesses. Counters are disposed only when the process exits or its name is removed.
- **Duplicate names in `MonitoredProcessNames` threw `ArgumentException`**; they now collapse into one key.
- **macOS disk space was multiplied** by the number of APFS volumes in the root container (e.g. 18.6TB reported for a 3.7TB disk). Only `/` and `/Volumes/*` are counted, and volumes reporting the same filesystem numbers are counted once (this also fixes Linux bind mounts).
- **macOS free memory was under-reported** (free + speculative + purgeable pages only, e.g. 6.9GB of 57GB actually available). It is now physical memory minus app, wired, and compressed memory, matching Activity Monitor and comparable to Linux `MemAvailable`.
- **macOS page-size fallback** of 4KB under-reported memory 4x on Apple Silicon; page size is now read via `sysctlbyname`.
- **Linux disk read/write operations**: reading read ops then write ops back-to-back returned 0 for the second and reset the sample. NVMe partitions (`nvme0n1p1`) were double-counted with their disks.
- **Linux process CPU** assumed 100 clock ticks per second; it now uses `Process.TotalProcessorTime`.
- **Windows rate counters returned 0**: CPU, disk operations, and disk queues created a new `PerformanceCounter` per read and read it once.
- **Windows process CPU** used the per-name counter instance, so every process sharing a name (e.g. several `dotnet` processes) reported the first instance's CPU, and the value was not normalized (could exceed 100).
- **Culture-dependent output**: `ToString()` and Windows values formatted with `"N2"` then re-parsed; all formatting is now invariant.
- The interactive `Test` app was hard-wired to `WindowsPerformanceStatistics`; it now uses the factory.

#### Behavior changes

| Member | Before | Now |
|--------|--------|-----|
| Process `CpuUtilizationPercent` (Windows) | Per-name counter, 0-100 per core (could exceed 100) | Process CPU time / wall time / logical processors: 0-100 of machine capacity, same as Linux and macOS |
| `MonitoredProcesses` | New counters on every access; previous ones disposed | Same instance per running process; disposed when the process exits or its name is removed |
| System `CpuUtilizationPercent`, disk operations | First read `0` | First read waits up to ~100ms and returns a measured value |
| Windows system CPU / memory | Performance counters | `GetSystemTimes` / `GlobalMemoryStatusEx` (no localized counter names) |
| Windows disk operations / queues | `LogicalDisk(_Total)` | `PhysicalDisk(_Total)`, counters reused across reads |
| Windows disk space | `LogicalDisk` counters | Fixed drives via `DriveInfo`, the same as the other platforms |
| macOS `MemoryFreeMegabytes` | free + speculative + purgeable | available memory as Activity Monitor computes it (much larger) |
| macOS disk operations | `iostat` transfers / 2 for both reads and writes (blocked ~1s per read) | Separate read and write counts from IOKit (`ioreg`, ~20ms) |
| macOS process `HandleCount` | `0` | Open file descriptors; `null` for other users' processes |
| macOS process `PrivateMemory` | `0` | Physical footprint; `null` for other users' processes |
| macOS `PeakWorkingSetMemory`, `PeakVirtualSystemMemory` | `0` | `null` (not tracked) |
| Linux `TotalDiskReadQueue` / `TotalDiskWriteQueue` | I/O in progress / 2 for both | Separate in-flight reads and writes from `/sys/block/<disk>/inflight` |
| Linux process `HandleCount` when unreadable | `0` | `null` |
| Linux disk space | Fixed and network filesystems | Fixed filesystems only (a stale network mount could hang) |

### v2.0.1

- Fixed `IndexOutOfRangeException` in `ToString()` when the active TCP connection table changed during enumeration.
- Dependency updates; Touchstone-based test infrastructure (`Test.Shared`, `Test.Automated`, `Test.Xunit`, `Test.Nunit`).

### v2.0.0

Major release adding cross-platform support for Linux and macOS.

#### New Features

- **Cross-platform support**: Now works on Windows, Linux, and macOS
- **Factory pattern**: `PerformanceStatisticsFactory.Create()` automatically detects platform and creates appropriate implementation
- **New interfaces**: `IPerformanceStatistics`, `ISystemCounters`, `IProcessCounters` for platform-agnostic code
- **Platform detection**: `PerformanceStatisticsFactory.CurrentPlatform` and `IsPlatformSupported` properties
- **`PlatformTypeEnum` enum**: Explicit platform identification (`Windows`, `Linux`, `Mac`, `Unknown`)
- **Automated test suite**: `Test.Automated` project with 54 comprehensive tests

#### Platform Implementations

- **Linux**: Uses `/proc` filesystem (`/proc/stat`, `/proc/meminfo`, `/proc/diskstats`, `/proc/[pid]/stat`)
- **macOS**: Uses system commands (`top`, `vm_stat`, `iostat`) and `DriveInfo`
- **Windows**: Unchanged from v1.x, uses Windows Performance Counters

#### Platform-Specific Limitations

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

#### Breaking Changes

- **Namespace change**: Windows classes moved to `PerformanceStatistics.Windows` namespace
  - Add `using PerformanceStatistics.Windows;` to existing code using `WindowsPerformanceStatistics` directly
  - No API changes to the classes themselves

#### Migration Guide

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

#### File Structure

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
