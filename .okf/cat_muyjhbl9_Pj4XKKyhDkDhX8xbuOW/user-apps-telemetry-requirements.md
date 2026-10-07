---
type: "User"
title: "User requirement: chart process list and system telemetry over time, in the TUI or the web app"
description: "The maintainer wants, over time and in both TUI and web: per-process PID, memory, disk I/O, CPU, handle count, thread count, executable path plus extra metadata; aggregate and per-CPU utilization; memory; network; disk I/O; any other useful telemetry."
timestamp: "2026-10-07T20:47:11.3215348Z"
created: "2026-10-07T20:47:10.8289951Z"
slug: "user-apps-telemetry-requirements"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["user-apps-plan", "library-gaps-for-user-apps"]
version: "1"
salience: "0.5"
---
Stated by the maintainer on 2026-10-07. The use case: Task Manager and performance reporting differ wildly across Windows, macOS, and Linux, and they want one consistent interface.

Must be viewable live and charted over time, in the TUI or the web app:
- The process list over time. For each process: PID, memory, disk I/O, CPU, file handle count, thread count, executable location, and any other useful metadata. The plan adds parent PID, user, command line, working directory, start time, state, priority, page faults, context switches, TCP connections, and platform extras.
- CPU utilization, aggregate and per core.
- Memory utilization.
- Network utilization.
- Disk I/O.
- Any other telemetry worth charting. The plan adds load average, swap, process/thread/handle totals, uptime, and per-volume space. GPU, temperature, and power are stretch goals.

How to apply:
- The USER_APPS.md Requirements section maps each item to its per-OS source and phase.
- History has three tiers:
  - An in-memory ring.
  - Persisted SQLite with downsampling; the TUI gets an optional `--record`.
  - Per-process history at full resolution for the top N and pinned processes, and 1-minute resolution for the rest. Exited processes are kept until retention expires.
- Per-process network bytes need kernel tracing on every OS, so they are a stretch goal.
- Never show a fake 0; show n/a with a reason.

See [[user-apps-plan]] and [[library-gaps-for-user-apps]].
