---
type: "Project"
title: "The three XPerformanceStatistics orchestrators are copy-pasted (~280 lines each)"
description: "Windows/Linux/MacPerformanceStatistics duplicate MonitoredProcesses caching, TCP logic, GetActiveTcpConnections, ToString, and Dispose; consider an abstract base class with a CreateProcessCounters(Process) hook."
timestamp: "2026-10-07T20:09:37.9413360Z"
status: "deprecated"
created: "2026-10-07T20:09:37.6888690Z"
slug: "orchestrator-duplication"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["namespace-and-folder-layout", "monitored-processes-lifecycle"]
supersededBy: "mem_muykm8l0_p0eb7RdlzP2dDOum7GE"
version: "1"
salience: "0.5"
---
Verified by diff (2026-10-07). Linux and Mac differ only in their names. Windows also differs in private field casing and an extra `using System.Runtime.InteropServices`. Every behavioral fix must be applied three times; the v2.0.1 ToString fix was. Refactor idea: an internal abstract `PerformanceStatisticsBase` implementing IPerformanceStatistics, with abstract `ISystemCounters CreateSystemCounters()` and `IProcessCounters CreateProcessCounters(Process)` members. Keep the public class names for compatibility. The same duplication exists between the X*SystemCounters ToString and the GetDiskSpace helpers (Linux and Mac).
