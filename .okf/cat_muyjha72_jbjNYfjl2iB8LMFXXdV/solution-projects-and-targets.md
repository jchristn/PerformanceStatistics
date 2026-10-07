---
type: "Project"
title: "Solution projects, target frameworks, dependencies, and CI"
description: "src/PerformanceStatistics.sln: library (netstandard2.1;net8.0;net10.0), Monitoring, TaskManager.Tui/Agent/Server, Test, Test.Shared, Test.Automated, Test.Xunit, Test.Nunit, Test.Tui, Test.Server; web/ (npm) and docker/ outside the solution."
timestamp: "2026-10-07T22:10:01.5082817Z"
created: "2026-10-07T20:08:54.2146970Z"
slug: "solution-projects-and-targets"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["taskmanager-tests", "release-process", "taskmanager-architecture"]
version: "3"
salience: "0.5"
---
Build everything with `dotnet build src/PerformanceStatistics.sln`. It has 0 warnings as of 2026-10-07.

Libraries and apps:
- PerformanceStatistics: netstandard2.1, net8.0, net10.0. Its only dependency is System.Diagnostics.PerformanceCounter 10.0.11. Version 2.2.0. GeneratePackageOnBuild is on.
- PerformanceStatistics.Monitoring: net8.0 and net10.0. Uses Microsoft.Data.Sqlite 10.0.12. Package version 1.0.0.
- TaskManager.Tui: net8.0 and net10.0, TUIKit 1.5.*. PackAsTool with command taskmgr.
- TaskManager.Agent: Worker SDK, net8.0 and net10.0. Uses Microsoft.Extensions.Hosting, .WindowsServices, and .Systemd 10.0.12. Assembly taskmgr-agent, published single-file self-contained.
- TaskManager.Server: Web SDK, net10.0.

Test projects:
- Test: interactive console app.
- Test.Shared, Test.Automated, Test.Xunit, Test.Nunit: Touchstone 0.1.12. Test.Shared also references Monitoring.
- Test.Tui: Touchstone.Cli.
- Test.Server: net10.0; references both the server and the agent.

Outside the solution: web/ (Node 22 or later; npm ci) and docker/.

CI is .github/workflows/ci.yml; see [[taskmanager-tests]].
