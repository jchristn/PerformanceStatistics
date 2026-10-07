---
type: "Project"
title: "Disk metric sources by platform (ops, queues, space)"
description: "All three platforms report separate read and write ops/sec over physical disks; queues are Windows averages, Linux in-flight counts, and null on macOS; disk space is DriveInfo fixed drives de-duplicated by (format, size, free)."
timestamp: "2026-10-07T20:38:29.1836050Z"
created: "2026-10-07T20:08:38.3273810Z"
slug: "disk-ops-queue-approximations"
category: "cat_muyjh704_U65ZYmJoqusyC0RWeiM"
links: ["linux-backend", "mac-backend", "windows-backend"]
version: "2"
salience: "0.5"
---
As of v2.1.0:

| Metric | Windows | Linux | macOS |
|---|---|---|---|
| Read/Write ops/sec | PhysicalDisk(_Total) Disk Reads/Writes/sec RawValue deltas | /proc/diskstats deltas, physical disks only | IOKit "Operations (Read/Write)" deltas via ioreg |
| Read/Write queue | Avg. Disk Read/Write Queue Length (average since the previous read) | /sys/block/*/inflight (instantaneous in-flight) | null |
| Free/Size/Used | DriveInfo Fixed | DriveInfo Fixed, minus /proc, /sys, /dev, /run, /snap | DriveInfo "/" and /Volumes/* |

Disk space (Internal/DiskSpace.TryGetTotals) skips drives that are not ready or have size 0. It counts a (DriveFormat, TotalSize, AvailableFreeSpace) triple once, because APFS volumes and Linux bind mounts report identical numbers. Free = AvailableFreeSpace (excluding root-reserved blocks), and Used = Size − Free, so Linux "used" is larger than df's Used.

Remaining semantic difference: queues on Windows are averages, while on Linux they are instantaneous counts. Both are documented in the README "Sampling Semantics" and "Platform-Specific Metric Availability" sections.
