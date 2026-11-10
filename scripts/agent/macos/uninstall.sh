#!/usr/bin/env bash
# Remove the TaskManager agent (the com.taskmgr.agent launchd daemon).
# Succeeds with a message when it is not installed.
# The agent runs as root so it can read every process. It is published self-contained to
# /usr/local/taskmgr-agent; its settings (including the agent key) are in appsettings.json there
# (root:wheel, mode 600), not in the plist, whose environment any user can read with launchctl
# print. /Library/LaunchDaemons/com.taskmgr.agent.plist (KeepAlive) starts it there; it logs to
# /var/log/taskmgr-agent.log.
# Removes every file the install wrote, including the log.
# Run it as your normal user (it calls sudo for the steps that need root) or as root.
# Usage: ./scripts/agent/macos/uninstall.sh
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

case "${1:-}" in -h|--help) usage; exit 0 ;; esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

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

echo "TaskManager agent uninstall (macOS)"
echo "[1/2] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
echo "[2/2] Stopping and removing the agent..."
if agent_installed; then
    # bootout stops the daemon through launchd first, so KeepAlive does not restart it. It can
    # return before launchd has finished, so wait (up to 10 seconds) until the label is gone.
    as_root launchctl bootout system/com.taskmgr.agent 2>/dev/null || true
    tries=0
    while launchctl print system/com.taskmgr.agent >/dev/null 2>&1; do
        tries=$((tries + 1))
        [ "$tries" -le 50 ] || die "launchd did not unload com.taskmgr.agent within 10 seconds."
        sleep 0.2
    done
    as_root rm -f "$PLIST"
    as_root rm -rf "$DEST"
    as_root rm -f "$LOG"
    echo "TaskManager agent removed."
else
    echo "The TaskManager agent is not installed; nothing to remove."
fi
