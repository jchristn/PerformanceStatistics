---
type: "Project"
title: "Folder layout: shared bases at the root, Internal/ helpers, thin per-OS folders"
description: "src/PerformanceStatistics has root base classes, Interfaces/, Internal/ (DeltaSampler, Samples, DiskSpace), and Windows/, Linux/, Mac/ folders, each holding three thin public classes plus native/parse helpers."
timestamp: "2026-10-07T20:37:34.6423800Z"
created: "2026-10-07T20:07:52.4743640Z"
slug: "namespace-and-folder-layout"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["public-api-and-factory", "cpu-sampling-model", "v2-1-0-consistency-refactor"]
version: "2"
salience: "0.5"
---
Library: src/PerformanceStatistics/ (solution src/PerformanceStatistics.sln). Layout since v2.1.0:

Root namespace `PerformanceStatistics`:
- PerformanceStatisticsFactory.cs and PlatformTypeEnum.cs.
- Interfaces/I*.cs.
- PerformanceStatisticsBase.cs: process tracking, TCP, ToString, Dispose.
- SystemCountersBase.cs: sampling, rounding, units, disk space, ToString.
- ProcessCountersBase.cs: everything read from System.Diagnostics.Process, plus CPU% via a TotalProcessorTime delta.

Internal/ (namespace PerformanceStatistics.Internal, all internal):
- DeltaSampler.cs: rate sampling; see [[cpu-sampling-model]].
- Samples.cs: CpuTimes, DiskIoTotals, DiskIoRates.
- DiskSpace.cs: DriveInfo aggregation with de-duplication.

Per-OS folders (namespaces PerformanceStatistics.Windows / .Linux / .Mac):
- XPerformanceStatistics: only the constructor plus `CreateProcessCounters(Process)`.
- XSystemCounters: overrides internal hooks TryReadCpuTimes, TryGetAvailableMemoryBytes, TryReadDiskTotals, TryGetDiskQueues, IncludeDrive.
- XProcessCounters: overrides only the members that differ by OS.
- Helpers:
  - Windows: WindowsNativeMethods (GetSystemTimes, GlobalMemoryStatusEx).
  - Linux: ProcFileParser (/proc and /sys).
  - Mac: MacNativeMethods (host_statistics, host_statistics64, sysctlbyname, proc_pidinfo, proc_pid_rusage) and MacSystemInfo (computations and ioreg).

Rule: put behavior in a base class. A platform class should only supply raw OS readings, so a fix lands once.
