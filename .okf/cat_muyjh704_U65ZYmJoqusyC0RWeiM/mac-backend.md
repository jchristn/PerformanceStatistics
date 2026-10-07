---
type: "Project"
title: "macOS backend: Mach host statistics, sysctl, libproc, and IOKit stats (ioreg)"
description: "macOS uses P/Invoke (host_statistics, host_statistics64, sysctlbyname, proc_pidinfo, proc_pid_rusage) plus `ioreg -c IOBlockStorageDriver` for disk ops; memory available = memsize − (internal−purgeable + wired + compressed)."
timestamp: "2026-10-07T20:38:14.1543200Z"
created: "2026-10-07T20:08:26.1319410Z"
slug: "mac-backend"
category: "cat_muyjh704_U65ZYmJoqusyC0RWeiM"
links: ["cpu-sampling-model", "public-api-and-factory", "disk-ops-queue-approximations"]
version: "2"
salience: "0.5"
---
Files: src/PerformanceStatistics/Mac/ (MacNativeMethods, MacSystemInfo). This reflects v2.1.0; before that the backend shelled out to top, vm_stat, and iostat.

System (MacSystemCounters):
- CPU: host_statistics(HOST_CPU_LOAD_INFO=3, count 4) returns user/system/idle/nice ticks as uint32. Ticks are published in bursts, so DeltaSampler uses a progress predicate; see [[cpu-sampling-model]].
- Memory available = hw.memsize − (internal_page_count − purgeable_count + wire_count + compressor_page_count) × hw.pagesize. The page counts come from host_statistics64(HOST_VM_INFO64=4, count 38); word offsets are wire=3, purgeable=22, compressor=32, internal=35. This matches Activity Monitor's "Memory Used". The old formula (free + speculative + purgeable) reported 6.9GB when 57GB was actually available.
- Disk ops: `/usr/sbin/ioreg -c IOBlockStorageDriver -r -w 0`, summing "Operations (Read)" and "Operations (Write)". Reads and writes are reported separately, it takes about 20ms, and the command has a 5s timeout. This is the only remaining process spawn.
- Disk queues: always null.
- Disk space: only "/" and "/Volumes/*", plus DiskSpace de-duplication. All APFS volumes in a container report the container's capacity and free space; the pre-2.1.0 code counted /, VM, Preboot, Update, and Data separately and reported 5× the capacity.

Process (MacProcessCounters):
- HandleCount = proc_pidinfo(PROC_PIDLISTFDS) bytes / 8.
- PrivateMemory = ri_phys_footprint from proc_pid_rusage(RUSAGE_INFO_V2), at 64-bit word 9.
- Both of those are null for other users' processes (no privilege).
- PeakWorkingSet, PeakVirtual, NonPaged, Paged, and PeakPaged are null. .NET returns 0 for these on macOS.

Verified on 2026-10-07 on Apple Silicon (18 cores, 128GB RAM): 94/94 tests passed, and the suite ran in about 4s, down from 37s.
