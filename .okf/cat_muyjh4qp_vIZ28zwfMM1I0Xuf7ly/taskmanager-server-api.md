---
type: "Project"
title: "TaskManager.Server API, auth, actions, and the agent protocol"
description: "ASP.NET Core minimal API: bearer sessions from /api/v1/auth/login (admin + optional viewer), X-Agent-Key for /api/v1/agent/*, WS /api/v1/stream?host&access_token with deflate, actions queued per host and audited; settings via TASKMGR__*."
timestamp: "2026-10-07T22:08:37.5691582Z"
created: "2026-10-07T22:08:36.7623658Z"
slug: "taskmanager-server-api"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-architecture", "sqlite-history-store", "taskmanager-docker-deploy"]
version: "1"
salience: "0.5"
---
File: src/TaskManager.Server/Program.cs. Program.Build(args, configure) is reused by Test.Server.

Settings come from the TASKMGR config section (environment variables TASKMGR__*), bound into ServerSettings:
- ADMINPASSWORD and AGENTKEY are required.
- Optional: ADMINUSER (default admin), VIEWERPASSWORD (enables viewer, read-only), AGENTKEYS (comma list), STORAGE (SQLite path), SESSIONHOURS, ONLINESECONDS (15), retention values, CORSORIGINS.

Services:
- SessionService: in-memory bearer tokens; constant-time password compare.
- HostRegistry: per host — info, allowed actions, latest snapshot, bounded drop-oldest channels per stream subscriber, the action queue, and responses. Requests older than 2 minutes become Stale.
- AuditLog: audit table in the same SQLite file.
- StartupService: initializes storage, seeds hosts from the database, and compacts every 60s.
- Request decompression (agents gzip their bodies) and response compression.

Endpoints:
- /health
- POST /api/v1/auth/login, /auth/logout, /me
- /catalog
- /hosts and /hosts/{id}
- /hosts/{id}/snapshot[?processes=false]
- /hosts/{id}/processes?sort=&desc=&filter=&top= (sorted on the server by a catalog metric)
- /process-records, /history, /processes/{key}/history, /interfaces/{name}/history, /accounting?groupBy=name|user
- POST /actions (admin, and the agent must allow the action) → 202 Pending
- GET /actions/{requestId}
- /audit
- Agent endpoints: POST /api/v1/agent/hello (AgentHello: info + allowed actions), POST /agent/ingest (List<HostSnapshot>), GET /agent/actions?host=, POST /agent/actions/results?host=.

Action safety layers:
1. The server requires admin, and that the agent's allowlist includes the action.
2. The agent's ProcessActionExecutor re-checks its policy: deny by default, protected names, PID ≤ 4, and a StartTimeUtc mismatch → Stale.
3. Every request and result is audited.

Agent (src/TaskManager.Agent): AgentSettings (TASKMGR__SERVER, AGENTKEY, HOSTID, INTERVALSECONDS, ALLOWEDACTIONS, ALLOWEDPROCESSUSERS, BUFFERSIZE 600) and AgentWorker (send loop every 1s with batches of up to 30; action poll every 2s). It uses AddWindowsService and AddSystemd. Installers live in scripts/agent/{linux,macos,windows}.
