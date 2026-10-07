---
type: "Project"
title: "Remaining TaskManager and library work after the 2026-10-07 build-out"
description: "Not done: TCP connections with owning PID, per-physical-disk breakdown, per-process network bytes, page faults/context switches, GPU/temperature/power, product naming, publishing packages/images, and running on Windows."
timestamp: "2026-10-07T22:09:26.5571065Z"
created: "2026-10-07T22:09:26.0164268Z"
slug: "taskmanager-remaining-work"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["user-apps-plan", "windows-validation-pending", "taskmanager-architecture"]
version: "1"
salience: "0.5"
---
Phase 0 (v2.2.0) and phases 1–5 of USER_APPS.md are implemented; the USER_APPS.md Status section is authoritative. Still open:
1. TCP connections with the owning PID. Sources: Windows GetExtendedTcpTable; Linux /proc/net/tcp{,6} plus /proc/<pid>/fd socket inodes; macOS proc_pidfdinfo PROC_PIDFDSOCKETINFO.
2. Per-physical-disk metrics (ops, bytes, busy %). Totals and per-volume space exist. Sources: Linux per-device diskstats; IOKit per-driver statistics; Windows PhysicalDisk instances.
3. Per-process page faults and context switches. Sources: Linux stat/status; macOS PROC_PIDTASKINFO; Windows GetProcessMemoryInfo.
4. Per-process network bytes. Needs ETW, eBPF, or nettop; keep it behind a capability flag.
5. GPU, temperature, and power (stretch).
6. Product naming: TaskManager.*/taskmgr are placeholders. Then publish the NuGet packages (PerformanceStatistics 2.2.0, PerformanceStatistics.Monitoring 1.0.0, TaskManager.Tui tool) and the Docker images.
7. Windows: nothing has been run on Windows yet; see [[windows-validation-pending]].
8. Sessions are in memory, so a server restart logs users out. Consider persisting them, or OIDC.

Follow USER_APPS.md's consistency rules for any new metric: add it to MetricCatalog with per-OS availability, a ConsistencySuite or ExtendedMetricsSuite test, and the formatter.
