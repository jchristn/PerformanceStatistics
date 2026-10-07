---
type: "Project"
title: "USER_APPS.md: implemented on 2026-10-07 (TUIKit TUI, server, agents, React web, Compose)"
description: "The plan is implemented; decisions taken: fleet-capable, ASP.NET Core server, actions off by default (agent allowlist + admin + audit), TaskManager.*/taskmgr placeholder names, uPlot charts. See USER_APPS.md Status and docs/TASKMANAGER.md."
timestamp: "2026-10-07T22:09:46.8397817Z"
created: "2026-10-07T20:47:02.5955180Z"
slug: "user-apps-plan"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["taskmanager-architecture", "taskmanager-remaining-work", "user-apps-telemetry-requirements"]
version: "2"
salience: "0.5"
---
USER_APPS.md at the repo root plans two apps, a TUI and a web app, that give one consistent task-manager and performance-reporting interface across Windows, macOS, and Linux. On 2026-10-07 the maintainer said "Execute it". Phases 0–5 were then built in commits:
- fb5077e: library v2.2.0
- d050017: Monitoring
- e95088b: TUI
- 762fc50: server, agent, installers
- 9b74499: web, compose, CI, docs

USER_APPS.md now opens with a Status table and a "Not done yet" list.

The plan's open questions were answered with its recommended defaults; tell the maintainer if any should change:
- Fleet-capable.
- ASP.NET Core minimal APIs (not Watson).
- Web-triggered actions are off by default and enabled per agent with TASKMGR__ALLOWEDACTIONS; admin only; protected names and PID ≤ 4 refused; PID-reuse guard; audited.
- Placeholder names TaskManager.* and the taskmgr command.
- uPlot for charts.

Key architectural reason for native agents: containers on Docker Desktop (macOS and Windows) run in a Linux VM and cannot observe the host.

Details: [[taskmanager-architecture]], [[taskmanager-server-api]], [[taskmanager-tui]], [[taskmanager-web-app]], [[taskmanager-docker-deploy]], [[taskmanager-tests]], [[taskmanager-remaining-work]], and the requirements in [[user-apps-telemetry-requirements]].
