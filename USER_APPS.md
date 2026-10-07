# User Applications Plan

A plan for two applications built on the PerformanceStatistics library:

1. **A terminal app (TUI)** in C#, built on the [TUIKit](https://www.nuget.org/packages/TUIKit) NuGet package.
2. **A web app**: a React (JavaScript) front end and a C# back-end server, run with Docker Compose.

## Status (2026-10-07)

All five phases are implemented on branch `multiplatform-hardening`. See [docs/TASKMANAGER.md](docs/TASKMANAGER.md) for usage.

| Milestone | State |
|---|---|
| 0. Library gaps (v2.2.0) | Done. All-process enumeration with executable path and metadata; per-process disk I/O; per-core CPU; memory totals; swap; per-interface network; volumes; load; `SystemInfo`; `ProcessActions`; `PERFSTATS_HOST_ROOT`. |
| 1. `PerformanceStatistics.Monitoring` 1.0 | Done. Snapshots, sampler, history buffer, catalog, formatter, accounting, action policy, SQLite store with rollups, client. |
| 2. TUI | Done. Five tabs, process detail with history, actions, local and `--record` modes, remote `--server` mode. Headless tests. |
| 3. Server and agent | Done. API, WebSocket stream, auth, audit, agent with offline buffer and local policy; installers for systemd, launchd, and Windows services. End-to-end tests. |
| 4. Web via Compose | Done. Hosts, Overview, Processes (virtualized, column picker, detail drawer, actions, exited processes), Network, Disks, Accounting (CSV/JSON), Audit; light and dark themes. `docker compose up` verified; Playwright tests. |
| 5. Parity and accounting | Done, in both apps (Accounting tab; `/accounting` API). |
| 6. Hardening | Partly done. Auth, action policies, retention and downsampling, and CI on all three OSes are in place. Still open: Windows runs, and the stretch items below. |

**Decisions on the open questions** (from the earlier draft; change them if you prefer otherwise):

- Scope: fleet-capable. A single machine works with the TUI alone, or with one agent.
- Server: ASP.NET Core minimal APIs.
- Web-triggered actions: off by default, enabled per agent with `TASKMGR__ALLOWEDACTIONS`, admin only, protected system processes refused, PID-reuse guard, audited.
- Names: `TaskManager.*` / `taskmgr` (still a placeholder; rename before publishing).
- Charts: uPlot.

**Not done yet:**

- TCP connections with the owning PID.
- Per-physical-disk breakdown (totals and per-volume space exist).
- Per-process network bytes (needs kernel tracing).
- GPU, temperature, and power.
- Page faults and context switches per process.
- Running on Windows: CI covers it, but nothing has been executed on a Windows machine yet.

## Goal

Task Manager (Windows), Activity Monitor (macOS), and `top`/`htop`/`ps` (Linux) disagree on what they show and how they compute it:

- CPU % per core vs. per machine.
- What counts as "free" memory.
- Whether disk numbers are rates or totals.
- Which process columns exist at all.

These apps provide **one interface and one set of definitions on all three operating systems**. The same process, metric, and column mean the same thing everywhere. A metric that one OS cannot provide is shown as "n/a" with a reason; it is never a silent `0`.

The library's v2.1.0 release (see CHANGELOG.md) laid the groundwork. Rates, units, rounding, and sampling are now implemented once in shared base classes. Each OS backend supplies only raw readings.

---

## Requirements

Both apps must chart everything below **over time** as well as show it live. A metric the OS cannot provide appears as "n/a" with a reason.

### Per-process telemetry (the process list, sampled every interval and kept as history)

| Field | Windows | Linux | macOS | Library today |
|---|---|---|---|---|
| PID, parent PID | `Process.Id`, toolhelp snapshot | `/proc/<pid>/stat` | `proc_pidinfo(PROC_PIDTBSDINFO)` | PID only |
| Name, **executable path** | `QueryFullProcessImageName` | `readlink /proc/<pid>/exe` | `proc_pidpath` | name only (Phase 0) |
| Command line, working directory | PEB via `NtQueryInformationProcess` | `/proc/<pid>/cmdline`, `cwd` | `sysctl KERN_PROCARGS2`, `proc_pidinfo(PROC_PIDVNODEPATHINFO)` | Phase 0 |
| User, session, start time, uptime | `OpenProcessToken`, `Process.StartTime` | `/proc/<pid>/status` Uid, stat starttime | `PROC_PIDTBSDINFO` uid, start time | Phase 0 |
| State (running, sleeping, stopped, zombie), priority / nice | `PriorityClass`, suspended threads | stat state, nice | `pbi_status`, `pbi_nice` | Phase 0 |
| **CPU %** (0–100 of machine), cumulative CPU time | `TotalProcessorTime` | same | same | ✓ |
| **Memory**: working set/resident, private/footprint, virtual, peaks | `Process` | `Process`, `/proc/<pid>/status` VmHWM | `proc_pid_rusage` | ✓ (peaks are null on macOS) |
| **Disk I/O**: read/write bytes/sec, cumulative bytes | `GetProcessIoCounters` | `/proc/<pid>/io` | `proc_pid_rusage` diskio | Phase 0 |
| **Handle / file descriptor count** | `HandleCount` | `/proc/<pid>/fd` | `proc_pidinfo(PROC_PIDLISTFDS)` | ✓ |
| **Thread count** | `Process.Threads` | same | same | ✓ |
| Page faults/sec, context switches/sec | `GetProcessMemoryInfo`, thread times | `/proc/<pid>/stat` minflt/majflt, `status` ctxt switches | `proc_pid_rusage` pageins, `proc_pidinfo(PROC_PIDTASKINFO)` faults/csw | Phase 0 |
| Open TCP connections (count and list) | `GetExtendedTcpTable` | `/proc/net/tcp*` + fd inodes | `proc_pidfdinfo` sockets | Phase 0 |
| Extras where available: file version, company, and signer (Windows); cgroup or container ID (Linux); code-signing team ID and architecture (macOS) | version info | `/proc/<pid>/cgroup` | `proc_pidinfo`, `codesign` APIs | stretch |

Per-process **network** bytes/sec are not exposed by any OS without kernel tracing (ETW on Windows, eBPF on Linux, `nettop` on macOS). They are a stretch goal behind a capability flag, not part of the MVP.

### System telemetry (charted over time)

| Series | Detail | Library today |
|---|---|---|
| **CPU, aggregate and per core** | busy %, split into user, system, iowait (Linux), and idle; per-core % | aggregate ✓; per-core and splits in Phase 0 |
| **Memory** | total, used, available, cached/file-backed, compressed (macOS), swap or commit used and total, page faults and swap in/out per second | available ✓; the rest in Phase 0 |
| **Network** | per interface and total: bytes/sec in and out, packets/sec, errors and drops; TCP connections by state | Phase 0 (`NetworkInterface` statistics through `DeltaSampler`); TCP list ✓ |
| **Disk I/O** | per physical disk and total: read and write ops/sec, bytes/sec, queue length (n/a on macOS), busy % where available | ops and queues ✓ (totals); bytes, per-disk, and busy % in Phase 0 |
| **Disk space** | per volume and total: size, used, free, % | totals ✓; per-volume in Phase 0 |
| **Load and scheduler** | load average 1/5/15 (Linux, macOS), runnable processor-queue length (Windows), context switches and interrupts per second | Phase 0 |
| **Population** | process count, thread count, handle/fd count (system-wide) | Phase 0 |
| **Uptime and host facts** | boot time, OS and version, CPU model, core counts (physical and logical), total memory | Phase 0 (`ISystemInfo`) |
| **Stretch, platform-variable** | GPU utilization and memory, CPU temperature and frequency, battery and power draw | behind capability flags |

### "Over time" means three tiers of history

1. **Live history in memory:** one `HistoryBuffer` ring per series and per process (default 15 minutes at 1s). It drives sparklines and charts in both UIs with no storage dependency, so the TUI works fully offline.
2. **Persisted history:**
   - The server keeps SQLite (or PostgreSQL) series with downsampling: raw 1s for 1 hour, 1-minute averages, minimums, and maximums for 30 days, and 1-hour for 1 year.
   - The TUI can optionally write the same schema to a local SQLite file (`--record <file>`), so a standalone machine also keeps days of history.
3. **Per-process history:**
   - Every process is recorded every tick while it is in the top N for any metric (default 50 by CPU, memory, disk, or handles) or is pinned by the user.
   - All other processes are recorded at 1-minute resolution.
   - Exited processes keep their history, with exit time and lifetime totals, until retention expires.
   - This keeps storage bounded while letting you chart "what was using the disk at 3am".

---

## Architecture

```
                      ┌───────────────────────────────────────────────┐
                      │ PerformanceStatistics (library, NuGet)        │
                      │ raw OS readings → consistent counters         │
                      └───────────────────────▲───────────────────────┘
                                              │
                      ┌───────────────────────┴───────────────────────┐
                      │ PerformanceStatistics.Monitoring (new, NuGet) │
                      │ snapshots, sampler loop, history ring,        │
                      │ metric catalog, process actions, DTOs         │
                      └───▲──────────────────▲──────────────────▲─────┘
                          │                  │                  │
               ┌──────────┴───┐     ┌────────┴──────┐   ┌───────┴────────┐
               │ TaskManager  │     │ TaskManager   │   │ TaskManager    │
               │ .Tui         │     │ .Agent        │   │ .Server        │
               │ (TUIKit)     │     │ (native svc   │──▶│ (ASP.NET Core, │
               │ local or     │     │  on each host)│   │  Docker)       │
               │ --server URL │     └───────────────┘   └───────▲────────┘
               └──────────────┘                                 │ REST + WebSocket
                                                        ┌───────┴────────┐
                                                        │ web (React,    │
                                                        │ nginx, Docker) │
                                                        └────────────────┘
```

**Key constraint: a container cannot monitor its host on macOS or Windows.** Docker Desktop runs containers in a Linux VM, so a containerized collector on a Mac or Windows machine would report the VM, not the machine. The design therefore splits the system in two:

- **Collection** runs natively. The **Agent** is a small .NET service installed on each machine you want to watch: a Windows Service, a launchd daemon, or a systemd unit. On Linux it can also run in Compose with `pid: host`.
- **Presentation and history** run in Docker. The **Server** stores history and serves the API. The **web** container serves the React app.

The **TUI** reads the local machine in-process by default, with no agent or server needed. With `--server <url>` it browses any host the server knows about. Both apps call the same Monitoring layer, and the TUI's remote mode uses the same API as the web app, so the two cannot drift apart.

### Repository layout (proposed)

```
src/
  PerformanceStatistics/              existing library (NuGet)
  PerformanceStatistics.Monitoring/   NEW shared layer (NuGet): snapshots, sampler, history, catalog, actions
  TaskManager.Tui/                    NEW TUIKit app; `dotnet tool` + self-contained single-file builds
  TaskManager.Agent/                  NEW worker service; pushes snapshots to the server; optional local API
  TaskManager.Server/                 NEW ASP.NET Core API + WebSocket + SQLite history (Docker image)
  Test.Monitoring/, Test.Tui/, Test.Server/   Touchstone suites, following the existing Test.Shared pattern
web/                                  NEW React + Vite (JavaScript) app
docker/
  compose.yaml                        server + web (+ optional linux agent profile)
  server.Dockerfile, web.Dockerfile, nginx.conf
```

---

## Phase 0: Library gaps a task manager needs

Today the library monitors processes **by name** and exposes ten system counters. A task manager needs more. These items belong in the library, so the per-OS code stays behind the existing hook pattern (`internal` hooks on the base classes, per-OS `*NativeMethods`):

| Gap | Windows | Linux | macOS |
|---|---|---|---|
| **Enumerate all processes** (not by name), with every per-process field in [Requirements](#requirements): executable path, parent PID, user, start time, command line, state, priority | `Process.GetProcesses()`, `CreateToolhelp32Snapshot` (parent PID), `NtQueryInformationProcess` (command line), `OpenProcessToken` (user) | `/proc/<pid>/stat`, `status`, `cmdline` | `proc_listallpids`, `proc_pidinfo(PROC_PIDTBSDINFO)` |
| **Total, used, and cached physical memory**, compressed (macOS), swap/commit, page faults and swap in/out per second | `GlobalMemoryStatusEx` | `/proc/meminfo` | `hw.memsize`, `vm.swapusage` |
| **Per-core CPU** with user/system/iowait split, uptime, load average (Unix), context switches and interrupts, process/thread/handle totals | `NtQuerySystemInformation` / PDH | `/proc/stat` per-cpu, `/proc/uptime`, `/proc/loadavg` | `host_processor_info`, `kern.boottime`, `getloadavg` |
| **Per-process disk I/O bytes**, page faults, context switches | `GetProcessIoCounters` | `/proc/<pid>/io` (own processes, or root) | `proc_pid_rusage` `ri_diskio_bytesread/written` |
| **Network throughput** per interface: bytes, packets, errors, drops | `NetworkInterface.GetIPStatistics()` (cross-platform .NET), sampled with DeltaSampler | same | same |
| **TCP connections with owning PID** | `GetExtendedTcpTable` | `/proc/net/tcp{,6}` + `/proc/<pid>/fd` socket inodes | `proc_pidfdinfo(PROC_PIDFDSOCKETINFO)` |
| **Actions**: kill, terminate tree, priority, suspend/resume | `Process.Kill(true)`, `PriorityClass`, `NtSuspendProcess` | `kill(SIGTERM/SIGKILL/SIGSTOP/SIGCONT)`, `setpriority` | same as Linux |
| **Per-disk and per-volume** detail: bytes/sec, busy %, per-volume space | PhysicalDisk instances, `DriveInfo` | `/proc/diskstats` sectors and io_ticks | IOKit "Bytes (Read/Write)" |
| **Container-aware Linux root** (read `/host/proc`, `/host/sys`, host mounts) so a containerized agent reports the host | n/a | configurable root path | n/a |

Proposed public API (additive, minor-version bump):

```csharp
public interface IPerformanceStatistics
{
    // existing members ...
    IReadOnlyList<IProcessCounters> GetAllProcesses();   // reuses counters per PID, like MonitoredProcesses
    ISystemInfo SystemInfo { get; }                      // static facts: OS, cores, total memory, boot time
}

public interface IProcessCounters
{
    // existing members ...
    int? ParentProcessId { get; }
    string ExecutablePath { get; }
    string UserName { get; }
    DateTime? StartTimeUtc { get; }
    string CommandLine { get; }
    long? DiskReadBytesPerSecond { get; }
    long? DiskWriteBytesPerSecond { get; }
    double? PageFaultsPerSecond { get; }
    double? ContextSwitchesPerSecond { get; }
}

public interface ISystemCounters
{
    // existing members ...
    IReadOnlyList<double?> PerCoreCpuUtilizationPercent { get; }
    double? MemoryTotalMegabytes { get; }
    double? MemoryUsedMegabytes { get; }
    double? SwapUsedMegabytes { get; }
    IReadOnlyList<INetworkInterfaceCounters> NetworkInterfaces { get; }  // bytes/packets/errors per second
    IReadOnlyList<IDiskCounters> Disks { get; }                          // per physical disk ops, bytes, queue, busy %
    IReadOnlyList<IVolumeCounters> Volumes { get; }                      // per volume space
}
```

Every new metric goes through `DeltaSampler` (for rates) and follows the existing rules: `null` means unavailable, getters never throw, and output is formatted with the invariant culture. Each new metric also gets a `ConsistencySuite` test that asserts the same semantics on all three OSes.

**Exit criteria:** CI is green on Windows, Linux, and macOS. `GetAllProcesses()` for about 500 processes, sampled every second, costs less than 2% of one core on each OS.

---

## Phase 1: `PerformanceStatistics.Monitoring` (the shared layer)

This layer is what makes the two apps behave the same way. Both apps consume it; neither talks to the raw library directly.

- **Snapshots (immutable DTOs, JSON-serializable):**
  - `HostSnapshot` holds the timestamp, host ID, OS, a `SystemSnapshot`, a `ProcessSnapshot[]`, a `NetworkSnapshot`, and a `DiskSnapshot[]`.
  - These are plain records with explicit units in their names (`MemoryUsedBytes`, `CpuPercent`). The TUI renders them directly, and the server sends them over the wire unchanged.
- **`MetricsSampler`:** one background loop per host, with a configurable interval (1s default).
  - It reads the library once per tick, builds a snapshot, and publishes it through `IObservable<HostSnapshot>`, an event, or a channel.
  - It is the only thing that touches `IPerformanceStatistics`, which avoids races from several readers.
- **`HistoryBuffer`:** a ring buffer per system series and per process (default 15 minutes at 1s) that feeds sparklines and charts in both apps. It implements the three history tiers in [Requirements](#requirements): an optional `IHistoryStore` (SQLite) persists the same series with downsampling, and the top-N plus pinned-process policy decides per-process resolution.
- **`MetricCatalog`:** the single source of truth for every metric and column. It records the ID, display name, unit, formatter, description, and **per-OS availability with a reason**, such as "Handle count = open file descriptors on Linux/macOS" or "Disk queue not exposed by macOS". Both UIs build their columns, tooltips, and "n/a" badges from it.
- **`ProcessActions`:** kill, terminate tree, set priority, suspend, and resume, with one cross-platform priority scale (Low, BelowNormal, Normal, AboveNormal, High) mapped to Windows priority classes or Unix nice values. Every action returns a result (`Ok`, `Denied`, `NotFound`, `Unsupported`) instead of throwing.
- **Formatting:** bytes (binary units), percentages, and durations, the same in both UIs.
- **Accounting:** an `Accumulator` that integrates per-process CPU-seconds, disk bytes, and peak memory over a window, keyed by (host, process name, user). This produces the "who used what" reports.

---

## Phase 2: TUI app (`TaskManager.Tui`, TUIKit)

**Package:** `<PackageReference Include="TUIKit" Version="1.5.*" />`. Reference the package, not the source. Start from the template: `dotnet new install TUIKit.Templates`, then `dotnet new tuikit-app -n TaskManager.Tui`.

**Hosting:**
- Start with `TuiApp.RunAsync(app => { ... })` (Hosting/TuiApp.cs), which handles Ctrl+C and cleanup.
- A background `MetricsSampler` produces snapshots off the UI thread. Each update is marshalled to the UI loop with `app.Post(() => ...)`. `DataTable<T>` is documented as not thread-safe, and `Sparkline`, `Gauge`, and `LineChart` have no locks, so **every widget mutation goes through `Post`**.
- Set `app.Theme` (Dark, Light, or HighContrast) and `app.ApplyThemeToWidgets = true`.

**Layout:** a `TabView` with five tabs.

| Tab | Content (TUIKit widgets) |
|---|---|
| **Overview** | A 2×2 `FramedStack` of `LineChart`s with a selectable window (1m, 5m, 15m, or persisted history with `--record`): CPU `LineChart` (total plus per-core sparklines), memory `Gauge` with used/available, disk read/write `Sparkline`s, network in/out `Sparkline`s. A header line shows OS, uptime, cores, and total memory. |
| **Processes** | A `DataTable<ProcessSnapshot>` with `KeySelector = p => p.Pid`, so the selection survives refreshes. Columns come from `MetricCatalog`: PID, name, executable path, user, CPU %, memory, disk read/write, handles/fds, threads, start time, and more, chosen with a column picker. `Enter` opens a **process detail view**: metadata (path, command line, parent, user, start time) and `LineChart`s of that process's CPU, memory, disk I/O, handles, and threads over time. A `TextField` filter sits above the table; filtering happens before `Bind`. Sorting uses the header click or `SortByColumn`. `MultiSelect` allows bulk actions. |
| **Network** | Per-interface in/out throughput charts over time, plus TCP connections (local, remote, state, owning process) in a `DataTable`. |
| **Disks** | Volumes (size, used, free, %), plus per-disk ops, bytes, and queue charts over time. "n/a" badges come from the catalog. |
| **History / Accounting** | Top consumers over the window (CPU-seconds, peak memory, disk bytes), with `LineChart` history for the selected process. |

**Interaction:**
- A `StatusBar` bound with `app.BindKeyHints(status)` shows only the keys valid for the focused widget.
- Help is on both **F1** and **?**, because macOS can intercept F-keys. Key labels come from `chord.ToLabel(KeyLabel.Recommended)`.
- Process actions:
  - `k` kills after `app.ConfirmAsync(...)`.
  - `K` terminates the whole tree.
  - `p` sets priority through `app.SelectAsync("Priority", ...)`.
  - `s` suspends or resumes.
  - Results appear as `app.Notify(text, NotificationSeverity...)` toasts, for example "Denied: run elevated to signal processes owned by root".
- Other keys: `/` focuses the filter, `Tab` cycles regions (with `HighlightFocusedRegion` on), `1`–`5` switch tabs, `+`/`-` change the sample interval, `space` pauses, `q` or `ctrl+q` quits.
- Mouse is on by default: header click sorts, wheel scrolls.

**Modes and distribution:**
- Modes: `taskmgr` (local, in-process) and `taskmgr --server http://host:8080 [--host <id>]` (remote, using the same snapshots over the API).
- Distribution:
  - A `dotnet tool` (`dotnet tool install -g TaskManager.Tui`).
  - Self-contained single-file builds for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
  - A note on elevated runs (Administrator, or `sudo`) to see every process's details.

**Testing:**
- Screen tests use `HeadlessBackend` (`FeedKey`, `FeedClick`, `Resize`, `CaptureFrame`, `Snapshot.ToText`). The sampler is fed fake snapshots, so the tests are deterministic.
- `WidgetTester.For(...)` covers individual widgets.
- `FocusAudit.Run(app).ThrowIfProblems()` checks Tab order.
- Manual smoke tests in Windows Terminal, macOS Terminal and iTerm2, and a Linux terminal. If stale cells appear in Windows Terminal, set `ForceFullRepaint = true`.

---

## Phase 3: Server and agent (C#)

**`TaskManager.Agent`** is a .NET worker service:
- It runs `MetricsSampler` and pushes compressed snapshot batches to the server (`POST /api/v1/ingest`, bearer agent key), buffering locally when the server is unreachable.
- It polls for pending actions and executes them only if the agent's local policy allows them. Actions are **disabled by default**; enable them per action and per user or process allowlist.
- Install scripts come in `.sh`/`.bat` pairs:
  - Windows: `sc create`.
  - macOS: launchd plist.
  - Linux: systemd unit.

**`TaskManager.Server`** uses ASP.NET Core minimal APIs (Kestrel), with background services for retention and downsampling:

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/hosts` | Known hosts, OS, and last-seen time |
| `GET /api/v1/hosts/{id}/snapshot` | Latest `HostSnapshot` |
| `GET /api/v1/hosts/{id}/processes?sort=&filter=&top=` | Process list, sorted and filtered on the server |
| `GET /api/v1/hosts/{id}/history?metric=&from=&to=&step=` | System time series for charts (aggregate, per core, per interface, per disk) |
| `GET /api/v1/hosts/{id}/processes/{pid}/history?metric=&from=&to=&step=` | Per-process time series, including exited processes (keyed by PID + start time) |
| `GET /api/v1/hosts/{id}/accounting?window=&groupBy=name\|user` | Resource accounting report |
| `GET /api/v1/catalog` | `MetricCatalog`: units, descriptions, per-OS availability |
| `POST /api/v1/hosts/{id}/processes/{pid}/actions` | Kill, tree, priority, or suspend; queued for the agent and audited |
| `WS /api/v1/stream?host=` | Live snapshots (one message per tick) |
| `POST /api/v1/ingest` | Agent → server batches |

**Server details:**
- **Storage:** SQLite on a Docker volume by default, with optional PostgreSQL. Retention keeps raw 1s data for 1 hour, 1-minute aggregates for 30 days, and 1-hour aggregates for 1 year.
- **Auth:**
  - Agent keys for ingest.
  - User login with bearer sessions for the web app and remote TUI.
  - An admin role is required for actions, and every action is written to an audit log.
- **OpenAPI** document generated from the endpoints. The web client and the TUI's remote mode both use the same DTOs from `PerformanceStatistics.Monitoring`.

---

## Phase 4: Web app (React + JavaScript, Docker Compose)

**Stack:**
- React with Vite in **JavaScript** (not TypeScript), using PropTypes or JSDoc for shapes.
- TanStack Table for the virtualized process grid.
- A lightweight time-series chart library (uPlot or Recharts).
- The native `WebSocket` for live data.
- Served by nginx, which also reverse-proxies `/api` and the WebSocket to the server.

**Pages (matching the TUI tabs):**
- **Hosts:** a fleet grid with CPU and memory sparklines and status per host.
- **Host → Overview / Processes / Network / Disks / Accounting:** the same columns, units, and "n/a" reasons as the TUI, all driven by `/api/v1/catalog`. Every chart has a time-range picker (live, 15m, 1h, 24h, 7d, 30d, custom) backed by `/history`. A process detail page charts every per-process series over its lifetime, including after it exits.
- **Processes:**
  - Search, sort, and multi-select.
  - Kill, priority, and suspend, each behind a confirmation dialog, shown only when the server reports the action is enabled for that host.
  - A detail drawer with history charts.
- **Accounting:** time window picker, group by process or user, CSV/JSON export.
- **Settings:** theme (light, dark, or system), sampling interval, retention.

**`docker/compose.yaml` (sketch):**

```yaml
services:
  server:
    build: { context: .., dockerfile: docker/server.Dockerfile }
    environment:
      - TASKMGR__STORAGE=sqlite:/data/taskmgr.db
      - TASKMGR__ADMINPASSWORD=${TASKMGR_ADMIN_PASSWORD:?set it}
    volumes: [ "taskmgr-data:/data" ]
    ports: [ "8080:8080" ]
    healthcheck: { test: ["CMD", "wget", "-qO-", "http://localhost:8080/health"], interval: 10s }

  web:
    build: { context: .., dockerfile: docker/web.Dockerfile }   # node build stage -> nginx stage
    ports: [ "3000:80" ]
    depends_on: { server: { condition: service_healthy } }

  agent-linux:                       # optional: monitor the Docker host itself (Linux hosts only)
    profiles: [ "linux-agent" ]
    build: { context: .., dockerfile: docker/agent.Dockerfile }
    pid: host                        # see host processes
    network_mode: host               # see host interfaces and TCP connections
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/host/root:ro              # host mounts for disk space
    environment:
      - TASKMGR__SERVER=http://localhost:8080
      - TASKMGR__AGENTKEY=${TASKMGR_AGENT_KEY:?set it}
      - PERFSTATS__HOSTROOT=/host    # Phase 0: container-aware Linux root

volumes:
  taskmgr-data:
```

Macs and Windows machines run the native agent (Phase 3) and point it at the server's URL.

---

## Cross-platform consistency rules (apply to every phase)

1. **One definition per metric**, documented in `MetricCatalog` and the README tables. Examples:
   - CPU % is 0–100 of the whole machine.
   - Memory "available" is what can be allocated without swapping (`MemAvailable` on Linux, the Activity Monitor formula on macOS, `ullAvailPhys` on Windows).
   - Handle count is open handles on Windows and open file descriptors elsewhere.
2. **Never fake a value.** Unavailable means `null` in the library, "n/a" plus a tooltip reason in the UIs, and an omitted field in JSON.
3. **Same sampling everywhere:** the `DeltaSampler` semantics, one sampler per host, and the timestamp is part of every snapshot.
4. **Same units everywhere:** bytes in the API, formatted with binary units in the UIs, and rates per second.
5. **Privilege differences are explicit.** When a process's details are hidden because it belongs to another user, the UI says so; it never shows zeros.
6. **Every rule has a test:** `ConsistencySuite`-style tests in the library and Monitoring layer, run by CI on Windows, Linux, and macOS.

---

## Testing and CI

| Layer | Tests | Runs on |
|---|---|---|
| Library + Monitoring | Touchstone suites (`Test.Shared` pattern), consistency and regression cases | CI matrix: Windows, Linux, macOS × net8.0, net10.0 |
| TUI | HeadlessBackend screen tests with a fake sampler; focus audit | CI matrix (all three OSes) |
| Server | API tests against an in-memory host with SQLite in a temp directory; auth and action-policy tests | Linux CI |
| Web | Vitest component tests; Playwright end-to-end tests against `docker compose up` with a fake agent | Linux CI |
| Agent | Install and uninstall script smoke tests per OS; ingest buffering | CI matrix plus a manual release checklist |

---

## Milestones

| # | Deliverable | Done when |
|---|---|---|
| 0 | Library gaps (Phase 0), v2.2.0 | All-process enumeration, totals, per-core, I/O, network, actions; CI green on all three OSes |
| 1 | `PerformanceStatistics.Monitoring` 1.0 | Snapshots, sampler, catalog, history, accounting; JSON contract frozen |
| 2 | TUI MVP | Overview (aggregate and per-core CPU, memory, network, disk charts) and Processes tabs (all per-process fields, detail view with history), filter, sort, kill and priority, local mode; single-file builds for all six runtime identifiers |
| 3 | Server and agent MVP | Ingest, snapshot, processes, history, stream; SQLite; agent installers for three OSes |
| 4 | Web MVP via Compose | Hosts, overview, and processes pages live over WebSocket; `docker compose up` works from a clean clone |
| 5 | Parity and accounting | Network, Disks, and Accounting in both apps; TUI remote mode; audit log; CSV export |
| 6 | Hardening | Auth, action policies, retention and downsampling, performance budgets, docs |

---

## Open questions

1. **Scope: one machine or a fleet?** The plan supports both. If you only need "my machine, same UI everywhere", milestones 3 and 4 can collapse into the agent hosting the API and the web app locally, with Compose used only for development.
2. **Server framework:** ASP.NET Core minimal APIs (recommended: built-in WebSockets, OpenAPI, hosted services), or Watson Webserver for consistency with your other projects.
3. **Process actions from the web:** keep them off by default with a per-agent allowlist (recommended), or allow them for admins without agent-side opt-in.
4. **Naming:** `TaskManager.*` is a placeholder. Pick a product name before publishing packages and images.
5. **Charts in the web app:** uPlot (small and fast for dense time series) or Recharts (more React-idiomatic).
