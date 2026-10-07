---
type: "Project"
title: "The v2.1.0 Windows code paths have not been executed yet; verify via CI or on a Windows machine"
description: "WindowsSystemCounters (GetSystemTimes, GlobalMemoryStatusEx, PhysicalDisk RawValue/NextValue) and the Windows suite compile but were only run on macOS/Linux; first CI run on windows-latest must pass before release."
timestamp: "2026-10-07T20:39:27.3544790Z"
created: "2026-10-07T20:39:27.0118600Z"
slug: "windows-validation-pending"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["windows-backend", "solution-projects-and-targets"]
version: "1"
salience: "0.5"
---
As of 2026-10-07, v2.1.0 was developed on macOS. It compiles for all target frameworks, but the Windows code has not been executed.

What to verify on Windows:
- `dotnet run --project src/Test.Automated -f net10.0` passes. That includes the Windows suite's DiskOperationsAvailable case (all four disk metrics non-null from the PhysicalDisk counters) and ConsistencySuite.
- PhysicalDisk "Disk Reads/sec" RawValue increases with I/O. Fallback if not: switch to NextValue on held counters.
- GetSystemTimes CPU tracks Task Manager within a few percent.
- Process CPU for several `dotnet` processes is reported per PID.
- On a non-English Windows install, CPU and memory still work, and disk counters may be null. Consider PdhAddEnglishCounter, or reading the 009 counter names, if they are null.

How: push branch multiplatform-hardening (or open a PR) so .github/workflows/ci.yml runs on windows-latest. Delete this memory once CI is green on Windows.
