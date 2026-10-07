---
type: "Project"
title: "Rate sampling: DeltaSampler gives every platform the same CPU and disk-ops semantics"
description: "Internal/DeltaSampler turns cumulative counters into rates since the previous read: first read waits ≥100ms, reads <100ms apart return the cached value, a progress predicate handles macOS bursty CPU ticks; Stopwatch timing."
timestamp: "2026-10-07T20:37:51.3624590Z"
created: "2026-10-07T20:08:07.4415410Z"
slug: "cpu-sampling-model"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["linux-backend", "mac-backend", "windows-backend", "monitored-processes-lifecycle"]
version: "2"
salience: "0.5"
---
`DeltaSampler<TSample,TResult>` (src/PerformanceStatistics/Internal/DeltaSampler.cs) is thread-safe and locked. Its inputs:
- `TryRead(out sample)`: reads a raw counter.
- `compute(prev, curr, elapsed)`: turns two samples into a result.
- An optional `hasProgressed(prev, curr)` predicate.
- A minimum interval, 100ms by default.

Semantics:
- Prime() takes a baseline. The system CPU sampler is primed in the SystemCountersBase constructor, and the process CPU sampler in the ProcessCountersBase constructor.
- On the first TryGet, if less than the minimum interval has passed since the baseline, it sleeps for the remainder. A one-shot read therefore returns a real value, not 0.
- A read less than the minimum interval after the previous one returns the cached result without resetting the baseline. Before v2.1.0, Linux reset the baseline and returned 0.
- If hasProgressed is false (the counters did not move), the baseline is kept and the cached value is returned. On the first read, it polls until progress or MaximumFirstReadWait (1.5s). This exists because macOS host_statistics CPU ticks are published in bursts, sometimes about 800ms apart; this was measured on Apple Silicon on 2026-10-07. The CPU sampler passes `curr.Total > prev.Total`.

Users of the sampler:
- System CPU: CpuTimes (Total, Idle) → `CpuTimes.BusyPercent`, rounded to 0 decimal places.
  - Linux: /proc/stat, idle = idle + iowait.
  - Mac: HOST_CPU_LOAD_INFO user/system/idle/nice.
  - Windows: GetSystemTimes, total = kernel + user (kernel includes idle).
- Disk ops: DiskIoTotals (cumulative reads/writes) → DiskIoRates per second. One sample feeds both the read and write properties.
- Process CPU: Process.TotalProcessorTime delta / wall time / Environment.ProcessorCount × 100, clamped to 0..100 and rounded to 2 decimal places. It is identical on all OSes, including Windows since v2.1.0.

Windows disk queue lengths are the exception: they use held PerformanceCounter.NextValue() calls, and the first read may be 0.
