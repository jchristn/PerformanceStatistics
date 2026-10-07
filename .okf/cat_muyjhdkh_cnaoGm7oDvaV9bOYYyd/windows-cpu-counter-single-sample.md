---
type: "Project"
title: "Suspected: Windows rate counters read from a brand-new PerformanceCounter return ~0"
description: "WindowsSystemCounters creates a new counter and calls NextValue() once (CPU and Disk Reads/Writes/sec need two samples); WindowsProcessCounters calls NextValue() twice with no delay and keys the instance by ProcessName."
timestamp: "2026-10-07T20:09:32.7535230Z"
status: "deprecated"
created: "2026-10-07T20:09:32.2508950Z"
slug: "windows-cpu-counter-single-sample"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["windows-backend", "cpu-sampling-model"]
supersededBy: "mem_muykm8l0_p0eb7RdlzP2dDOum7GE"
version: "1"
salience: "0.5"
---
Status: suspected from reading the code (2026-10-07); not verified on Windows.
- WindowsSystemCounters.CpuUtilizationPercent, TotalDiskRead/WriteOperations, and the queue-length counters each create `new PerformanceCounter(...)` and call `NextValue()` once. For rate/average counters (% Processor Time, Disk Reads/sec, Avg. Disk Queue Length), the first NextValue on a new instance returns 0, so these likely always report 0. Memory and free-space counters are instantaneous and unaffected.
- WindowsProcessCounters.CpuUtilizationPercent calls NextValue() twice back-to-back. With nearly zero elapsed time the result is noisy or 0. The value is also not divided by ProcessorCount (it can exceed 100), unlike the Linux/Mac process CPU values.
- The Process instance name is set to `ProcessName`. With multiple same-named processes, Windows names them dotnet, dotnet#1, ..., so every PID reports the first instance's CPU.
Suggested fix: hold long-lived PerformanceCounter fields per WindowsSystemCounters/WindowsProcessCounters instance (dispose them in Dispose), call NextValue once at construction, and resolve the instance name by PID ("Process" category, "ID Process" counter). Alternatively, use the Process.TotalProcessorTime delta as on Mac/Linux.
