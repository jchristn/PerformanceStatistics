---
okf_version: "0.2"
---
# cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly

* [Rate sampling: DeltaSampler gives every platform the same CPU and disk-ops semantics](cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly/cpu-sampling-model.md) - Internal/DeltaSampler turns cumulative counters into rates since the previous read: first read waits ≥100ms, reads <100ms apart return the cached value, a progress predicate handles macOS bursty CPU ticks; Stopwatch timing.
* [MonitoredProcesses re-scans names but reuses one counter instance per running PID](cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly/monitored-processes-lifecycle.md) - PerformanceStatisticsBase tracks counters by PID: a running process keeps its IProcessCounters (refreshed) across accesses so CPU is measured between them; exited PIDs, reused PIDs, and removed names are disposed.
* [Folder layout: shared bases at the root, Internal/ helpers, thin per-OS folders](cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly/namespace-and-folder-layout.md) - src/PerformanceStatistics has root base classes, Interfaces/, Internal/ (DeltaSampler, Samples, DiskSpace), and Windows/, Linux/, Mac/ folders, each holding three thin public classes plus native/parse helpers.
* [Public API: three interfaces, three shared base classes, and PerformanceStatisticsFactory](cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly/public-api-and-factory.md) - Consumers call PerformanceStatisticsFactory.Create() to get an IPerformanceStatistics; concrete Windows/Linux/Mac classes derive from PerformanceStatisticsBase, SystemCountersBase, and ProcessCountersBase (since v2.1.0).

# cat_muyjh704_U65ZYmJoqusyC0RWeiM

* [Disk metric sources by platform (ops, queues, space)](cat_muyjh704_U65ZYmJoqusyC0RWeiM/disk-ops-queue-approximations.md) - All three platforms report separate read and write ops/sec over physical disks; queues are Windows averages, Linux in-flight counts, and null on macOS; disk space is DriveInfo fixed drives de-duplicated by (format, size, free).
* [Linux backend: /proc and /sys via ProcFileParser](cat_muyjh704_U65ZYmJoqusyC0RWeiM/linux-backend.md) - CPU from /proc/stat, memory from MemAvailable, disk ops from /proc/diskstats limited to physical disks listed in /sys/block, queues from /sys/block/*/inflight, HandleCount = /proc/<pid>/fd entries.
* [macOS backend: Mach host statistics, sysctl, libproc, and IOKit stats (ioreg)](cat_muyjh704_U65ZYmJoqusyC0RWeiM/mac-backend.md) - macOS uses P/Invoke (host_statistics, host_statistics64, sysctlbyname, proc_pidinfo, proc_pid_rusage) plus `ioreg -c IOBlockStorageDriver` for disk ops; memory available = memsize − (internal−purgeable + wired + compressed).
* [Windows backend: kernel32 for CPU and memory, reused PhysicalDisk counters for disk I/O](cat_muyjh704_U65ZYmJoqusyC0RWeiM/windows-backend.md) - Windows CPU from GetSystemTimes, memory from GlobalMemoryStatusEx, disk ops from the RawValue of long-lived PhysicalDisk(_Total) Disk Reads/Writes/sec counters, queues via NextValue; process fields from System.Diagnostics.Process.

# cat_muyjh8gh_3Sv0b6Ggpoz9xqbqbI4

* [Code style: getters never throw, null means unavailable, invariant formatting, explicit types, _PascalCase fields](cat_muyjh8gh_3Sv0b6Ggpoz9xqbqbI4/never-throw-return-null.md) - Getters return null (unavailable), never throw; wrong-OS constructors throw PlatformNotSupportedException; format with InvariantCulture; no var; _PascalCase private fields; #region blocks; XML docs on everything.
* [This scope's OKF bundle is committed at .okf/ and kept in sync by agents](cat_muyjh8gh_3Sv0b6Ggpoz9xqbqbI4/okf-mirror-in-repo.md) - The NotDory server is remote and can't see the checkout (filesystemMirror off), so after every memory_upsert/delete agents rewrite .okf/<categoryId>/<slug>.md and regenerate .okf/index.md (OKF v0.2), committed with the code it describes.

# cat_muyjha72_jbjNYfjl2iB8LMFXXdV

* [Release process: bump the csproj version and notes, update CHANGELOG and README, let CI pass on all three OSes](cat_muyjha72_jbjNYfjl2iB8LMFXXdV/release-process.md) - Bump <Version> and <PackageReleaseNotes> in src/PerformanceStatistics/PerformanceStatistics.csproj, add a CHANGELOG "Current Version" section (moving the old one under Previous Versions), update README "New in", commit the regenerated PerformanceStatistics.xml.
* [Solution projects, target frameworks, dependencies, and CI](cat_muyjha72_jbjNYfjl2iB8LMFXXdV/solution-projects-and-targets.md) - src/PerformanceStatistics.sln: library (netstandard2.1;net8.0;net10.0, only depends on System.Diagnostics.PerformanceCounter) plus Test, Test.Shared, Test.Automated, Test.Xunit, and Test.Nunit; GitHub Actions matrix on Windows/Linux/macOS.
* [Tests: Touchstone suites in Test.Shared, run on each OS (macOS locally, Linux via Docker)](cat_muyjha72_jbjNYfjl2iB8LMFXXdV/touchstone-test-architecture.md) - Add tests in src/Test.Shared/Suites via TestSupport.Case; 94 cases on macOS/Linux including ConsistencySuite regressions; run Linux tests in Docker by tar-copying the repo without bin/obj.

# cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW

* [v2.0.1 (2026-08-15): snapshot MonitoredProcesses and ActiveTcpConnections once in ToString()](cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW/v2-0-1-tostring-race-fix.md) - Fixed IndexOutOfRangeException in XPerformanceStatistics.ToString() when the TCP table changed between .Length and indexing; the fix copies each volatile collection into a local once.
* [v2.1.0 (2026-10-07): shared base classes, one sampling model, native APIs per OS](cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW/v2-1-0-consistency-refactor.md) - Refactored to PerformanceStatisticsBase/SystemCountersBase/ProcessCountersBase + DeltaSampler so metrics mean the same thing on every OS; macOS moved from CLI tools to native APIs; Windows from per-read counters to kernel32 + reused counters.
* [v2.0.0 (2026-01-05): cross-platform via interfaces, a factory, and per-OS backends with nullable metrics](cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW/v2-cross-platform-design.md) - v2 introduced IPerformanceStatistics/ISystemCounters/IProcessCounters, PerformanceStatisticsFactory, and Linux/Mac backends while keeping the Windows classes usable directly.

# cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd

* [The three XPerformanceStatistics orchestrators are copy-pasted (~280 lines each)](cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd/orchestrator-duplication.md) - Windows/Linux/MacPerformanceStatistics duplicate MonitoredProcesses caching, TCP logic, GetActiveTcpConnections, ToString, and Dispose; consider an abstract base class with a CreateProcessCounters(Process) hook. (deprecated)
* [Private field casing differs: Windows uses _PascalCase, Linux/Mac use _camelCase](cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd/private-field-naming-inconsistency.md) - Windows classes use _Disposed/_Lock/_Process; Linux and Mac classes use _disposed/_lock/_process. Match the file you are editing; ask the maintainer before unifying. (deprecated)
* [Docs drift: CHANGELOG says v2.0.0 and the README describes the old 54-test Test.Automated](cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd/readme-changelog-drift.md) - The csproj is 2.0.1, but CHANGELOG.md "Current Version" is still v2.0.0 with no 2.0.1 entry; the README "Example Projects" section omits Test.Shared/Xunit/Nunit and the Touchstone setup. (deprecated)
* [Suspected: Windows rate counters read from a brand-new PerformanceCounter return ~0](cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd/windows-cpu-counter-single-sample.md) - WindowsSystemCounters creates a new counter and calls NextValue() once (CPU and Disk Reads/Writes/sec need two samples); WindowsProcessCounters calls NextValue() twice with no delay and keys the instance by ProcessName. (deprecated)
* [The v2.1.0 Windows code paths have not been executed yet; verify via CI or on a Windows machine](cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd/windows-validation-pending.md) - WindowsSystemCounters (GetSystemTimes, GlobalMemoryStatusEx, PhysicalDisk RawValue/NextValue) and the Windows suite compile but were only run on macOS/Linux; first CI run on windows-latest must pass before release.
