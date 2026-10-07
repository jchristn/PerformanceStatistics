---
type: "Project"
title: "Solution projects, target frameworks, dependencies, and CI"
description: "src/PerformanceStatistics.sln: library (netstandard2.1;net8.0;net10.0, only depends on System.Diagnostics.PerformanceCounter) plus Test, Test.Shared, Test.Automated, Test.Xunit, and Test.Nunit; GitHub Actions matrix on Windows/Linux/macOS."
timestamp: "2026-10-07T20:38:52.3514780Z"
created: "2026-10-07T20:08:54.2146970Z"
slug: "solution-projects-and-targets"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["touchstone-test-architecture", "release-process", "windows-validation-pending"]
version: "2"
salience: "0.5"
---
Solution: src/PerformanceStatistics.sln. Build with `dotnet build src/PerformanceStatistics.sln`. It builds with 0 warnings as of v2.1.0.

PerformanceStatistics (the library):
- TargetFrameworks: netstandard2.1, net8.0, net10.0.
- Its only PackageReference is System.Diagnostics.PerformanceCounter 10.0.11. System.Text.Json was removed in v2.1.0 as unused.
- GeneratePackageOnBuild=True, so every build produces the nupkg and snupkg.
- Version: csproj `<Version>`, 2.1.0 as of 2026-10-07, alongside `<PackageReleaseNotes>`.

Test projects:
- Test: interactive console app, net8.0 and net10.0, using Inputty 1.0.13 and in-box System.Text.Json. It uses PerformanceStatisticsFactory.Create(), so it runs on all OSes. Commands: q, ?, cls, stats, json, port, add, del.
- Test.Shared: Touchstone.Core 0.1.12, holding the suites. NoWarn CA1416.
- Test.Automated: Touchstone.Cli 0.1.12.
- Test.Xunit: xunit 2.9.3 with Touchstone.XunitAdapter.
- Test.Nunit: NUnit 4.6.1 with Touchstone.NunitAdapter.
- All test projects target net8.0 and net10.0.

CI: .github/workflows/ci.yml runs the matrix {ubuntu, windows, macos}-latest × {net8.0, net10.0}. Each job builds Release, runs Test.Automated, and uploads the JSON results. It runs on push to main, on PRs, and on manual dispatch.
