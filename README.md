# PerformanceStatistics

<img src="https://github.com/jchristn/PerformanceStatistics/blob/main/Assets/logo.png?raw=true" width="100" height="100" />

Library for capturing performance statistics from commonly-used performance counters.  Currently only supports Windows.

[![NuGet Version](https://img.shields.io/nuget/v/PerformanceStatistics.svg?style=flat)](https://www.nuget.org/packages/PerformanceStatistics/) [![NuGet](https://img.shields.io/nuget/dt/PerformanceStatistics.svg)](https://www.nuget.org/packages/PerformanceStatistics) 

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v1.1.x

- Added monitored processes

## Example Project

Refer to the ```Test``` project for exercising the library.

## Getting Started
```csharp
using PerformanceStatistics;

WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics();
stats.MonitoredProcessNames.Add("chrome");  // if you want detailed statistics
stats.MonitoredProcessNames.Add("outlook"); // about these processes
Console.WriteLine(stats.ToString());
/*
--------------------------------------------------
Operating System                : WINDOWS
System Counters                 :
  CPU Utilization Percent       : 0%
  Memory Free (Megabytes)       : 4638MB
  Total Disk Read Operations    : 0
  Total Disk Write Operations   : 0
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
  CPU Utilization               : 0%
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
  ...
---
Active TCP Connections          : 137
  | 127.0.0.1:49677 to 127.0.0.1:49678: Established
  | 127.0.0.1:49678 to 127.0.0.1:49677: Established
  ...
*/
```

## Version History

Refer to CHANGELOG.md for version history.
