---
type: "Project"
title: "Release process: bump the csproj version and notes, update CHANGELOG and README, let CI pass on all three OSes"
description: "Bump <Version> and <PackageReleaseNotes> in src/PerformanceStatistics/PerformanceStatistics.csproj, add a CHANGELOG \"Current Version\" section (moving the old one under Previous Versions), update README \"New in\", commit the regenerated PerformanceStatistics.xml."
timestamp: "2026-10-07T20:39:08.7721650Z"
created: "2026-10-07T20:09:07.4789270Z"
slug: "release-process"
category: "cat_muyjha72_jbjNYfjl2iB8LMFXXdV"
links: ["solution-projects-and-targets"]
version: "2"
salience: "0.5"
---
Versions live only in src/PerformanceStatistics/PerformanceStatistics.csproj (`<Version>`, `<PackageReleaseNotes>`).

CHANGELOG.md layout (as of v2.1.0):
- "## Current Version", then the version, then ### Architecture / ### Fixes / ### Behavior changes (a before/now table).
- "## Previous Versions", with ### vX.Y.Z subsections.
- When releasing, move the current section down.

README.md:
- Add a "## New in vX.Y.Z" section above the previous one.
- Keep "Sampling Semantics" and the "Platform-Specific Metric Availability" tables in sync with behavior.

Mechanics:
- GeneratePackageOnBuild packs on every build, and IncludeSymbols produces a snupkg. The NuGet id is `PerformanceStatistics`. Publishing is manual.
- CI (.github/workflows/ci.yml) must be green on Windows, Linux, and macOS before publishing.
- Commit message patterns: "Bump version to 2.0.1"; v2.1.0 was developed on branch `multiplatform-hardening`.
