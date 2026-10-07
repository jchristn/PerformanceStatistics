---
type: "Project"
title: "Public API: three interfaces, three shared base classes, and PerformanceStatisticsFactory"
description: "Consumers call PerformanceStatisticsFactory.Create() to get an IPerformanceStatistics; concrete Windows/Linux/Mac classes derive from PerformanceStatisticsBase, SystemCountersBase, and ProcessCountersBase (since v2.1.0)."
timestamp: "2026-10-07T20:37:28.7440080Z"
created: "2026-10-07T20:07:45.2083750Z"
slug: "public-api-and-factory"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["namespace-and-folder-layout", "monitored-processes-lifecycle", "cpu-sampling-model", "linux-backend", "mac-backend", "windows-backend"]
version: "2"
salience: "0.5"
---
Entry point: `PerformanceStatisticsFactory` (src/PerformanceStatistics/PerformanceStatisticsFactory.cs, static, namespace `PerformanceStatistics`).
- `Create(List<string> monitoredProcesses = null)` checks `RuntimeInformation.IsOSPlatform` in the order Windows → Linux → OSX and returns `WindowsPerformanceStatistics` / `LinuxPerformanceStatistics` / `MacPerformanceStatistics`. On any other OS it throws `PlatformNotSupportedException`.
- `CurrentPlatform` returns `PlatformTypeEnum` {Unknown, Windows, Linux, Mac}; `IsPlatformSupported` returns a bool.

Interfaces (src/PerformanceStatistics/Interfaces/; unchanged since v2.0.0):
- `IPerformanceStatistics : IDisposable`:
  - `ISystemCounters System`
  - `List<string> MonitoredProcessNames {get;set;}`
  - `Dictionary<string, List<IProcessCounters>> MonitoredProcesses`, keyed by requested name, one entry per matching PID
  - `TcpConnectionInformation[] ActiveTcpConnections`
  - `GetActiveTcpConnections(int? sourcePort, int? destPort)`
- `ISystemCounters`: CPU%, MemoryFreeMegabytes, disk read/write ops/sec, disk read/write queue, disk free%/free/size/used MB. All members are nullable.
- `IProcessCounters : IDisposable`: id, name, title, machine, CPU%, HandleCount, ThreadCount, and memory values as `long?` bytes.

Base classes (root namespace, public abstract, added in v2.1.0):
- `PerformanceStatisticsBase`
- `SystemCountersBase` (also IDisposable)
- `ProcessCountersBase` (adds a public `Refresh()`)

Their hooks and constructors are `internal`, so only this assembly can derive from them; consumers can't subclass. Platform classes are thin; see [[namespace-and-folder-layout]].

Null means "unavailable on this platform or for this process"; getters never throw. The platform-specific system counters and statistics classes throw `PlatformNotSupportedException` from their constructor on the wrong OS, and are annotated `[SupportedOSPlatform]` on net8.0+.
