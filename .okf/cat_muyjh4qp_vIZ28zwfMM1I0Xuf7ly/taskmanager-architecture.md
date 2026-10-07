---
type: "Project"
title: "TaskManager apps: library → Monitoring layer → TUI / agent → server → web"
description: "src/PerformanceStatistics.Monitoring (snapshots, sampler, catalog, history, SQLite store, client) feeds TaskManager.Tui (TUIKit), TaskManager.Agent (native per host) → TaskManager.Server (ASP.NET Core) → web/ (React); docs/TASKMANAGER.md is the user guide."
timestamp: "2026-10-07T22:08:18.5231579Z"
created: "2026-10-07T22:08:15.4099530Z"
slug: "taskmanager-architecture"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-server-api", "taskmanager-tui", "taskmanager-web-app", "taskmanager-docker-deploy", "sqlite-history-store", "user-apps-plan"]
version: "1"
salience: "0.5"
---
Built on 2026-10-07 on branch multiplatform-hardening, following USER_APPS.md.

Projects (all in src/PerformanceStatistics.sln):
- PerformanceStatistics.Monitoring (net8.0;net10.0): the only layer the apps use; neither app reads the library directly.
  - HostSnapshot DTOs.
  - SnapshotBuilder: the single reader of IPerformanceStatistics.
  - MetricsSampler: one fixed-rate loop per host.
  - HistoryBuffer.
  - MetricCatalog / MetricIds / MetricFormatter.
  - ProcessAccountant.
  - Process action policy and executor.
  - SqliteHistoryStore.
  - MonitoringClient.
  - MonitoringJson: camelCase, string enums, nulls omitted.
- TaskManager.Tui (assembly and tool name taskmgr). Local in-process mode via LocalSnapshotSource (optional --record SQLite), or remote mode via RemoteSnapshotSource (--server, WebSocket stream).
- TaskManager.Agent (taskmgr-agent; net8.0;net10.0): pushes gzip batches to /api/v1/agent/ingest and polls /api/v1/agent/actions.
- TaskManager.Server (net10.0): the API, the stream, and storage. See [[taskmanager-server-api]].
- web/: React + Vite. See [[taskmanager-web-app]].
- docker/: compose stack. See [[taskmanager-docker-deploy]].

Data flow:
- agent: MetricsSampler → HostSnapshot → server ingest → SqliteHistoryStore.WriteAsync and HostRegistry.Publish → WebSocket subscribers (web, TUI remote).
- Local TUI: MetricsSampler → app.Post → TaskManagerState (HistoryBuffer + ProcessAccountant) → views.

Consistency rule: column names, units, formatting, and n/a reasons come from MetricCatalog (served at /api/v1/catalog). web/src/format.js mirrors MetricFormatter, with parity tests in both languages.

Process identity is ProcessSnapshot.Key = "pid@StartTimeUtc.Ticks". It is serialized as "key" in JSON; the web app must use it, not recompute it, because JavaScript Date loses sub-millisecond ticks.
