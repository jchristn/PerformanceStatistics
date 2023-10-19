# PerformanceStatistics

<img src="https://github.com/jchristn/PerformanceStatistics/blob/main/Assets/logo.png?raw=true" width="100" height="100" />

Library for capturing performance statistics from commonly-used performance counters.  Currently only supports Windows.

[![NuGet Version](https://img.shields.io/nuget/v/PerformanceStatistics.svg?style=flat)](https://www.nuget.org/packages/PerformanceStatistics/) [![NuGet](https://img.shields.io/nuget/dt/PerformanceStatistics.svg)](https://www.nuget.org/packages/PerformanceStatistics) 

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v1.0.x

- Initial release, only for Windows

## Example Project

Refer to the ```Test``` project for exercising the library.

## Getting Started
```csharp
using PerformanceStatistics;

WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics();
Console.WriteLine(stats.ToString());

/*

Windows Performance Statistics
------------------------------
  CPU Utilization Percent         : 0%
  Memory Free (Megabytes)         : 3276MB
  Total Disk Read Operations      : 0
  Total Disk Write Operations     : 0
  Total Disk Read Queue           : 0
  Total Disk Write Queue          : 0
  Total Disk Free Percent         : 27%
  Total Disk Free Megabytes       : 326140MB
  Total Disk Size Megabytes       : 1207925.93MB
  Total Disk Used Megabytes       : 881785.93MB
  Active TCP Connections          : 98
  | 127.0.0.1:49677 to 127.0.0.1:49678: Established
  | 127.0.0.1:49678 to 127.0.0.1:49677: Established
  ...

*/
```

## Version History

Refer to CHANGELOG.md for version history.
