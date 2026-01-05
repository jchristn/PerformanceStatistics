# PerformanceStatistics

<img src="https://github.com/jchristn/PerformanceStatistics/blob/main/Assets/logo.png?raw=true" width="100" height="100" />

A lightweight .NET library for capturing real-time performance statistics from Windows performance counters. Monitor system resources like CPU, memory, and disk usage, as well as detailed metrics for specific processes.

[![NuGet Version](https://img.shields.io/nuget/v/PerformanceStatistics.svg?style=flat)](https://www.nuget.org/packages/PerformanceStatistics/) [![NuGet](https://img.shields.io/nuget/dt/PerformanceStatistics.svg)](https://www.nuget.org/packages/PerformanceStatistics)

## Why Use This Library?

- **Simple API**: Get system and process performance metrics with just a few lines of code
- **Real-time Monitoring**: Access live CPU, memory, disk, and network statistics
- **Process Tracking**: Monitor specific processes by name with detailed metrics including memory usage, thread counts, and handle counts
- **TCP Connection Visibility**: View active TCP connections with local and remote endpoints
- **Background Service Integration**: Ideal for health monitoring, performance dashboards, and automated alerting systems

## Platform Support

**Windows Only** - This library uses Windows Performance Counters and is only supported on Windows operating systems.

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v1.1.6

- Implemented `IDisposable` on all classes for proper resource cleanup
- Fixed memory leaks caused by undisposed `PerformanceCounter` objects
- Fixed memory leaks from undisposed `Process` objects in monitored processes
- Added thread-safe caching for monitored process data

## Example Project

Refer to the `Test` project for exercising the library.

## Getting Started

### Basic Usage

```csharp
using PerformanceStatistics;

// Create the statistics object (implements IDisposable)
using (var stats = new WindowsPerformanceStatistics())
{
    // Add process names to monitor (optional)
    stats.MonitoredProcessNames.Add("chrome");
    stats.MonitoredProcessNames.Add("outlook");

    // Print all statistics
    Console.WriteLine(stats.ToString());
}
```

### Recommended Usage Pattern

Since `WindowsPerformanceStatistics` implements `IDisposable`, always use it with a `using` statement or call `Dispose()` when finished:

```csharp
using PerformanceStatistics;

// Option 1: using statement (recommended)
using (var stats = new WindowsPerformanceStatistics())
{
    Console.WriteLine($"CPU: {stats.System.CpuUtilizationPercent}%");
    Console.WriteLine($"Free RAM: {stats.System.MemoryFreeMegabytes} MB");
}

// Option 2: using declaration (C# 8+)
using var stats = new WindowsPerformanceStatistics();
Console.WriteLine($"CPU: {stats.System.CpuUtilizationPercent}%");

// Option 3: Manual disposal (for long-lived instances)
var stats = new WindowsPerformanceStatistics();
try
{
    // Use stats...
}
finally
{
    stats.Dispose();
}
```

### Accessing System Counters

```csharp
using var stats = new WindowsPerformanceStatistics();

// CPU
double cpuPercent = stats.System.CpuUtilizationPercent;

// Memory
double freeMemoryMB = stats.System.MemoryFreeMegabytes;

// Disk
int diskReads = stats.System.TotalDiskReadOperations;
int diskWrites = stats.System.TotalDiskWriteOperations;
int diskReadQueue = stats.System.TotalDiskReadQueue;
int diskWriteQueue = stats.System.TotalDiskWriteQueue;
double diskFreePercent = stats.System.TotalDiskFreePercent;
double diskFreeMB = stats.System.TotalDiskFreeMegabytes;
double diskSizeMB = stats.System.TotalDiskSizeMegabytes;
double diskUsedMB = stats.System.TotalDiskUsedMegabytes;
```

### Monitoring Specific Processes

```csharp
using var stats = new WindowsPerformanceStatistics();
stats.MonitoredProcessNames.Add("devenv");  // Visual Studio
stats.MonitoredProcessNames.Add("sqlservr"); // SQL Server

// Access monitored process data
foreach (var kvp in stats.MonitoredProcesses)
{
    string processName = kvp.Key;

    foreach (var process in kvp.Value)
    {
        Console.WriteLine($"Process: {process.ProcessName} (PID: {process.ProcessId})");
        Console.WriteLine($"  CPU: {process.CpuUtilizationPercent}%");
        Console.WriteLine($"  Private Memory: {process.PrivateMemory / (1024 * 1024)} MB");
        Console.WriteLine($"  Working Set: {process.WorkingSetMemory / (1024 * 1024)} MB");
        Console.WriteLine($"  Threads: {process.ThreadCount}");
        Console.WriteLine($"  Handles: {process.HandleCount}");
    }
}
```

### Monitoring TCP Connections

```csharp
using var stats = new WindowsPerformanceStatistics();

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
    private WindowsPerformanceStatistics _stats;
    private Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stats = new WindowsPerformanceStatistics();
        _stats.MonitoredProcessNames.Add("MyApp");

        _timer = new Timer(CheckPerformance, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    private void CheckPerformance(object state)
    {
        if (_stats.System.CpuUtilizationPercent > 90)
        {
            // Alert: High CPU usage
        }

        if (_stats.System.MemoryFreeMegabytes < 500)
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

## Sample Output

```
--------------------------------------------------
System Counters                 :
  CPU Utilization Percent       : 12%
  Memory Free (Megabytes)       : 4638MB
  Total Disk Read Operations    : 0
  Total Disk Write Operations   : 15
  Total Disk Read Queue         : 0
  Total Disk Write Queue        : 0
  Total Disk Free Percent       : 28%
  Total Disk Free Megabytes     : 338436MB
  Total Disk Size Megabytes     : 1208700MB
  Total Disk Used Megabytes     : 870264MB
Monitored Processes             : 2
  chrome
    Process ID                    : 39192
    Process Name                  : chrome
    Process Title                 : Welcome to my email!
    Machine Name                  : .
    CPU Utilization               : 0.5%
    Handle Count                  : 2652
    Thread Count                  : 43
    Memory, Private               : 252215296 bytes
    Memory, Paged System          : 1546536 bytes
    Memory, Peak Paged System     : 282923008 bytes
    Memory, Non-Paged System      : 97720 bytes
    Memory, Virtual               : 2307852075008 bytes
    Memory, Peak Virtual          : 2308019273728 bytes
    Memory, Working Set           : 322637824 bytes
    Memory, Peak Working Set      : 367566848 bytes
  ---
  outlook
    ...
  ---
Active TCP Connections          : 137
  | 127.0.0.1:49677 to 127.0.0.1:49678: Established
  | 127.0.0.1:49678 to 127.0.0.1:49677: Established
  ...
```

## API Reference

### WindowsPerformanceStatistics

| Property | Type | Description |
|----------|------|-------------|
| `System` | `WindowsSystemCounters` | System-wide performance counters |
| `MonitoredProcessNames` | `List<string>` | List of process names to monitor |
| `MonitoredProcesses` | `Dictionary<string, List<WindowsProcessCounters>>` | Performance data for monitored processes |
| `ActiveTcpConnections` | `TcpConnectionInformation[]` | Array of active TCP connections |

### WindowsSystemCounters

| Property | Type | Description |
|----------|------|-------------|
| `CpuUtilizationPercent` | `double` | Current CPU usage percentage |
| `MemoryFreeMegabytes` | `double` | Available memory in MB |
| `TotalDiskReadOperations` | `int` | Disk reads per second |
| `TotalDiskWriteOperations` | `int` | Disk writes per second |
| `TotalDiskReadQueue` | `int` | Average disk read queue length |
| `TotalDiskWriteQueue` | `int` | Average disk write queue length |
| `TotalDiskFreePercent` | `double` | Percentage of free disk space |
| `TotalDiskFreeMegabytes` | `double` | Free disk space in MB |
| `TotalDiskSizeMegabytes` | `double` | Total disk size in MB |
| `TotalDiskUsedMegabytes` | `double` | Used disk space in MB |

### WindowsProcessCounters

| Property | Type | Description |
|----------|------|-------------|
| `ProcessId` | `int?` | Process ID |
| `ProcessName` | `string` | Process name |
| `ProcessTitle` | `string` | Main window title |
| `MachineName` | `string` | Machine name |
| `CpuUtilizationPercent` | `double` | CPU usage percentage for this process |
| `HandleCount` | `int` | Number of handles |
| `ThreadCount` | `int` | Number of threads |
| `PrivateMemory` | `long` | Private memory in bytes |
| `PagedSystemMemory` | `long` | Paged system memory in bytes |
| `NonPagedSystemMemory` | `long` | Non-paged system memory in bytes |
| `VirtualMemory` | `long` | Virtual memory in bytes |
| `WorkingSetMemory` | `long` | Working set memory in bytes |
| `PeakPagedSystemMemory` | `long` | Peak paged memory in bytes |
| `PeakVirtualSystemMemory` | `long` | Peak virtual memory in bytes |
| `PeakWorkingSetMemory` | `long` | Peak working set memory in bytes |

## Version History

Refer to CHANGELOG.md for version history.
