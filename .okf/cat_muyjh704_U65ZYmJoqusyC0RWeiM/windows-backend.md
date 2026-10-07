---
type: "Project"
title: "Windows backend: kernel32 for CPU and memory, reused PhysicalDisk counters for disk I/O"
description: "Windows CPU from GetSystemTimes, memory from GlobalMemoryStatusEx, disk ops from the RawValue of long-lived PhysicalDisk(_Total) Disk Reads/Writes/sec counters, queues via NextValue; process fields from System.Diagnostics.Process."
timestamp: "2026-10-07T20:38:22.9649800Z"
created: "2026-10-07T20:08:33.3253120Z"
slug: "windows-backend"
category: "cat_muyjh704_U65ZYmJoqusyC0RWeiM"
links: ["cpu-sampling-model", "public-api-and-factory", "windows-validation-pending"]
version: "2"
salience: "0.5"
---
Files: src/PerformanceStatistics/Windows/ (WindowsNativeMethods). This reflects v2.1.0.

System (WindowsSystemCounters):
- CPU: GetSystemTimes(idle, kernel, user). total = kernel + user, because kernel includes idle. It does not depend on localized counter names.
- Memory: GlobalMemoryStatusEx ullAvailPhys.
- Disk counters are created lazily on first use under `_CounterLock`, with category "PhysicalDisk", instance "_Total", and readOnly. A counter that fails to create stays null, so its property returns null. The counters are disposed in Dispose(bool).
  - Disk ops: the RawValue (cumulative count, PERF_COUNTER_COUNTER) of "Disk Reads/sec" and "Disk Writes/sec", fed to DeltaSampler.
  - Disk queues: NextValue() on "Avg. Disk Read Queue Length" and "Avg. Disk Write Queue Length". They are primed once, so each read is the average since the previous read.
- Disk space: DriveInfo Fixed drives, the same code as the other platforms.

Process (WindowsProcessCounters): no overrides. Everything comes from Process, including HandleCount and the paged and non-paged fields. CPU comes from TotalProcessorTime, normalized by ProcessorCount.

Before v2.1.0:
- A new PerformanceCounter was created for every read, and NextValue() was called once, so rate counters returned 0.
- Process CPU used the "Process" counter keyed by ProcessName, so same-named processes collided and the value was not normalized.
- Values were formatted with "N2" and then re-parsed.

Not run locally: this was developed on macOS. It compiles, and the .github/workflows/ci.yml matrix runs it on windows-latest. See [[windows-validation-pending]].
