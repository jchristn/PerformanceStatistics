---
type: "Project"
title: "v2.2.0 (2026-10-07): all-process enumeration, process metadata, system series, ProcessActions"
description: "Added GetAllProcesses, SystemInfo, process ExecutablePath/CommandLine/UserName/ParentProcessId/StartTimeUtc/State/Priority/disk I/O, per-core CPU, memory totals, swap, network interfaces, volumes, load, ProcessActions, PERFSTATS_HOST_ROOT, via internal hooks per OS."
timestamp: "2026-10-07T22:09:38.4816448Z"
created: "2026-10-07T22:09:37.7320448Z"
slug: "v2-2-0-task-manager-metrics"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["public-api-and-factory", "mac-backend", "linux-backend", "windows-backend", "taskmanager-architecture"]
version: "1"
salience: "0.5"
---
Phase 0 of USER_APPS.md, an additive minor release. The interfaces gained members, so external implementers must add them.

Per-OS sources, all behind internal hooks on ProcessCountersBase and SystemCountersBase:
- Executable path: Windows QueryFullProcessImageName; Linux readlink /proc/<pid>/exe (via libc readlink); macOS proc_pidpath.
- Command line: Windows NtQueryInformationProcess(60); Linux /proc/<pid>/cmdline; macOS sysctl KERN_PROCARGS2 (same user or root only).
- Parent PID, state, and user:
  - Windows: PROCESS_BASIC_INFORMATION, thread suspension state, and token plus LookupAccountSid.
  - Linux: /proc stat and status.
  - macOS: proc_pidinfo PROC_PIDT_SHORTBSDINFO (flavor 13; offsets ppid 4, status 12, uid 36). It works for every user's processes, unlike PROC_PIDTBSDINFO, which fails for root's.
  - User names come from getpwuid (cached).
- Disk I/O: Windows GetProcessIoCounters (all I/O); Linux /proc/<pid>/io (storage); macOS proc_pid_rusage rusage_info_v2 words 18 and 19.
- Per-core CPU: Windows NtQuerySystemInformation(8); Linux /proc/stat cpuN; macOS host_processor_info followed by vm_deallocate(mach_task_self).
- Swap: Windows paging file beyond RAM (approximate); Linux SwapTotal/SwapFree; macOS vm.swapusage.
- Network: .NET NetworkInterface statistics through DeltaSampler. Each packet counter is read separately because NonUnicastPacketsSent throws on Linux.
- ProcessActions: Process.Kill(entireTree), PriorityClass, and suspend/resume via kill SIGSTOP/SIGCONT (Linux 19/18, macOS 17/19) or NtSuspendProcess/NtResumeProcess on Windows. Results are ProcessActionResultEnum values; it never throws.

Caching: exe path, command line, user, and start time are read once per counter, even when empty, because repeated denials are expensive. Disk and CPU samplers are primed in the constructor so the first pass over about 1,200 processes does not sleep 100ms per process.

Measured on Apple Silicon (1,170 processes): the first GetAllProcesses pass with metadata took 161ms, and steady passes took about 14ms.

macOS privilege limit: CPU, I/O, handles, footprint, and command line for other users' processes (about 340 root and daemon processes) are null without elevation.
