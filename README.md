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
| Windows | Full Support | All metrics available via Windows Performance Counters |
| Linux | Full Support | Metrics from `/proc` filesystem |
| macOS | Full Support | Metrics via system commands (`top`, `vm_stat`, `iostat`) |

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v2.0.0

- **Cross-platform support** for Windows, Linux, and macOS
- New `PerformanceStatisticsFactory` for automatic platform detection
- New interfaces: `IPerformanceStatistics`, `ISystemCounters`, `IProcessCounters`
- Full backward compatibility with existing Windows-specific classes
- `PlatformTypeEnum` enum for explicit platform identification

## Example Projects

- `Test` - Interactive console application for manual testing
- `Test.Automated` - Automated test suite with 54 cross-platform tests

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

// CPU (note: first call may return 0 as it requires two samples)
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
        Console.WriteLine($"  Handles: {process.HandleCount ?? 0}"); // 0 on macOS
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

## Platform-Specific Metric Availability

### System Counters

| Metric | Windows | Linux | macOS | Notes |
|--------|:-------:|:-----:|:-----:|-------|
| `CpuUtilizationPercent` | ✓ | ✓ | ✓ | First call returns 0 (requires two samples) |
| `MemoryFreeMegabytes` | ✓ | ✓ | ✓ | |
| `TotalDiskReadOperations` | ✓ | ✓ | ~approx | macOS: approximated from combined transfers |
| `TotalDiskWriteOperations` | ✓ | ✓ | ~approx | macOS: approximated from combined transfers |
| `TotalDiskReadQueue` | ✓ | ~approx | null | Linux: approximated from I/O in progress |
| `TotalDiskWriteQueue` | ✓ | ~approx | null | Linux: approximated from I/O in progress |
| `TotalDiskFreePercent` | ✓ | ✓ | ✓ | |
| `TotalDiskFreeMegabytes` | ✓ | ✓ | ✓ | |
| `TotalDiskSizeMegabytes` | ✓ | ✓ | ✓ | |
| `TotalDiskUsedMegabytes` | ✓ | ✓ | ✓ | |

### Process Counters

| Metric | Windows | Linux | macOS | Notes |
|--------|:-------:|:-----:|:-----:|-------|
| `ProcessId` | ✓ | ✓ | ✓ | |
| `ProcessName` | ✓ | ✓ | ✓ | |
| `ProcessTitle` | ✓ | empty | empty | No window concept on Linux/macOS |
| `MachineName` | ✓ | ✓ | ✓ | |
| `CpuUtilizationPercent` | ✓ | ✓ | ✓ | First call returns 0 (requires two samples) |
| `HandleCount` | ✓ | ~fd count | 0 | Linux: counts `/proc/[pid]/fd` entries |
| `ThreadCount` | ✓ | ✓ | ✓ | |
| `PrivateMemory` | ✓ | ✓ | 0 | May return 0 on macOS |
| `VirtualMemory` | ✓ | ✓ | ✓ | |
| `WorkingSetMemory` | ✓ | ✓ | ✓ | |
| `NonPagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PeakPagedSystemMemory` | ✓ | null | null | Windows-specific concept |
| `PeakVirtualSystemMemory` | ✓ | ✓ | ✓ | |
| `PeakWorkingSetMemory` | ✓ | ✓ | 0 | May return 0 on macOS |

**Legend:**
- ✓ = Fully supported
- ~approx = Approximated value
- null = Returns null (metric not available)
- 0 = Returns 0 (metric not tracked on this platform)
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
| `CpuUtilizationPercent` | `double?` | CPU usage percentage for this process |
| `HandleCount` | `int?` | Number of handles (fd count on Linux, 0 on macOS) |
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

All platform implementations are organized in their own namespaces and implement the common interfaces:

- `PerformanceStatistics.Windows` - Windows implementation using Performance Counters
  - `WindowsPerformanceStatistics`, `WindowsSystemCounters`, `WindowsProcessCounters`
- `PerformanceStatistics.Linux` - Linux implementation using `/proc` filesystem
  - `LinuxPerformanceStatistics`, `LinuxSystemCounters`, `LinuxProcessCounters`
- `PerformanceStatistics.Mac` - macOS implementation using system commands
  - `MacPerformanceStatistics`, `MacSystemCounters`, `MacProcessCounters`

For cross-platform code, use `PerformanceStatisticsFactory.Create()` which returns `IPerformanceStatistics`.

## Version History

Refer to CHANGELOG.md for version history.
