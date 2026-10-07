# TaskManager

TaskManager shows the same task manager on Windows, Linux, and macOS: every process with its PID, CPU, memory, disk I/O, handles or file descriptors, threads, executable path, command line, user, and state, plus per-core CPU, memory, swap, disk, network, and load over time. Every metric has one definition on all three systems. A value a platform cannot provide shows as `n/a` with the reason, never as a fake `0`.

There are two front ends over the same data:

| | Terminal app (`taskmgr`) | Web app |
|---|---|---|
| Runs | In your terminal, on the machine you are watching, or against a server | In a browser, against a TaskManager server |
| Data | In-process sampling, or a server's live stream | The server's live stream and stored history |
| History | 15 minutes in memory; days with `--record` | 1-second data for an hour, minutes for 30 days, hours for a year |
| Best for | One machine, SSH sessions, quick checks | Several machines, history, accounting reports |

Components:

- **`taskmgr`** (`src/TaskManager.Tui`): the terminal app, built with [TUIKit](https://www.nuget.org/packages/TUIKit).
- **`taskmgr-agent`** (`src/TaskManager.Agent`): runs on each machine and reports to the server.
- **`TaskManager.Server`** (`src/TaskManager.Server`): stores history, serves the API and live stream, and queues process actions.
- **Web app** (`web/`): React, served by nginx in Docker.
- **`PerformanceStatistics.Monitoring`**: the shared layer (snapshots, catalog, history, storage) that keeps all of the above consistent.

## Quick start

```bash
./run-tui.sh                 # terminal app for this machine (run-tui.bat on Windows); extra arguments go to taskmgr
sudo ./run-tui.sh            # see every process (on Windows, run run-tui.bat from an Administrator terminal)
./run-docker.sh              # server + web app; creates docker/.env with random secrets on first run
./run-docker.sh --linux-agent   # also monitor this Docker host (Linux)
./run-docker.sh down | logs | ps | restart
```

`run-docker.sh` (and `run-docker.bat`) prints the web URL, the admin login, and the agent key location.

### Admin privileges

Every operating system restricts what one user can see of another user's processes:

- **macOS** hides their CPU, disk I/O, open files, memory footprint, and command line.
- **Linux** hides their disk I/O and open files.
- **Windows** hides details of system and service processes.

When the collector (taskmgr in local mode, or the host's agent) is not elevated:

- The TUI shows a yellow header line saying that admin privileges may be required, with how to elevate on that platform. The help screen and the empty-table text say the same, and unavailable values carry the reason.
- The web app shows a banner on every tab of that host, a note on the host card, and the reason next to each n/a value.

The fix is to run elevated: `sudo`, or "Run as administrator". The agent installers already install the agent as root, a LocalSystem service, or a root launchd daemon.

## Terminal app

```bash
dotnet run --project src/TaskManager.Tui                     # this machine
dotnet run --project src/TaskManager.Tui -- --record ~/taskmgr.db
dotnet run --project src/TaskManager.Tui -- --server http://monitor:8080 --user admin --password '...' [--host web-01]
```

Install it as a tool with `dotnet pack src/TaskManager.Tui -o ./nupkg` and then `dotnet tool install -g TaskManager.Tui --add-source ./nupkg`. Or publish a single file:

```bash
dotnet publish src/TaskManager.Tui -c Release -f net10.0 -r <rid> --self-contained -p:PublishSingleFile=true
```

Supported RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.

### Keys

| Keys | Action |
|---|---|
| `1`–`5` | Switch tabs: Overview, Processes, Network, Disks, Accounting |
| `Tab` | Move focus between panels, including each chart. The focused chart shows `Enter: maximize`. |
| `Enter` on a chart | Maximize it to fill the screen, with a legend (latest and peak), a labeled Y axis, and time on the X axis. `Enter` or `Esc` restores it. |
| `Enter` on a row | Details in a live modal: a process (identity, path, command line, every metric with n/a reasons, CPU/memory/disk charts), a network interface (type, MAC, every IPv4/IPv6 address, throughput and packet charts), a TCP connection, a volume, an accounting group, or the Overview's core/memory panels (system details). `Esc` closes it; `↑` `↓` `PgUp` `PgDn` scroll. |
| `/` | Filter processes by PID, name, user, path, or command line. `Esc` returns to the table. |
| `c` `m` `d` `n` | Sort by CPU, memory, disk, or name (or click a column header) |
| `k` | Kill the selected process |
| `t` | Kill the selected process and its children |
| `p` | Set priority |
| `s` | Suspend or resume |
| `space` | Pause sampling |
| `=` `-` | Faster or slower sampling |
| `w` | Chart window: 1, 5, or 15 minutes |
| `g` | Accounting: group by name or user |
| `r` | Accounting: reset |
| `?` or `F1` | Help |
| `q` or `Ctrl+Q` | Quit |

Run elevated (Administrator, or `sudo`) to see every process's details. Without elevation, macOS hides CPU, I/O, and command lines for other users' processes, and Linux hides their I/O.

## Server, web app, and agents (Docker Compose)

```bash
./run-docker.sh                           # or: cp docker/.env.example docker/.env, edit it, then
                                          #     docker compose -f docker/compose.yaml up -d --build
open http://localhost:3000                # sign in as admin
```

The server listens on port 8080 and the web app on port 3000; both ports can be changed in `docker/.env`. History is stored in the `taskmgr-data` volume.

### Add machines

Run the agent natively on each machine you want to watch. It must run natively: on macOS and Windows, Docker Desktop runs containers in a Linux VM, so a container would report the VM instead of the machine.

| OS | Install (as root or Administrator) |
|---|---|
| Linux (systemd) | `sudo scripts/agent/linux/install.sh --server http://monitor:8080 --key <agent key> [--allowed-actions Kill,Suspend,Resume,SetPriority]` |
| macOS (launchd) | `sudo scripts/agent/macos/install.sh --server http://monitor:8080 --key <agent key> [...]` |
| Windows (service) | `scripts\agent\windows\install.bat http://monitor:8080 <agent key> "" Kill,Suspend,Resume` |

Each folder also has an uninstall script. To monitor the Linux Docker host itself, add `--profile linux-agent` to the compose command. That agent runs with `pid: host`, `network_mode: host`, `SYS_PTRACE`, and the host's `/proc` and `/sys` mounted at `/host`, and it sets `PERFSTATS_HOST_ROOT`.

### Settings

**Server** (`TASKMGR__*` environment variables):

| Setting | Default | Meaning |
|---|---|---|
| `TASKMGR__ADMINPASSWORD` | required | Password for the admin user (`TASKMGR__ADMINUSER`, default `admin`). Admins may request process actions. |
| `TASKMGR__AGENTKEY` / `TASKMGR__AGENTKEYS` | required | Key or keys (comma-separated) that agents send in `X-Agent-Key` |
| `TASKMGR__VIEWERPASSWORD` | empty (disabled) | Read-only `viewer` login |
| `TASKMGR__STORAGE` | `taskmgr.db` (`/data/taskmgr.db` in Docker) | SQLite file |
| `TASKMGR__RAWRETENTIONMINUTES` | 60 | Retention for 1-second data |
| `TASKMGR__MINUTERETENTIONDAYS` | 30 | Retention for 1-minute system aggregates (hourly aggregates are kept a year) |
| `TASKMGR__PROCESSMINUTERETENTIONHOURS` | 24 | Retention for 1-minute process aggregates (hourly ones are kept 30 days) |
| `TASKMGR__SESSIONHOURS` | 12 | Session lifetime |
| `TASKMGR__CORSORIGINS` | empty | Only needed if the web app is served from another origin |

**Agent:**

| Setting | Default | Meaning |
|---|---|---|
| `TASKMGR__SERVER` | required | Server URL |
| `TASKMGR__AGENTKEY` | required | One of the server's agent keys |
| `TASKMGR__HOSTID` | machine name | Host identifier |
| `TASKMGR__INTERVALSECONDS` | 1 | Sampling interval |
| `TASKMGR__ALLOWEDACTIONS` | none | Actions remote admins may request: `Kill`, `KillTree`, `SetPriority`, `Suspend`, `Resume` |
| `TASKMGR__ALLOWEDPROCESSUSERS` | any | Only processes owned by these users may be targeted |
| `TASKMGR__BUFFERSIZE` | 600 | Snapshots kept while the server is unreachable |

### Process actions are opt-in

Three checks must all pass before a remote action runs:

1. The agent must allow the action (`TASKMGR__ALLOWEDACTIONS`).
2. The requester must be an admin.
3. The target must not be a protected system process (`System`, `lsass`, `launchd`, `init`, `systemd`, and others, or PID ≤ 4).

The agent also refuses an action if the PID now belongs to a different process than the one the user saw. Every request and outcome is written to the audit log, which appears under the host's Audit tab.

### How history is stored

The server stores data at three resolutions:

- Every sample, for an hour.
- 1-minute averages and maximums: 30 days for system and network series, 24 hours for processes.
- 1-hour aggregates: a year for system series, 30 days for processes.

Not every process is stored every second. The busiest 25 processes by each of CPU, memory, disk I/O, and handles are recorded every sample; every other process is recorded once a minute. Exited processes stay queryable until their retention expires (enable "Show exited" on the Processes tab).

## API

Every endpoint except `/health` and `/api/v1/auth/login` needs `Authorization: Bearer <token>`, which you get from `POST /api/v1/auth/login` with `{ "username", "password" }`.

| Endpoint | Returns |
|---|---|
| `GET /api/v1/hosts`, `GET /api/v1/hosts/{id}` | Hosts, with online status, allowed actions, and latest CPU and memory |
| `GET /api/v1/hosts/{id}/snapshot[?processes=false]` | Latest snapshot |
| `GET /api/v1/hosts/{id}/processes?sort=process.cpu&desc=true&filter=&top=50` | Processes sorted and filtered on the server |
| `GET /api/v1/hosts/{id}/history?metric=system.cpu&from=&to=&step=` | System series, including `system.cpu.core.N` |
| `GET /api/v1/hosts/{id}/processes/{key}/history?metric=process.cpu` | Process series (the key is `pid@startTicks` from the snapshot) |
| `GET /api/v1/hosts/{id}/interfaces/{name}/history?metric=network.rx` | Interface series |
| `GET /api/v1/hosts/{id}/process-records?from=&to=` | Processes seen in a window, including exited ones |
| `GET /api/v1/hosts/{id}/accounting?from=&to=&groupBy=name\|user` | CPU time, peak memory, and disk bytes by process name or user |
| `GET /api/v1/catalog` | Every metric: unit, definition, and per-platform availability |
| `POST /api/v1/hosts/{id}/actions` | Queue an action (admin only); poll `GET .../actions/{requestId}` for the result |
| `GET /api/v1/hosts/{id}/audit` | Audit log |
| `WS /api/v1/stream?host={id}&access_token={token}` | Live snapshots (permessage-deflate) |

## Tests

```bash
dotnet run --project src/Test.Automated -f net10.0     # library + monitoring (Touchstone)
dotnet run --project src/Test.Tui -f net10.0           # terminal UI, headless (TUI_DUMP=1 prints every tab)
dotnet run --project src/Test.Server                   # server + agent end to end
cd web && npm test                                     # web unit tests
cd web && TASKMGR_WEB_URL=http://localhost:3000 TASKMGR_ADMIN_PASSWORD=... TASKMGR_E2E_HOST=<host> npx playwright test
```

CI runs all of these. The .NET suites run on Windows, Linux, and macOS. The compose stack and browser tests run on Linux.
