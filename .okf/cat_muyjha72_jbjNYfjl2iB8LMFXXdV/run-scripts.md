---
type: "Project"
title: "run-tui.sh/.bat and run-docker.sh/.bat at the repo root; admin-privilege notices in both UIs"
description: "run-tui builds TaskManager.Tui (net10.0, else net8.0) and execs taskmgr.dll with args, warning when not root/Administrator; run-docker creates docker/.env with random secrets and wraps compose (up/down/logs/ps/restart, --linux-agent). Non-elevated collectors trigger 'admin privileges may be required' hints in TUI and web."
timestamp: "2026-10-07T22:32:11.0912390Z"
created: "2026-10-07T22:32:07.9830625Z"
slug: "run-scripts"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["taskmanager-docker-deploy", "taskmanager-tui", "taskmanager-web-app", "taskmanager-tests"]
version: "1"
salience: "0.5"
---
Added 2026-10-07 at the maintainer's request.

run-tui.sh / run-tui.bat:
- Builds src/TaskManager.Tui in Release (net10.0 if that SDK is installed, else net8.0), then runs bin/Release/<tfm>/taskmgr.dll and passes all arguments through.
- If not running as root or Administrator, prints a note that admin privileges may be required, with how to elevate (sudo ./run-tui.sh, or a terminal opened with "Run as administrator").
- The .sh version finds dotnet even under sudo (whose secure_path drops it) and builds as SUDO_USER, so build output is not root-owned.

run-docker.sh / run-docker.bat:
- On first run, generates docker/.env from docker/.env.example with a random admin password and agent key (bash: /dev/urandom; bat: PowerShell GUIDs). The file is chmod 600 and gitignored.
- Actions: up (default; build and start, then print the web and server URLs, the login, and an agent install hint), down, logs, ps, restart.
- --linux-agent adds the compose profile. On a Mac it warns that the agent monitors Docker's VM.

Privilege notices, shown when HostInfo.Elevated is false:
- TUI:
  - Header line 3 (the header is 3 lines) shows a yellow hint from TaskManagerApp.ElevationHint: "...admin privileges may be required (run with sudo / run as Administrator)".
  - The help screen explains how to elevate.
  - The process table's empty text and the n/a values in the detail panel say admin privileges may be required.
- Web:
  - ElevationNotice in components/Controls.jsx: a banner on every host tab, plus a compact inline note in the host header.
  - The host card says "not elevated: admin privileges may be required to see every process".
  - The empty process list and n/a values in the detail panel say the same.
- Catalog: ElevationNote reads "Hidden for other users' processes; admin/root privileges may be required (run as Administrator or with sudo)."

Tests: Test.Tui has ElevationHintWhenNotElevated and NoElevationHintWhenElevated (12 cases in total); there is a web unit test for ElevationNotice (14 in total). Verified on 2026-10-07 with run-docker.sh on Docker Desktop plus a non-elevated native agent on the Mac: the banner appeared for that host and not for the root container agent, and Playwright passed 5/5.

e2e: set TASKMGR_E2E_ACTIONS=1 when the monitored host allows Kill (CI does); otherwise the test expects Kill to be disabled.
