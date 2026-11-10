#!/usr/bin/env bash
# Install the TaskManager agent (the com.taskmgr.agent launchd daemon).
# Fails if it is already installed; use reinstall.sh to replace it.
# The agent runs as root so it can read every process. It is published self-contained to
# /usr/local/taskmgr-agent; its settings (including the agent key) are in appsettings.json there
# (root:wheel, mode 600), not in the plist, whose environment any user can read with launchctl
# print. /Library/LaunchDaemons/com.taskmgr.agent.plist (KeepAlive) starts it there; it logs to
# /var/log/taskmgr-agent.log.
# Run it as your normal user (it calls sudo for the steps that need root) or as root.
# Usage: ./scripts/agent/macos/install.sh --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
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

if [ "$(uname -s)" != Darwin ]; then
    die "This script is for macOS. Use scripts/agent/linux on Linux, scripts/agent/wsl2 or scripts/agent/wsl under WSL, or scripts\\agent\\windows on Windows."
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
        *) die "Unexpected argument: $1 (run scripts/agent/macos/install.sh --help for usage)" ;;
    esac
    shift
done
[ -n "$SERVER" ] || die "--server is required (run scripts/agent/macos/install.sh --help for usage)."
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
DEST="$P/usr/local/taskmgr-agent"
PLIST="$P/Library/LaunchDaemons/com.taskmgr.agent.plist"
# shellcheck disable=SC2034 # written by install and reinstall; removed with $DEST
SETTINGS="$DEST/appsettings.json"
LOG="$P/var/log/taskmgr-agent.log"
# True when any part of an agent install is present (including a daemon loaded from elsewhere).
agent_installed() { [ -e "$PLIST" ] || [ -e "$DEST" ] || launchctl print system/com.taskmgr.agent >/dev/null 2>&1; }

echo "TaskManager agent install (macOS)"
echo "[1/5] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
# sudo resets PATH: also look in the invoking user's ~/.dotnet and the usual install directories.
DOTNET="$(command -v dotnet 2>/dev/null || true)"
if [ -z "$DOTNET" ] && [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ]; then
    user_home="$(dscl . -read "/Users/$SUDO_USER" NFSHomeDirectory 2>/dev/null | awk '{ print $2 }')"
    for candidate in "$user_home/.dotnet/dotnet" /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet /usr/local/bin/dotnet; do
        if [ -x "$candidate" ]; then DOTNET="$candidate"; break; fi
    done
    if [ "$DOTNET" = "$user_home/.dotnet/dotnet" ] && [ -z "${DOTNET_ROOT:-}" ]; then export DOTNET_ROOT="$user_home/.dotnet"; fi
fi
[ -n "$DOTNET" ] || die "dotnet was not found on PATH. Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/macos"
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
    *) die "No .NET SDK was found ($DOTNET --list-sdks is empty). Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/macos" ;;
esac
case "$(uname -m)" in
    arm64) RID=osx-arm64 ;;
    # A shell under Rosetta reports x86_64 on Apple silicon; publish the native agent there.
    x86_64) if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" = 1 ]; then RID=osx-arm64; else RID=osx-x64; fi ;;
    *) die "Unsupported processor architecture: $(uname -m)." ;;
esac
echo "[2/5] Checking for an existing install..."
if agent_installed; then
    die "The TaskManager agent is already installed. Run scripts/agent/macos/reinstall.sh to replace it."
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
# The settings (including the agent key) go in a root-only appsettings.json in the install
# directory, not in the plist: anyone can read a daemon's EnvironmentVariables with
# 'launchctl print'. The agent loads appsettings.json from its content root, which is the working
# directory set below.
json() { printf '%s' "$1" | sed -e 's/\\/\\\\/g'; }
as_root sh -c 'umask 077 && cat > "$1"' sh "$SETTINGS" <<JSONEOF
{
  "TASKMGR": {
    "Server": "$(json "$SERVER")",
    "AgentKey": "$(json "$KEY")",
    "HostId": "$(json "$HOST_ID")",
    "AllowedActions": "$(json "$ACTIONS")"
  }
}
JSONEOF
as_root chown root:wheel "$SETTINGS"
as_root chmod 600 "$SETTINGS"
# The plist values are paths only; escape them for XML anyway.
xml() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }
as_root mkdir -p "$(dirname "$PLIST")"
as_root tee "$PLIST" >/dev/null <<PLISTEOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.taskmgr.agent</string>
  <key>ProgramArguments</key><array><string>$(xml "$DEST/taskmgr-agent")</string></array>
  <key>WorkingDirectory</key><string>$(xml "$DEST")</string>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardErrorPath</key><string>$(xml "$LOG")</string>
  <key>StandardOutPath</key><string>$(xml "$LOG")</string>
</dict>
</plist>
PLISTEOF
echo "[5/5] Registering and starting the launchd daemon..."
# launchd ignores a daemon plist that is not owned by root or is writable by others.
as_root chown root:wheel "$PLIST"
as_root chmod 644 "$PLIST"
as_root launchctl bootstrap system "$PLIST"
echo "TaskManager agent installed and running as the com.taskmgr.agent launchd daemon (host id: ${HOST_ID:-the machine name})."
echo "Logs: $LOG"
