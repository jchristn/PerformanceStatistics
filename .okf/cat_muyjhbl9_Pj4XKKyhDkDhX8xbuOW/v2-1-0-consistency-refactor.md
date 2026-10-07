---
type: "Project"
title: "v2.1.0 (2026-10-07): shared base classes, one sampling model, native APIs per OS"
description: "Refactored to PerformanceStatisticsBase/SystemCountersBase/ProcessCountersBase + DeltaSampler so metrics mean the same thing on every OS; macOS moved from CLI tools to native APIs; Windows from per-read counters to kernel32 + reused counters."
timestamp: "2026-10-07T20:39:20.7218060Z"
created: "2026-10-07T20:39:20.0529490Z"
slug: "v2-1-0-consistency-refactor"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["namespace-and-folder-layout", "cpu-sampling-model", "monitored-processes-lifecycle", "mac-backend", "windows-backend", "linux-backend", "windows-validation-pending"]
version: "1"
salience: "0.5"
---
Decision (branch multiplatform-hardening, 2026-10-07): make the library dependable across Windows, Linux, and macOS, in support of the cross-platform Task Manager apps planned in USER_APPS.md. The public interfaces and public class names and constructors are unchanged, so the release is source-compatible. Semantics changed where they had been platform-specific or wrong; see the CHANGELOG "Behavior changes" table.

Choices and reasons:
1. Shared base classes with internal hooks (not public virtuals). They remove the three copies of each orchestrator, and keeping hooks internal keeps the public surface small, so consumers can't subclass.
2. DeltaSampler for every rate metric (see [[cpu-sampling-model]]). Previously each platform computed rates differently, and first reads returned 0.
3. Process CPU from Process.TotalProcessorTime on all OSes, normalized by core count. This replaced a Linux hardcoded CLK_TCK of 100 and the Windows per-name counter instance, so values are comparable across OSes.
4. Reuse process counters per PID across MonitoredProcesses accesses. Without this, process CPU could never be measured.
5. macOS: P/Invoke to stable libSystem and libproc APIs instead of spawning top, vm_stat, and iostat. It is about 10x faster, avoids parsing localized CLI output, and has no 1s iostat block. ioreg is kept for disk ops because IOKit P/Invoke is much more complex.
6. Windows: GetSystemTimes and GlobalMemoryStatusEx avoid localized performance counter names. Disk counters are kept but held for the object's lifetime.
7. Disk space everywhere: DriveInfo plus de-duplication by (format, size, free).
8. Wrong-OS construction throws PlatformNotSupportedException up front, rather than every getter throwing.
9. Added a CI matrix on all three OSes. Developed and tested on macOS (Apple Silicon) and on Linux in Docker; Windows is compile-only locally.

This decision supersedes the open-work items it fixed: windows-cpu-counter-single-sample, orchestrator-duplication, private-field-naming-inconsistency (the library is now uniformly _PascalCase), and readme-changelog-drift (CHANGELOG and README were updated).
