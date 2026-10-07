---
type: "Project"
title: "Docs drift: CHANGELOG says v2.0.0 and the README describes the old 54-test Test.Automated"
description: "The csproj is 2.0.1, but CHANGELOG.md \"Current Version\" is still v2.0.0 with no 2.0.1 entry; the README \"Example Projects\" section omits Test.Shared/Xunit/Nunit and the Touchstone setup."
timestamp: "2026-10-07T20:09:47.7172350Z"
status: "deprecated"
created: "2026-10-07T20:09:47.3591940Z"
slug: "readme-changelog-drift"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["release-process", "touchstone-test-architecture", "solution-projects-and-targets"]
supersededBy: "mem_muykm8l0_p0eb7RdlzP2dDOum7GE"
version: "1"
salience: "0.5"
---
As of 2026-10-07:
- CHANGELOG.md has "Current Version: v2.0.0" but no 2.0.1 entry. 2.0.1 fixed the ToString TCP race and updated dependencies; see [[v2-0-1-tostring-race-fix]].
- README.md "Example Projects" lists Test and "Test.Automated - Automated test suite with 54 cross-platform tests". Tests are now Touchstone suites in Test.Shared (roughly 90 cases across 7 suite files), run by Test.Automated, Test.Xunit, and Test.Nunit.
- The library references System.Text.Json 10.0.11, but no .cs file in src/PerformanceStatistics uses it. It is likely an unneeded dependency (only the Test app serializes JSON); consider removing it.
