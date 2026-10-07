---
type: "Project"
title: "Linux backend: /proc and /sys via ProcFileParser"
description: "CPU from /proc/stat, memory from MemAvailable, disk ops from /proc/diskstats limited to physical disks listed in /sys/block, queues from /sys/block/*/inflight, HandleCount = /proc/<pid>/fd entries."
timestamp: "2026-10-07T20:38:03.5908030Z"
created: "2026-10-07T20:08:18.3446380Z"
slug: "linux-backend"
category: "cat_muyjh704_U65ZYmJoqusyC0RWeiM"
links: ["cpu-sampling-model", "public-api-and-factory", "disk-ops-queue-approximations"]
version: "2"
salience: "0.5"
---
Files: src/PerformanceStatistics/Linux/. ProcFileParser is internal static and never throws. This reflects v2.1.0.

System (LinuxSystemCounters : SystemCountersBase):
- CPU: the "cpu " line of /proc/stat. Total sums fields 1..8 (user, nice, system, idle, iowait, irq, softirq, steal); guest is already counted inside user. Idle = idle + iowait.
- Memory: /proc/meminfo MemAvailable × 1024, falling back to MemFree+Buffers+Cached. Inside a container this is the host's or VM's value, not the cgroup limit.
- Disk ops: /proc/diskstats field 3 (reads completed) and field 7 (writes completed), summed over physical whole disks. Physical disks are the /sys/block entries minus the virtual prefixes loop, ram, zram, dm-, md, nbd, sr, and fd. If /sys/block is missing, the fallback regex is `^((s|h|v|xv)d[a-z]+|nvme\d+n\d+|mmcblk\d+)$`. Before v2.1.0, NVMe partitions were double-counted.
- Disk queues: in-flight reads and writes from /sys/block/<disk>/inflight, reported separately. Before v2.1.0 both were IoInProgress/2.
- Disk space: DriveInfo with DriveType.Fixed, excluding the /proc, /sys, /dev, /run, and /snap trees. In Docker, the overlay root is Fixed and the virtiofs bind mounts are Unknown, so they are excluded.

Process (LinuxProcessCounters : ProcessCountersBase):
- HandleCount = the number of /proc/<pid>/fd entries. It is null when unreadable (another user's process).
- NonPaged, Paged, and PeakPaged are null.
- Everything else comes from System.Diagnostics.Process.

Verified on 2026-10-07 in Docker (mcr.microsoft.com/dotnet/sdk:10.0, arm64): 94/94 tests passed, and reported values looked plausible.
