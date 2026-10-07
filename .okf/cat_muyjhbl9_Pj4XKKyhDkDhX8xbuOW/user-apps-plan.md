---
type: "Project"
title: "USER_APPS.md: TUIKit terminal app and React + C# web app over a shared Monitoring layer, with native agents"
description: "Plan (2026-10-07) for cross-platform Task Manager apps: PerformanceStatistics.Monitoring shared layer, TaskManager.Tui (TUIKit 1.5.* package), native TaskManager.Agent per host, TaskManager.Server (ASP.NET Core + SQLite) and React/Vite web via Docker Compose."
timestamp: "2026-10-07T20:47:05.7171682Z"
created: "2026-10-07T20:47:02.5955187Z"
slug: "user-apps-plan"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["user-apps-telemetry-requirements", "library-gaps-for-user-apps", "v2-1-0-consistency-refactor"]
version: "1"
salience: "0.5"
---
USER_APPS.md at the repo root (2026-10-07) plans two apps, a TUI and a web app. Both give one consistent task-manager and performance-reporting interface across Windows, macOS, and Linux.

Architecture:
- Library: PerformanceStatistics, extended in Phase 0.
- PerformanceStatistics.Monitoring, a new shared layer:
  - Immutable HostSnapshot DTOs and a single MetricsSampler per host.
  - HistoryBuffer and an IHistoryStore.
  - MetricCatalog: units, descriptions, and per-OS availability with reasons.
  - ProcessActions: kill, tree, priority, suspend.
  - An accounting Accumulator.
- TaskManager.Tui:
  - References the TUIKit NuGet package 1.5.* and starts from the tuikit-app template.
  - Local in-process by default; `--server` for remote mode.
- TaskManager.Agent: a native service on each host (Windows Service / launchd / systemd) that pushes snapshots to the server.
- TaskManager.Server:
  - ASP.NET Core minimal API with WebSocket streaming.
  - SQLite with downsampling.
  - Auth, plus audited actions that are off by default.
- web: React + Vite in JavaScript, served by nginx.
- Server and web run in Docker Compose. An optional linux-agent profile uses pid: host, network_mode: host, and /host mounts.

Key reason for native agents: containers on Docker Desktop (macOS and Windows) run in a Linux VM and cannot observe the host.

Milestones: 0 library gaps (v2.2.0), 1 Monitoring, 2 TUI MVP, 3 server+agent, 4 web via Compose, 5 parity and accounting, 6 hardening.

Open questions recorded in the plan:
- Single machine vs. fleet.
- ASP.NET Core vs. Watson Webserver.
- Web-triggered process actions policy.
- Product naming (TaskManager.* is a placeholder).
- uPlot vs. Recharts.

The user's telemetry requirements are in [[user-apps-telemetry-requirements]]; the library work they need is in [[library-gaps-for-user-apps]].
