---
type: "Project"
title: "SqliteHistoryStore: long-format series, three resolutions, top-N per-process recording"
description: "Tables hosts/series/samples(series_id,res,ts,avg,max)/processes/rollups; raw every sample, 1-minute and 1-hour rollups via CompactAsync; top 25 processes per metric every sample, others every 60s; accounting from 1-minute process aggregates."
timestamp: "2026-10-07T22:08:25.9578186Z"
created: "2026-10-07T22:08:25.4758093Z"
slug: "sqlite-history-store"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-architecture", "taskmanager-server-api"]
version: "1"
salience: "0.5"
---
File: src/PerformanceStatistics.Monitoring/Storage/SqliteHistoryStore.cs. It uses Microsoft.Data.Sqlite 10.0.12 in WAL mode with one connection guarded by a SemaphoreSlim.

Schema:
- series(id, host_id, kind 0=system/1=interface/2=process, owner = '' / interface name / process key, metric). Unique on (host_id, kind, owner, metric). Series IDs are cached in memory.
- samples(series_id, res 1/60/3600, ts unix ms, avg, max). WITHOUT ROWID, primary key (series_id, res, ts).
- processes(host_id, key, pid, ppid, name, path, cmdline, user, start_ts, first_seen, last_seen).
- hosts(host_id, info JSON, last_seen).
- rollups(res, until_ts).

WriteAsync stores, in one transaction per snapshot:
- Every catalog system metric.
- system.cpu.core.N.
- Interface rx, tx, and packets.
- Processes chosen by HistoryStoreSettings: the top N (default 25) by CPU, working set, disk read+write, and handles every sample, plus every other process every BackgroundProcessInterval (default 60s). Seven process metrics are stored.

CompactAsync(now), called every 60s by the server's StartupService:
- Rolls completed minutes from raw into res=60, and completed hours from 60 into res=3600 (AVG of avg, MAX of max).
- Applies retention:
  - raw: 1h
  - system and interface: 60 → 30 days, 3600 → 365 days
  - process: 60 → 24h, 3600 → 30 days
  - process metadata: until last_seen is older than 30 days

Query*Async picks the resolution from the step (step ≥ 3600 → hour, ≥ 60 → minute, otherwise raw) and falls back when that resolution has no rows. It buckets with (ts/step)*step.

QueryAccountingAsync reads res=60 process rows. CPU-seconds = avg% / 100 × cores × 60, disk bytes = avg rate × 60, and peak memory is the maximum per-minute sum per group. It therefore covers completed minutes only.
