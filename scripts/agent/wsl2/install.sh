#!/usr/bin/env bash
# Install the TaskManager agent in this WSL 2 distro.
# Fails if it is already installed; use reinstall.sh to replace it.
# With systemd running in this distro ([boot] systemd=true in /etc/wsl.conf) the agent is the
# taskmgr-agent system service, as on Linux; WSL still stops a distro that has no open sessions, so
# it runs while the distro is up. Without systemd, a hidden launcher in the Windows Startup folder
# starts it as root (wsl.exe -u root) at Windows login and keeps the distro running (no admin
# rights on Windows); /opt/taskmgr-agent/run-wsl.sh restarts it when it exits and logs to
# /var/log/taskmgr-agent.log. Settings (including the agent key) are in /etc/taskmgr-agent.env
# (root:root, mode 600). The self-contained agent needs no DOTNET_ROOT.
# The agent reports the WSL 2 VM (its kernel, its share of CPU and memory, and this distro's
# processes), not the Windows host. To monitor Windows, use scripts\agent\windows from an
# elevated prompt.
# Run it as your normal user (it calls sudo for the steps that need root) or with sudo.
# Usage: ./scripts/agent/wsl2/install.sh --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
#   --server           TaskManager server URL, for example http://monitor:8080
#   --key              agent key (TASKMGR_AGENT_KEY on the server); asked for without echo when omitted
#   --host-id          host id to report (default: "<hostname>-wsl"; WSL shares the Windows computer name)
#   --allowed-actions  comma-separated: Kill, KillTree, SetPriority, Suspend, Resume (default: none)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }
# Prints the header comment above as usage text.
usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
case "$REPO_ROOT" in
    /mnt/[a-zA-Z]/*) echo "Warning: obj/ and bin/ in this checkout are shared with Windows builds of it; build from one side at a time, or clone separately inside WSL." >&2 ;;
esac

SERVER=""; KEY=""; HOST_ID=""; ACTIONS=""
# Prints the value of option $1 (the next argument), or fails when it is missing.
need_value() { [ "$#" -ge 2 ] && [ -n "$2" ] && [ "${2#--}" = "$2" ] || die "$1 requires a value."; }
while [ "$#" -gt 0 ]; do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        --server) need_value "$@"; SERVER="$2"; shift ;;
        --key) need_value "$@"; KEY="$2"; shift ;;
        --host-id) need_value "$@"; HOST_ID="$2"; shift ;;
        --allowed-actions) need_value "$@"; ACTIONS="$2"; shift ;;
        *) die "Unexpected argument: $1 (run scripts/agent/wsl2/install.sh --help for usage)" ;;
    esac
    shift
done
[ -n "$SERVER" ] || die "--server is required (run scripts/agent/wsl2/install.sh --help for usage)."
# Case-insensitive, as in the Windows script.
case "$(printf '%s' "$SERVER" | tr '[:upper:]' '[:lower:]')" in
    http://*|https://*) ;;
    *) die "--server must be an http:// or https:// URL (got '$SERVER')." ;;
esac
# Validate the action list here: the agent refuses to start with an unknown action.
ACTIONS="$(printf '%s' "$ACTIONS" | tr -d ' ')"
IFS=, read -r -a ACTION_LIST <<< "$ACTIONS,"
for action in "${ACTION_LIST[@]}"; do
    case "$(printf '%s' "$action" | tr '[:upper:]' '[:lower:]')" in
        ''|kill|killtree|setpriority|suspend|resume) ;;
        *) die "Unknown action '$action' in --allowed-actions. Valid actions: Kill, KillTree, SetPriority, Suspend, Resume." ;;
    esac
done
for value in "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS"; do
    case "$value" in
        *"'"*|*'"'*|*$'\n'*) die "Values must not contain quotes or newlines." ;;
    esac
done
[ -n "$HOST_ID" ] || HOST_ID="$(hostname | tr '[:upper:]' '[:lower:]')-wsl"

# Steps that need root go through sudo when this script runs as a normal user.
as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
# For the script tests only: puts every system path under this directory.
P="${TASKMGR_AGENT_TEST_ROOT:-}"

# sudo drops WSL_DISTRO_NAME and WSL_INTEROP, which the Windows Startup launcher and Windows interop
# need. Under sudo, take them from the shell that ran sudo.
recover_wsl_env() {
    local pid="$$" parent var value
    while [ -z "${WSL_DISTRO_NAME:-}" ] && [ "$pid" -gt 1 ]; do
        parent="$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ' || true)"
        [ -n "$parent" ] || break
        pid="$parent"
        [ -r "/proc/$pid/environ" ] || continue
        for var in WSL_DISTRO_NAME WSL_INTEROP; do
            # awk reads all of its input, so tr never gets SIGPIPE under pipefail.
            value="$(tr '\0' '\n' < "/proc/$pid/environ" | awk -v k="$var" 'index($0, k "=") == 1 { v = substr($0, length(k) + 2) } END { print v }')"
            if [ -n "$value" ] && [ -z "${!var:-}" ]; then export "$var=$value"; fi
        done
    done
}
if [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ]; then recover_wsl_env; fi
DEST="$P/opt/taskmgr-agent"
ENV_FILE="$P/etc/taskmgr-agent.env"
UNIT="$P/etc/systemd/system/taskmgr-agent.service"
# shellcheck disable=SC2034 # used by the removal steps
WANTS="$P/etc/systemd/system/multi-user.target.wants/taskmgr-agent.service"
LOG="$P/var/log/taskmgr-agent.log"
# True when any part of an agent install is present, in either mode.
agent_installed() { [ -e "$UNIT" ] || [ -e "$DEST" ] || [ -e "$ENV_FILE" ] || wsl_startup_exists taskmgr-agent; }
# PID 1 being systemd is enough for a system service (wsl_has_systemd also needs a user manager,
# which is missing under sudo).
pid1_is_systemd() { [ "$(ps -p 1 -o comm= 2>/dev/null || true)" = systemd ] && command -v systemctl >/dev/null 2>&1; }

echo "TaskManager agent install (WSL 2)"
echo "[1/5] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
# sudo resets PATH: also look in the invoking user's ~/.dotnet and the usual install directories.
DOTNET="$(command -v dotnet 2>/dev/null || true)"
if [ -z "$DOTNET" ] && [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ]; then
    user_home="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
    for candidate in "$user_home/.dotnet/dotnet" /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet /usr/local/share/dotnet/dotnet; do
        if [ -x "$candidate" ]; then DOTNET="$candidate"; break; fi
    done
    if [ "$DOTNET" = "$user_home/.dotnet/dotnet" ] && [ -z "${DOTNET_ROOT:-}" ]; then export DOTNET_ROOT="$user_home/.dotnet"; fi
fi
if [ -n "$DOTNET" ]; then PATH="$(dirname "$DOTNET"):$PATH"; fi
wsl_require_cmd dotnet "Install the .NET 8 or 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
DOTNET="$(command -v dotnet)"
# Pick the framework from the installed SDKs; capture first, because under 'set -o pipefail'
# 'dotnet --list-sdks | grep -q' can fail with SIGPIPE.
SDKS="$("$DOTNET" --list-sdks 2>/dev/null || true)"
case "
$SDKS" in
    *"
1"[0-9]"."*) FRAMEWORK=net10.0 ;;
    *"
8."*|*"
9."*) FRAMEWORK=net8.0 ;;
    *) die "No .NET SDK was found ($DOTNET --list-sdks is empty). Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux" ;;
esac
case "$(uname -m)" in
    x86_64|amd64) RID=linux-x64 ;;
    aarch64|arm64) RID=linux-arm64 ;;
    armv7l|armv7*) RID=linux-arm ;;
    *) die "Unsupported processor architecture: $(uname -m). The agent is published for x86_64, aarch64 and armv7l." ;;
esac
if pid1_is_systemd; then MODE=systemd; else MODE=startup; fi
if [ "$MODE" = startup ] && [ -z "${WSL_DISTRO_NAME:-}" ]; then
    die "WSL_DISTRO_NAME is not set, so the Windows Startup launcher cannot name this distro. Run this script from a WSL shell without sudo; it calls sudo itself."
fi
# Check Windows interop and the Startup folder now, so a failure cannot leave a half install.
if [ "$MODE" = startup ]; then wsl_startup_dir >/dev/null; fi
# Under WSL 2 NAT networking, localhost here is not Windows. The URL is used as given; only warn.
case "$SERVER" in
    *://localhost:*|*://localhost/*|*://127.0.0.1:*|*://127.0.0.1/*)
        if command -v curl >/dev/null 2>&1 && ! curl -s -o /dev/null --max-time 2 "$SERVER" 2>/dev/null; then
            echo "Warning: nothing answers at $SERVER inside WSL. If the server runs on Windows, use the Windows host address (http://$(wsl_windows_host)...) or networkingMode=mirrored in .wslconfig." >&2
        fi ;;
esac
echo "[2/5] Checking for an existing install..."
if agent_installed; then
    die "The TaskManager agent is already installed. Run scripts/agent/wsl2/reinstall.sh to replace it."
fi
# Asked for only now, after the prerequisite and existing-install checks, as on every OS.
if [ -z "$KEY" ]; then
    [ -t 0 ] || die "--key is required (it is asked for only on a terminal)."
    read -r -s -p "Agent key: " KEY
    echo
    [ -n "$KEY" ] || die "An agent key is required."
    case "$KEY" in *"'"*|*'"'*) die "Values must not contain quotes or newlines." ;; esac
fi
echo "[3/5] Building the agent ($FRAMEWORK, $RID)..."
# Build as the invoking user, never as root, so the repository gets no root-owned build output.
as_builder() {
    if [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ] && [ "$SUDO_USER" != root ]; then
        sudo -u "$SUDO_USER" -H env "PATH=$PATH" ${DOTNET_ROOT:+"DOTNET_ROOT=$DOTNET_ROOT"} "$@"
    else
        "$@"
    fi
}
STAGE="$(as_builder mktemp -d "${TMPDIR:-/tmp}/taskmgr-agent.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
as_builder "$DOTNET" publish "$REPO_ROOT/src/TaskManager.Agent/TaskManager.Agent.csproj" -c Release -f "$FRAMEWORK" \
    -r "$RID" --self-contained true -p:PublishSingleFile=true -o "$STAGE" -nologo -v quiet
[ -f "$STAGE/taskmgr-agent" ] || die "The build did not produce $STAGE/taskmgr-agent."
echo "[4/5] Installing the agent and its settings in $DEST..."
as_root sh -c 'rm -rf "$1" && mkdir -p "$1" && cp -R "$2/." "$1/" && chmod 755 "$1/taskmgr-agent"' sh "$DEST" "$STAGE"
# Single-quoted values work both for systemd's EnvironmentFile and for sh.
printf "TASKMGR__SERVER='%s'\nTASKMGR__AGENTKEY='%s'\nTASKMGR__HOSTID='%s'\nTASKMGR__ALLOWEDACTIONS='%s'\n" \
    "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS" | as_root sh -c 'umask 077 && mkdir -p "$(dirname "$1")" && cat > "$1"' sh "$ENV_FILE"
# Readable by root only: it holds the agent key.
as_root chown root:root "$ENV_FILE"
as_root chmod 600 "$ENV_FILE"
echo "[5/5] Registering and starting the agent ($MODE)..."
if [ "$MODE" = systemd ]; then
    as_root mkdir -p "$(dirname "$UNIT")"
    as_root tee "$UNIT" >/dev/null <<UNITEOF
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
UNITEOF
    as_root systemctl daemon-reload
    as_root systemctl enable --now taskmgr-agent
    as_root systemctl --no-pager status taskmgr-agent 2>/dev/null | head -n 5 || true
    echo "TaskManager agent installed and running as the taskmgr-agent systemd service (host id: $HOST_ID)."
    echo "It runs while this distro is running; WSL stops a distro that has no open sessions. Logs: journalctl -u taskmgr-agent"
else
    as_root tee "$DEST/run-wsl.sh" >/dev/null <<RUNEOF
#!/bin/sh
# Started as root by the TaskManager agent's Windows Startup launcher. Restarts the agent when it exits.
set -a; . '$ENV_FILE'; set +a
while :; do
    '$DEST/taskmgr-agent' >>'$LOG' 2>&1
    sleep 5
done
RUNEOF
    as_root chmod 755 "$DEST/run-wsl.sh"
    wsl_startup_install taskmgr-agent /bin/sh "$DEST/run-wsl.sh"
    # The agent must run as root to read every process: make the launcher start it as root.
    LAUNCHER="$(wsl_startup_dir)/$(wsl_launcher_file taskmgr-agent)"
    sed 's/wsl\.exe -d ""\([^"]*\)"" --cd/wsl.exe -d ""\1"" -u root --cd/' "$LAUNCHER" > "$LAUNCHER.tmp"
    mv -f "$LAUNCHER.tmp" "$LAUNCHER"
    grep -q ' -u root --cd ' "$LAUNCHER" || die "Could not make $LAUNCHER start the agent as root."
    # Start it through the launcher now, so it keeps running after this terminal closes.
    wsl_startup_run taskmgr-agent
    # Report "running" only once the restart loop is actually up (up to 5 seconds).
    running=0
    for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25; do
        if pgrep -f "$DEST/[r]un-wsl.sh" >/dev/null 2>&1; then running=1; break; fi
        sleep 0.2
    done
    if [ "$running" = 1 ]; then
        echo "TaskManager agent installed (host id: $HOST_ID); it starts at Windows login and is running now."
    else
        echo "TaskManager agent installed (host id: $HOST_ID); it starts at Windows login." >&2
        echo "Warning: the launcher ran, but the agent is not running yet. Check $LOG, or Windows interop (wscript.exe)." >&2
    fi
    echo "Logs: $LOG"
    echo "Enable systemd ([boot] systemd=true in /etc/wsl.conf) to run it as a systemd service instead."
fi
echo "This agent reports the WSL 2 VM, not Windows. For the Windows host, use scripts\\agent\\windows\\install.bat."
