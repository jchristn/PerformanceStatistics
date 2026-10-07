---
type: "Project"
title: "Code style: getters never throw, null means unavailable, invariant formatting, explicit types, _PascalCase fields"
description: "Getters return null (unavailable), never throw; wrong-OS constructors throw PlatformNotSupportedException; format with InvariantCulture; no var; _PascalCase private fields; #region blocks; XML docs on everything."
timestamp: "2026-10-07T20:38:44.2986740Z"
created: "2026-10-07T20:08:47.6357940Z"
slug: "never-throw-return-null"
category: "cat_muyjh8gh_3Sv0b6Ggpoz9xqbqbI4"
links: ["namespace-and-folder-layout"]
version: "2"
salience: "0.5"
---
Rules for src/PerformanceStatistics (the whole library follows them as of v2.1.0):
- Getters never throw. Platform hooks return bool/out or a nullable, and the base classes wrap them in Safe* methods. Unavailable means null: on that platform, for that process (another user's), or after Dispose. Collections return empty, never null.
- Intentional throws are limited to:
  - `PerformanceStatisticsFactory.Create` on an unknown OS.
  - A platform system-counters or statistics constructor on the wrong OS: `PlatformNotSupportedException`, raised in the SystemCountersBase constructor.
  - A process counters constructor given a null Process: `ArgumentNullException`.
- Format numbers with CultureInfo.InvariantCulture and round with Math.Round. Never use the old `Convert.ToDouble(string.Format("{0:N2}", x))`.
- No `var`; write explicit types.
- Private fields are `_PascalCase` (`_Lock`, `_Process`, `_Disposed`).
- Members go in #region blocks: Public-Members, Private-Members, Constructors-and-Factories, Public-Methods, Internal-and-Protected-Methods / Internal-Methods, Private-Methods.
- XML doc comments on every public, protected, and internal member. GenerateDocumentationFile writes src/PerformanceStatistics/PerformanceStatistics.xml, which is tracked in git, so commit it after API or doc changes.
- `using` directives sit outside the namespace (this repo's existing style), and namespaces are block-scoped.
- Compare strings with String.Equals or StartsWith using StringComparison.Ordinal.
- Native interop goes in a per-OS *NativeMethods class. Parsers and computations go in ProcFileParser / MacSystemInfo.
- Platform classes get `[SupportedOSPlatform]` under `#if NET5_0_OR_GREATER`. Test.Shared suppresses CA1416 because its platform suites are guarded at runtime.
