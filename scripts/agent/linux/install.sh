#!/usr/bin/env bash
# Install the TaskManager agent (the taskmgr-agent systemd service).
# Fails if it is already installed; use reinstall.sh to replace it.
# The agent runs as root so it can read every process. It is published self-contained to
# /opt/taskmgr-agent; its settings (including the agent key) are in /etc/taskmgr-agent.env
# (root:root, mode 600) and the unit is /etc/systemd/system/taskmgr-agent.service (Restart=always).
# Run it as your normal user (it calls sudo for the steps that need root) or as root.
# Usage: ./scripts/agent/linux/install.sh --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
#   --server           TaskManager server URL, for example http://monitor:8080
#   --key              agent key (TASKMGR_AGENT_KEY on the server); asked for without echo when omitted
#   --host-id          host id to report (default: the machine name)
#   --allowed-actions  comma-separated: Kill, KillTree, SetPriority, Suspend, Resume (default: none)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }
# Prints the header comment above as usage text.
usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/agent/macos on macOS or scripts\\agent\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL: Docker Desktop containers have one too, but no WSL interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/agent/wsl2 (WSL 2) or scripts/agent/wsl (WSL 1); systemd services do not start at Windows login."
fi

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
        *) die "Unexpected argument: $1 (run scripts/agent/linux/install.sh --help for usage)" ;;
    esac
    shift
done
[ -n "$SERVER" ] || die "--server is required (run scripts/agent/linux/install.sh --help for usage)."
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

# Steps that need root go through sudo when this script runs as a normal user.
as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
# For the script tests only: puts every system path under this directory.
P="${TASKMGR_AGENT_TEST_ROOT:-}"
DEST="$P/opt/taskmgr-agent"
ENV_FILE="$P/etc/taskmgr-agent.env"
UNIT="$P/etc/systemd/system/taskmgr-agent.service"
# shellcheck disable=SC2034 # used by the removal steps
WANTS="$P/etc/systemd/system/multi-user.target.wants/taskmgr-agent.service"
# True when any part of an agent install is present.
agent_installed() { [ -e "$UNIT" ] || [ -e "$DEST" ] || [ -e "$ENV_FILE" ]; }

echo "TaskManager agent install (Linux)"
echo "[1/5] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
if [ "$(ps -p 1 -o comm= 2>/dev/null || true)" != systemd ] || ! command -v systemctl >/dev/null 2>&1; then
    die "This script installs a systemd service, but systemd is not running here (PID 1 is $(ps -p 1 -o comm= 2>/dev/null || echo unknown))."
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
[ -n "$DOTNET" ] || die "dotnet was not found on PATH. Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
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
echo "[2/5] Checking for an existing install..."
if agent_installed; then
    die "The TaskManager agent is already installed. Run scripts/agent/linux/reinstall.sh to replace it."
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
echo "[5/5] Registering and starting the systemd service..."
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
echo "TaskManager agent installed and running as the taskmgr-agent systemd service (host id: ${HOST_ID:-the machine name})."
echo "Logs: journalctl -u taskmgr-agent"
