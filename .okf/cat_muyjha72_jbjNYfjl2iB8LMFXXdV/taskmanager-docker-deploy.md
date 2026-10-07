---
type: "Project"
title: "Docker Compose deployment: server + web, optional linux-agent profile"
description: "docker/compose.yaml (name taskmgr): server :8080 (/data volume, curl healthcheck), web :3000 (nginx proxies /api and the WebSocket); profile linux-agent runs the agent with pid/network host, SYS_PTRACE, host /proc and /sys at /host. Macs and Windows use the native agent."
timestamp: "2026-10-07T22:09:09.3922552Z"
created: "2026-10-07T22:09:08.7184441Z"
slug: "taskmanager-docker-deploy"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["taskmanager-architecture", "taskmanager-web-app", "taskmanager-server-api", "linux-backend"]
version: "1"
salience: "0.5"
---
Files:
- docker/server.Dockerfile: sdk:10.0 build stage, then aspnet:10.0 with curl added for the healthcheck.
- docker/web.Dockerfile: node:22-alpine npm ci + build, then nginx:1.27-alpine with docker/nginx.conf. The nginx config proxies /api/ with Upgrade headers, serves the SPA with a try_files fallback, and caches /assets immutably.
- docker/agent.Dockerfile: runtime:10.0 with PERFSTATS_HOST_ROOT=/host.
- docker/compose.yaml and docker/.env.example. Copy the example to docker/.env, which is gitignored.
- .dockerignore at the repo root.

The build context is the repo root. The images copy LICENSE.md, README.md, and Assets/logo.png to /, because the csproj pack items reference ../../.

Commands:
- `docker compose -f docker/compose.yaml up -d --build`
- Add `--profile linux-agent` to also monitor the Linux Docker host.
- Pass `--env-file <file>` to use non-default ports or secrets.

linux-agent needs four things:
- pid: host, to see host processes.
- network_mode: host, for host interfaces and TCP.
- cap_add SYS_PTRACE: without it /proc/<pid>/exe and /proc/<pid>/io of host processes are unreadable, even as root.
- /proc and /sys mounted read-only at /host/proc and /host/sys. With PERFSTATS_HOST_ROOT set, SystemInfo reports "Linux <kernel> (host)" instead of the container image's OS.

On Docker Desktop (macOS and Windows), linux-agent reports the Docker VM, not the Mac or PC, so run the native agent there.

Verified on 2026-10-07 on Docker Desktop for Mac with compose v5.5:
- The stack came up healthy.
- The agent showed the VM's dockerd and containerd with paths, I/O, and file descriptors.
- 5/5 Playwright tests passed through nginx.
- TUI remote mode streamed from the server.
