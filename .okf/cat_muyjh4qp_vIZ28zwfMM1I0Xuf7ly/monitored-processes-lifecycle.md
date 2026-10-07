---
type: "Project"
title: "MonitoredProcesses re-scans names but reuses one counter instance per running PID"
description: "PerformanceStatisticsBase tracks counters by PID: a running process keeps its IProcessCounters (refreshed) across accesses so CPU is measured between them; exited PIDs, reused PIDs, and removed names are disposed."
timestamp: "2026-10-07T20:37:41.9944340Z"
created: "2026-10-07T20:08:00.4781480Z"
slug: "monitored-processes-lifecycle"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["public-api-and-factory", "v2-0-1-tostring-race-fix", "cpu-sampling-model", "v2-1-0-consistency-refactor"]
version: "2"
salience: "0.5"
---
This is the behavior since v2.1.0. Before v2.1.0, every access disposed and recreated all counters, so process CPU on Linux and Mac was always 0.

The `MonitoredProcesses` getter (PerformanceStatisticsBase) runs under `_Lock`:
1. Snapshot MonitoredProcessNames. The copy is retried if the caller mutates the list concurrently.
2. For each distinct name, add a key. Duplicate names collapse into one key, and whitespace names map to an empty list.
3. For each Process from `Process.GetProcessesByName`:
   - If its PID is already in `_Tracked` (Dictionary<int, ProcessCountersBase>) and `HasExited()` is false, dispose the new Process object and call `existing.Refresh()`. That clears the Process cache, so memory values are current.
   - Otherwise (a new PID, or a PID reused after its process exited), dispose any old counter and create a new one with `CreateProcessCounters`.
4. Dispose and remove tracked PIDs that were not seen in this pass.

Dispose() disposes all tracked counters and the system counters. After that, MonitoredProcesses returns an empty dictionary and system getters return null.

Guidance for callers: read MonitoredProcesses once per polling pass. `ActiveTcpConnections` re-reads the OS table on every access, so copy it into a local before using Length and an indexer; see [[v2-0-1-tostring-race-fix]].
