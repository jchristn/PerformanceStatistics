---
type: "Project"
title: "Tests: Touchstone suites in Test.Shared, run on each OS (macOS locally, Linux via Docker)"
description: "Add tests in src/Test.Shared/Suites via TestSupport.Case; 94 cases on macOS/Linux including ConsistencySuite regressions; run Linux tests in Docker by tar-copying the repo without bin/obj."
timestamp: "2026-10-07T20:39:02.7855770Z"
created: "2026-10-07T20:09:02.1792180Z"
slug: "touchstone-test-architecture"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["solution-projects-and-targets", "windows-validation-pending"]
version: "2"
salience: "0.5"
---
src/Test.Shared/PerformanceStatisticsSuites.All contains: Factory, SystemCounters, ProcessMonitoring, TcpConnection, Disposal, EdgeCase, Consistency (added in v2.1.0), plus PlatformSpecificSuite.BuildForCurrentPlatform() for the current OS (Windows, Linux, or Mac).

ConsistencySuite holds regression tests for cross-platform bugs:
- Counters are reused across refreshes.
- Process CPU is greater than 0 after burning CPU between accesses.
- The first system CPU read under load is greater than 0.
- Disk read/write availability agrees.
- Rapid CPU reads return the cached value.
- Disk size is not inflated.
- Free memory is at most physical memory.
- Duplicate names are tolerated.
- Removing a name disposes its counters.
- A disposed instance is inert.
- Constructing another OS's classes throws PlatformNotSupportedException.
- HandleCount for the current process is greater than 0.
- Output is invariant-culture.

Add a case with `TestSupport.Case(suiteId, caseId, name, Action)` or `CaseAsync`, and assert with Test.Shared/Check.cs. Tests hit the real OS, so assertions must be platform-aware.

Commands:
- macOS (local): `dotnet run --project src/Test.Automated -f net10.0 [results.json]`. 94/94 passed in about 4s on 2026-10-07.
- Linux via Docker (as used on 2026-10-07; avoids rm, and copied obj/ folders from the Mac would break restore):
  `docker run --rm -v "$PWD":/src:ro mcr.microsoft.com/dotnet/sdk:10.0 bash -c 'mkdir -p /w && tar --exclude=bin --exclude=obj -cf - -C /src . | tar -xf - -C /w && cd /w/src && dotnet run --project Test.Automated -f net10.0'`
  The sdk:10.0 image has no net8 runtime; install it with dotnet-install.sh `--channel 8.0 --runtime dotnet` to test net8.0.
- `dotnet test src/Test.Xunit` and `dotnet test src/Test.Nunit` run the same 94 cases.
- Windows: only through CI; see [[windows-validation-pending]].
