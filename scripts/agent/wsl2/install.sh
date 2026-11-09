#!/usr/bin/env bash
# Install the TaskManager agent from WSL 2. WSL 2 counterpart of scripts/agent/linux/install.sh.
#   - With systemd enabled in this distro ([boot] systemd=true in /etc/wsl.conf), the agent is a
#     systemd service, as on Linux. WSL stops an idle distro, and systemd services alone do not
#     keep it running, so the agent runs while the distro is up.
#   - Without systemd, a hidden launcher in the Windows Startup folder starts the agent (as root)
#     inside this distro at Windows login and keeps the distro running. No admin rights on Windows.
# The agent reports the WSL 2 VM (its kernel, its share of CPU and memory, and this distro's
# processes), not the Windows host. To monitor Windows itself, run
# scripts\agent\windows\install.bat from an elevated Windows prompt.
# The host id defaults to "<hostname>-wsl": WSL uses the Windows computer name as its hostname,
# which would clash with a Windows agent on the same machine.
#
# Usage: ./install.sh --server http://monitor:8080 --key AGENT_KEY [--host-id ID] [--allowed-actions Kill,Suspend,Resume,SetPriority]
# Run it as your normal user; it calls sudo for the steps that need root.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../../.." && pwd)"
WSL_FLAVOR=wsl2   # "wsl" in scripts/agent/wsl
# shellcheck source-path=SCRIPTDIR source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
set -o pipefail   # only after wsl_check_repo, whose find | grep pipeline returns 1 when nothing matches

usage() { echo "Usage: $0 --server URL --key AGENT_KEY [--host-id ID] [--allowed-actions LIST]" >&2; exit 2; }
SERVER=""; KEY=""; HOST_ID=""; ACTIONS=""
while [ $# -gt 0 ]; do
  case "$1" in
    --server) SERVER="${2:-}"; shift 2 || usage ;;
    --key) KEY="${2:-}"; shift 2 || usage ;;
    --host-id) HOST_ID="${2:-}"; shift 2 || usage ;;
    --allowed-actions) ACTIONS="${2:-}"; shift 2 || usage ;;
    -h|--help) usage ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[ -n "$SERVER" ] && [ -n "$KEY" ] || usage
for v in "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS"; do
  case "$v" in *"'"*|*$'\n'*) wsl_die "Values must not contain single quotes or newlines." ;; esac
done
[ -n "$HOST_ID" ] || HOST_ID="$(hostname | tr '[:upper:]' '[:lower:]')-wsl"

as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
[ "$(id -u)" -eq 0 ] || command -v sudo >/dev/null 2>&1 || wsl_die "sudo is required (or run as root)."

# Under sudo, PATH and HOME are root's; look for the invoking user's ~/.dotnet as well.
if ! command -v dotnet >/dev/null 2>&1 && [ -n "${SUDO_USER:-}" ]; then
  user_home="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
  [ -x "$user_home/.dotnet/dotnet" ] && PATH="$user_home/.dotnet:$PATH" && export DOTNET_ROOT="$user_home/.dotnet"
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"

# A server on Windows (for example in Docker Desktop) may not answer at localhost under WSL 2 NAT.
resolved="$(wsl_resolve_local_url "$SERVER")"
if [ "$resolved" != "$SERVER" ]; then
  wsl_log "Note: the Windows host address can change when Windows restarts; rerun reinstall.sh if the agent stops reporting."
  SERVER="$resolved"
fi

pid1_is_systemd() { [ "$(ps -p 1 -o comm= 2>/dev/null)" = systemd ] && command -v systemctl >/dev/null 2>&1; }
# The agent is a system service, so PID 1 being systemd is enough; wsl_has_systemd also needs a user
# manager, which is missing under sudo.
if wsl_has_systemd || pid1_is_systemd; then MODE=systemd; else MODE=startup; fi
if [ "$MODE" = startup ] && [ -z "${WSL_DISTRO_NAME:-}" ]; then
  wsl_die "WSL_DISTRO_NAME is not set (sudo removes it). Run this script without sudo; it calls sudo itself."
fi

case "$(uname -m)" in aarch64|arm64) RID=linux-arm64 ;; *) RID=linux-x64 ;; esac
DEST=/opt/taskmgr-agent
UNIT=/etc/systemd/system/taskmgr-agent.service
ENV_FILE=/etc/taskmgr-agent.env
LOG=/var/log/taskmgr-agent.log

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
dotnet publish "$REPO_ROOT/src/TaskManager.Agent" -c Release -f net10.0 -r "$RID" --self-contained true -p:PublishSingleFile=true -o "$STAGE"

# Stop a running agent (either mode) before replacing its binary.
if pid1_is_systemd; then as_root systemctl stop taskmgr-agent 2>/dev/null || true; fi
# The [r] keeps the pattern from matching the sudo process that runs pkill.
as_root pkill -f "$DEST/[r]un-wsl.sh" 2>/dev/null || true
as_root pkill -x taskmgr-agent 2>/dev/null || true

as_root sh -c 'rm -rf "$1" && mkdir -p "$1" && cp -R "$2/." "$1/"' sh "$DEST" "$STAGE"

# Single-quoted values work both for systemd's EnvironmentFile and for sh.
printf "TASKMGR__SERVER='%s'\nTASKMGR__AGENTKEY='%s'\nTASKMGR__HOSTID='%s'\nTASKMGR__ALLOWEDACTIONS='%s'\n" \
  "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS" | as_root sh -c 'umask 077 && cat > "$1"' sh "$ENV_FILE"

if [ "$MODE" = systemd ]; then
  as_root tee "$UNIT" >/dev/null <<UNIT
[Unit]
Description=TaskManager agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=$DEST/taskmgr-agent
EnvironmentFile=$ENV_FILE
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
UNIT
  as_root systemctl daemon-reload
  as_root systemctl enable --now taskmgr-agent
  as_root systemctl --no-pager status taskmgr-agent | head -5 || true
  # Drop a Startup launcher left from an earlier install without systemd.
  if (wsl_win_exe cmd.exe) >/dev/null 2>&1; then (wsl_startup_remove taskmgr-agent) || true; fi
  echo "TaskManager agent installed as a systemd service (host id: $HOST_ID)."
  echo "It runs while this WSL distro is running; WSL stops a distro that has no open sessions."
else
  as_root tee "$DEST/run-wsl.sh" >/dev/null <<RUN
#!/bin/sh
# Started by the TaskManager agent's Windows Startup launcher. Restarts the agent when it exits.
set -a; . $ENV_FILE; set +a
while :; do
  $DEST/taskmgr-agent >>$LOG 2>&1
  sleep 5
done
RUN
  as_root chmod 755 "$DEST/run-wsl.sh"
  # Remove a systemd unit left from an earlier install.
  as_root rm -f "$UNIT"
  wsl_startup_install taskmgr-agent /bin/sh "$DEST/run-wsl.sh"
  # The agent must run as root to read every process: start it in the distro as root.
  launcher="$(wsl_startup_dir)/taskmgr-agent-wsl.vbs"
  sed -i 's/wsl\.exe -d ""\([^"]*\)"" --cd/wsl.exe -d ""\1"" -u root --cd/' "$launcher"
  as_root sh -c 'nohup /bin/sh "$1" >/dev/null 2>&1 &' sh "$DEST/run-wsl.sh"
  echo "TaskManager agent installed (host id: $HOST_ID); it starts at Windows login and is running now."
  echo "Logs: $LOG. Enable systemd ([boot] systemd=true in /etc/wsl.conf) to run it as a service instead."
fi
echo "This agent reports the WSL 2 VM, not Windows. For the Windows host, use scripts\\agent\\windows\\install.bat."
