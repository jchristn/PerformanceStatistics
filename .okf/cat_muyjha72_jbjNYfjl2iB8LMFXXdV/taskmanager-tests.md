---
type: "Project"
title: "Test suites and CI for the library and TaskManager apps"
description: "Test.Automated (115: library + Monitoring), Test.Tui (10 headless), Test.Server (9 end-to-end with real server + agent), web Vitest (13) and Playwright (5); CI jobs dotnet (3 OS × net8/net10), web, compose+Playwright."
timestamp: "2026-10-07T22:09:18.5028130Z"
created: "2026-10-07T22:09:17.8621584Z"
slug: "taskmanager-tests"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["touchstone-test-architecture", "taskmanager-tui", "taskmanager-web-app", "windows-validation-pending"]
version: "1"
salience: "0.5"
---
As of 2026-10-07; all pass on macOS and on Linux in Docker.

- src/Test.Automated → Test.Shared suites: Factory, SystemCounters, ProcessMonitoring, TcpConnection, Disposal, EdgeCase, Consistency, Extended (v2.2 metrics and a ProcessActions lifecycle on a spawned sleep/ping child), Monitoring (sampler, history, catalog, formatter, accountant, policy, JSON, SQLite store), and the platform suite. 115 cases.
- src/Test.Tui: Touchstone console runner. A FakeSnapshotSource drives TaskManagerApp on HeadlessBackend(160, 50); a real LocalSnapshotSource smoke test runs too. TUI_DUMP=1 prints every tab with live data. 10 cases.
- src/Test.Server (net10.0): starts TaskManager.Server.Program.Build on http://127.0.0.1:0 with in-memory config and a temporary database, starts a real AgentWorker against it, and runs MonitoringClient tests: auth, agent key, online host, snapshot, sorting, stream, history, accounting after CompactAsync, suspend/resume/kill, denials, and audit. 9 cases.
- web: npm test (Vitest, 13 cases, including format parity with the C# cases) and npm run e2e (Playwright, 5 cases against a stack).

Commands:
- `dotnet run --project src/Test.Automated -f net10.0`
- `dotnet run --project src/Test.Tui -f net10.0`
- `dotnet run --project src/Test.Server`
- Linux: use the Docker tar-copy recipe in [[touchstone-test-architecture]], adding --exclude=node_modules.

CI (.github/workflows/ci.yml):
- dotnet: {ubuntu, windows, macos} × {net8.0, net10.0}. Builds the solution, then runs Test.Automated and Test.Tui, plus Test.Server on net10.0.
- web: npm ci, test, build.
- compose: up --wait with linux-agent, then Playwright with ci-host.
