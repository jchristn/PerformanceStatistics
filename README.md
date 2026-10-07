# PerformanceStatistics

<img src="https://github.com/jchristn/PerformanceStatistics/blob/main/Assets/logo.png?raw=true" width="100" height="100" />

A lightweight cross-platform .NET library for capturing real-time performance statistics. Monitor system resources like CPU, memory, and disk usage, as well as detailed metrics for specific processes on Windows, Linux, and macOS.

[![NuGet Version](https://img.shields.io/nuget/v/PerformanceStatistics.svg?style=flat)](https://www.nuget.org/packages/PerformanceStatistics/) [![NuGet](https://img.shields.io/nuget/dt/PerformanceStatistics.svg)](https://www.nuget.org/packages/PerformanceStatistics)

## Why Use This Library?

- **Cross-Platform**: Works on Windows, Linux, and macOS
- **Simple API**: Get system and process performance metrics with just a few lines of code
- **Real-time Monitoring**: Access live CPU, memory, disk, and network statistics
- **Process Tracking**: Monitor specific processes by name with detailed metrics including memory usage, thread counts, and handle counts
- **TCP Connection Visibility**: View active TCP connections with local and remote endpoints
- **Background Service Integration**: Ideal for health monitoring, performance dashboards, and automated alerting systems

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| Windows | Full Support | Metrics via kernel32 (`GetSystemTimes`, `GlobalMemoryStatusEx`) and PhysicalDisk performance counters |
| Linux | Full Support | Metrics from `/proc` and `/sys` |
| macOS | Full Support | Metrics via native APIs (`host_statistics`, `sysctl`, `libproc`) and IOKit statistics |

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v2.1.0

- **Consistent semantics on every platform**: shared base classes own sampling, units, rounding, and formatting; platform classes only supply raw readings
- **Process CPU that works**: running processes keep their counters across `MonitoredProcesses` accesses, so CPU is measured between reads, normalized to 0-100 of machine capacity everywhere
- **macOS accuracy**: native APIs replace `top`/`vm_stat`/`iostat` (about 10x faster), with correct available memory, de-duplicated APFS disk space, and real file descriptor and memory footprint values
- **Windows accuracy**: CPU and memory via kernel32, reused disk counters, per-PID process CPU
- **CI on Windows, Linux, and macOS**

See [CHANGELOG.md](CHANGELOG.md) for fixes and behavior changes.

## New in v2.0.0

- **Cross-platform support** for Windows, Linux, and macOS
- New `PerformanceStatisticsFactory` for automatic platform detection
- New interfaces: `IPerformanceStatistics`, `ISystemCounters`, `IProcessCounters`
- Full backward compatibility with existing Windows-specific classes
- `PlatformTypeEnum` enum for explicit platform identification

## Example Projects

- `Test` - Interactive console application for manual testing
- `Test.Shared` - Touchstone test suites (the single source of truth for tests)
- `Test.Automated` - Console runner for the shared suites (`dotnet run --project src/Test.Automated -f net10.0`)
- `Test.Xunit` / `Test.Nunit` - The same suites under `dotnet test`

## Getting Started

### Cross-Platform Usage (Recommended)

```csharp
using PerformanceStatistics;

// Factory automatically detects platform and creates appropriate implementation
using var stats = PerformanceStatisticsFactory.Create();

Console.WriteLine($"Platform: {PerformanceStatisticsFactory.CurrentPlatform}");
Console.WriteLine($"CPU: {stats.System.CpuUtilizationPercent ?? 0}%");
Console.WriteLine($"Free RAM: {stats.System.MemoryFreeMegabytes ?? 0} MB");

// Add processes to monitor
stats.MonitoredProcessNames.Add("dotnet");

foreach (var kvp in stats.MonitoredProcesses)
{
    foreach (var proc in kvp.Value)
    {
        Console.WriteLine($"  {proc.ProcessName} (PID {proc.ProcessId}): {proc.WorkingSetMemory / 1024 / 1024} MB");
    }
}
```

### Windows-Specific Usage

```csharp
using PerformanceStatistics.Windows;

// Direct Windows implementation
using var stats = new WindowsPerformanceStatistics();

Console.WriteLine($"CPU: {stats.System.CpuUtilizationPercent}%");
Console.WriteLine($"Free RAM: {stats.System.MemoryFreeMegabytes} MB");
```

### Checking Platform Support

```csharp
using PerformanceStatistics;

if (PerformanceStatisticsFactory.IsPlatformSupported)
{
    Console.WriteLine($"Running on: {PerformanceStatisticsFactory.CurrentPlatform}");
    using var stats = PerformanceStatisticsFactory.Create();
    // Use stats...
}
else
{
    Console.WriteLine("Platform not supported");
}
```

### Accessing System Counters

```csharp
using var stats = PerformanceStatisticsFactory.Create();

// CPU (average since the previous read; the first read waits ~100ms for a second sample)
double? cpuPercent = stats.System.CpuUtilizationPercent;

// Memory
double? freeMemoryMB = stats.System.MemoryFreeMegabytes;

// Disk
int? diskReads = stats.System.TotalDiskReadOperations;
int? diskWrites = stats.System.TotalDiskWriteOperations;
int? diskReadQueue = stats.System.TotalDiskReadQueue;   // null on macOS
int? diskWriteQueue = stats.System.TotalDiskWriteQueue; // null on macOS
double? diskFreePercent = stats.System.TotalDiskFreePercent;
double? diskFreeMB = stats.System.TotalDiskFreeMegabytes;
double? diskSizeMB = stats.System.TotalDiskSizeMegabytes;
double? diskUsedMB = stats.System.TotalDiskUsedMegabytes;
```

### Monitoring Specific Processes

```csharp
using var stats = PerformanceStatisticsFactory.Create();
stats.MonitoredProcessNames.Add("dotnet");
stats.MonitoredProcessNames.Add("node");

foreach (var kvp in stats.MonitoredProcesses)
{
    string processName = kvp.Key;

    foreach (var process in kvp.Value)
    {
        Console.WriteLine($"Process: {process.ProcessName} (PID: {process.ProcessId})");
        Console.WriteLine($"  CPU: {process.CpuUtilizationPercent ?? 0}%");
        Console.WriteLine($"  Working Set: {process.WorkingSetMemory / (1024 * 1024)} MB");
        Console.WriteLine($"  Threads: {process.ThreadCount}");
        Console.WriteLine($"  Handles: {process.HandleCount}"); // handles on Windows, file descriptors elsewhere
    }
}
```

### Monitoring TCP Connections

```csharp
using var stats = PerformanceStatisticsFactory.Create();

// Get all active TCP connections
var allConnections = stats.ActiveTcpConnections;
Console.WriteLine($"Total TCP connections: {allConnections.Length}");

// Filter by port
var httpConnections = stats.GetActiveTcpConnections(sourcePort: 80);
var httpsConnections = stats.GetActiveTcpConnections(sourcePort: 443);

// Filter by source and destination port
var specificConnections = stats.GetActiveTcpConnections(sourcePort: 8080, destPort: 443);
```

### Integration with Background Services

```csharp
public class PerformanceMonitorService : IHostedService, IDisposable
{
    private IPerformanceStatistics _stats;
    private Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stats = PerformanceStatisticsFactory.Create();
        _stats.MonitoredProcessNames.Add("MyApp");

        _timer = new Timer(CheckPerformance, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    private void CheckPerformance(object state)
    {
        var cpu = _stats.System.CpuUtilizationPercent;
        var memory = _stats.System.MemoryFreeMegabytes;

        if (cpu.HasValue && cpu > 90)
        {
            // Alert: High CPU usage
        }

        if (memory.HasValue && memory < 500)
        {
            // Alert: Low memory
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _stats?.Dispose();
    }
}
```

## Sampling Semantics

- **Rates are averages since the previous read.** System CPU, disk operations, and process CPU compare the current raw counters with those from the previous read of the same object. Poll on a fixed interval (for example once per second) for steady values.
- **The first read is measured, not zero.** It waits up to ~100ms for a second sample (on macOS, until the kernel publishes new CPU ticks).
- **Reads closer than 100ms** return the previous value rather than resetting the measurement, so reading several properties back-to-back is consistent.
- **Process counters persist.** `MonitoredProcesses` re-scans process names on each access but returns the same `IProcessCounters` instance for a process that is still running, with refreshed memory values. Read it once per polling pass.
- **Null means unavailable.** A metric the platform does not expose, or a process you lack rights to inspect, returns `null`. Counters never throw from getters.

## Platform-Specific Metric Availability

### System Counters

| Metric | Windows | Linux | macOS | Source |
|--------|:-------:|:-----:|:-----:|--------|
| `CpuUtilizationPercent` | ✓ | ✓ | ✓ | `GetSystemTimes` / `/proc/stat` / `host_statistics` |
| `MemoryFreeMegabytes` | ✓ | ✓ | ✓ | Available physical / `MemAvailable` / memsize minus app+wired+compressed (Activity Monitor) |
| `TotalDiskReadOperations` | ✓ | ✓ | ✓ | PhysicalDisk counters / `/proc/diskstats` / IOKit block storage statistics |
| `TotalDiskWriteOperations` | ✓ | ✓ | ✓ | as above |
| `TotalDiskReadQueue` | ✓ | ✓ | null | Avg. Disk Read Queue Length / `/sys/block/*/inflight` |
| `TotalDiskWriteQueue` | ✓ | ✓ | null | Avg. Disk Write Queue Length / `/sys/block/*/inflight` |
| `TotalDiskFreePercent` | ✓ | ✓ | ✓ | Fixed drives; filesystems reporting identical numbers (APFS volumes, bind mounts) counted once |
| `TotalDiskFreeMegabytes` | ✓ | ✓ | ✓ | as above |
| `TotalDiskSizeMegabytes` | ✓ | ✓ | ✓ | as above |
| `TotalDiskUsedMegabytes` | ✓ | ✓ | ✓ | as above |

### Process Counters

| Metric | Windows | Linux | macOS | Notes |
|--------|:-------:|:-----:|:-----:|-------|
| `ProcessId` | ✓ | ✓ | ✓ | |
| `ProcessName` | ✓ | ✓ | ✓ | |
| `ProcessTitle` | ✓ | empty | empty | No window concept on Linux/macOS |
| `MachineName` | ✓ | ✓ | ✓ | |
| `CpuUtilizationPercent` | ✓ | ✓ | ✓ | 0-100 of total machine capacity (all cores) |
| `HandleCount` | ✓ | ✓ fd | ✓ fd | Linux/macOS: open file descriptors; null for other users' processes |
| `ThreadCount` | ✓ | ✓ | ✓ | |
| `PrivateMemory` | ✓ | ✓ | ✓ | macOS: physical footprint (Activity Monitor "Memory"); null for other users' processes |
| `VirtualMemory` | ✓ | ✓ | ✓ | |
| `WorkingSetMemory` | ✓ | ✓ | ✓ | |
| `NonPagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PeakPagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PeakVirtualSystemMemory` | ✓ | ✓ | null | Not tracked by macOS |
| `PeakWorkingSetMemory` | ✓ | ✓ | null | Not tracked by macOS |

**Legend:**
- ✓ = Supported
- fd = File descriptor count
- null = Returns null (metric not available)
- empty = Returns empty string

## API Reference

### PerformanceStatisticsFactory (Static)

| Member | Type | Description |
|--------|------|-------------|
| `Create(List<string>)` | `IPerformanceStatistics` | Creates platform-appropriate instance |
| `CurrentPlatform` | `PlatformTypeEnum` | Gets the current platform type |
| `IsPlatformSupported` | `bool` | Whether the current platform is supported |

### IPerformanceStatistics

| Property | Type | Description |
|----------|------|-------------|
| `System` | `ISystemCounters` | System-wide performance counters |
| `MonitoredProcessNames` | `List<string>` | List of process names to monitor |
| `MonitoredProcesses` | `Dictionary<string, List<IProcessCounters>>` | Performance data for monitored processes |
| `ActiveTcpConnections` | `TcpConnectionInformation[]` | Array of active TCP connections |

### ISystemCounters

| Property | Type | Description |
|----------|------|-------------|
| `CpuUtilizationPercent` | `double?` | Current CPU usage percentage (0-100) |
| `MemoryFreeMegabytes` | `double?` | Available memory in MB |
| `TotalDiskReadOperations` | `int?` | Disk reads per second |
| `TotalDiskWriteOperations` | `int?` | Disk writes per second |
| `TotalDiskReadQueue` | `int?` | Average disk read queue length |
| `TotalDiskWriteQueue` | `int?` | Average disk write queue length |
| `TotalDiskFreePercent` | `double?` | Percentage of free disk space |
| `TotalDiskFreeMegabytes` | `double?` | Free disk space in MB |
| `TotalDiskSizeMegabytes` | `double?` | Total disk size in MB |
| `TotalDiskUsedMegabytes` | `double?` | Used disk space in MB |

### IProcessCounters

| Property | Type | Description |
|----------|------|-------------|
| `ProcessId` | `int?` | Process ID |
| `ProcessName` | `string` | Process name |
| `ProcessTitle` | `string` | Main window title (Windows only) |
| `MachineName` | `string` | Machine name |
| `CpuUtilizationPercent` | `double?` | CPU usage of this process, 0-100 of total machine capacity |
| `HandleCount` | `int?` | Number of handles (Windows) or open file descriptors (Linux, macOS) |
| `ThreadCount` | `int?` | Number of threads |
| `PrivateMemory` | `long?` | Private memory in bytes |
| `VirtualMemory` | `long?` | Virtual memory in bytes |
| `WorkingSetMemory` | `long?` | Working set memory in bytes |
| `NonPagedSystemMemory` | `long?` | Non-paged system memory (Windows only) |
| `PagedSystemMemory` | `long?` | Paged system memory (Windows only) |
| `PeakPagedSystemMemory` | `long?` | Peak paged memory (Windows only) |
| `PeakVirtualSystemMemory` | `long?` | Peak virtual memory in bytes |
| `PeakWorkingSetMemory` | `long?` | Peak working set memory in bytes |

### PlatformTypeEnum

| Value | Description |
|-------|-------------|
| `Unknown` | Unknown or unsupported platform |
| `Windows` | Microsoft Windows |
| `Linux` | Linux |
| `Mac` | macOS |

## Platform-Specific Namespaces

All platform implementations derive from shared base classes in the root namespace (`PerformanceStatisticsBase`, `SystemCountersBase`, `ProcessCountersBase`) and implement the common interfaces:

- `PerformanceStatistics.Windows` - kernel32 and PhysicalDisk performance counters
  - `WindowsPerformanceStatistics`, `WindowsSystemCounters`, `WindowsProcessCounters`
- `PerformanceStatistics.Linux` - `/proc` and `/sys`
  - `LinuxPerformanceStatistics`, `LinuxSystemCounters`, `LinuxProcessCounters`
- `PerformanceStatistics.Mac` - Mach host statistics, `sysctl`, `libproc`, and IOKit statistics
  - `MacPerformanceStatistics`, `MacSystemCounters`, `MacProcessCounters`

Constructing another operating system's statistics or system counters throws `PlatformNotSupportedException`. For cross-platform code, use `PerformanceStatisticsFactory.Create()`, which returns `IPerformanceStatistics`.

## Building and Testing

```bash
dotnet build src/PerformanceStatistics.sln
dotnet run --project src/Test.Automated -f net10.0   # Touchstone console runner; exit code 0 = all passed
dotnet test src/Test.Xunit                             # same suites under xUnit
dotnet test src/Test.Nunit                             # same suites under NUnit
```

Tests run against the real operating system, so run them on each platform; CI (`.github/workflows/ci.yml`) does this on Windows, Linux, and macOS. Add new tests to `src/Test.Shared/Suites`.

## Version History

Refer to CHANGELOG.md for version history.
