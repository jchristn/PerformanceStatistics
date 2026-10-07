---
type: "Project"
title: "v2.0.1 (2026-08-15): snapshot MonitoredProcesses and ActiveTcpConnections once in ToString()"
description: "Fixed IndexOutOfRangeException in XPerformanceStatistics.ToString() when the TCP table changed between .Length and indexing; the fix copies each volatile collection into a local once."
timestamp: "2026-10-07T20:09:18.6189320Z"
created: "2026-10-07T20:09:18.3709610Z"
slug: "v2-0-1-tostring-race-fix"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["monitored-processes-lifecycle"]
version: "1"
salience: "0.5"
---
v2.0.1 (commits 3586470 and 9e71907, 2026-08-15). ToString() used to read `ActiveTcpConnections.Length` and then index `ActiveTcpConnections[i]`. Each access re-queries the OS, so the array could shrink between reads. The fix stores `MonitoredProcesses` and `ActiveTcpConnections` in locals once, with an explanatory comment, in all three orchestrators. The same release updated dependencies to the 10.0.11 packages and added tests. Guidance: any new code that reads these properties more than once must snapshot them first (see [[monitored-processes-lifecycle]]).
