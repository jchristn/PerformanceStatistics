---
type: "Project"
title: "Phase 0 library work for the user apps (target v2.2.0)"
description: "Add all-process enumeration with executable path and metadata, per-process disk I/O/faults/context switches, per-core CPU, memory totals/swap, per-interface network, per-disk/per-volume detail, load/population/uptime, TCP owning PID, process actions, and a container-aware Linux root."
timestamp: "2026-10-07T20:47:21.6116730Z"
status: "deprecated"
created: "2026-10-07T20:47:20.8624044Z"
slug: "library-gaps-for-user-apps"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["user-apps-plan", "user-apps-telemetry-requirements", "namespace-and-folder-layout", "cpu-sampling-model"]
supersededBy: "mem_muynu3uo_cwMRNTIhYC5A0BJPrED"
version: "1"
salience: "0.5"
---
Status: not started as of 2026-10-07. The library can only monitor processes by name and exposes 10 system counters. USER_APPS.md Phase 0 lists the per-OS sources.

Process work:
- `IPerformanceStatistics.GetAllProcesses()`, reusing counters per PID like MonitoredProcesses.
- New IProcessCounters members:
  - ParentProcessId, ExecutablePath, UserName, StartTimeUtc, CommandLine.
  - DiskRead/WriteBytesPerSecond, PageFaultsPerSecond, ContextSwitchesPerSecond.
  - State and priority.
- Sources:
  - Windows: QueryFullProcessImageName, GetProcessIoCounters, toolhelp.
  - Linux: /proc/pid/exe, io, stat, status.
  - macOS: proc_pidpath, proc_pid_rusage diskio, PROC_PIDTBSDINFO.

System work:
- PerCoreCpuUtilizationPercent with user/system/iowait splits.
- MemoryTotal and MemoryUsed, swap/commit, swap in/out.
- NetworkInterfaces: per-interface bytes, packets, errors, and drops per second, via NetworkInterface statistics through DeltaSampler.
- Disks and Volumes: per physical disk ops, bytes, queue, and busy %; per-volume space.
- Load average, context switches, and interrupts.
- Process, thread, and handle totals.
- ISystemInfo: boot time, OS, CPU model, core counts, total memory.

Other work:
- TCP connections with owning PID.
- Process actions: kill, kill tree, priority, suspend/resume.
- A configurable host root such as /host, so a containerized Linux agent reports the host.

Rules:
- Use the existing internal-hook pattern and DeltaSampler for every rate.
- Null means unavailable.
- Add a ConsistencySuite test per metric.
- CI must be green on all three OSes.
- Performance budget: under 2% of one core at 1s with about 500 processes.

Close this memory once Phase 0 ships.
