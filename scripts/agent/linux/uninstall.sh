#!/usr/bin/env bash
# Remove the TaskManager agent (the taskmgr-agent systemd service).
# Succeeds with a message when it is not installed.
# The agent runs as root so it can read every process. It is published self-contained to
# /opt/taskmgr-agent; its settings (including the agent key) are in /etc/taskmgr-agent.env
# (root:root, mode 600) and the unit is /etc/systemd/system/taskmgr-agent.service (Restart=always).
# Removes every file the install wrote.
# Run it as your normal user (it calls sudo for the steps that need root) or as root.
# Usage: ./scripts/agent/linux/uninstall.sh
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

case "${1:-}" in -h|--help) usage; exit 0 ;; esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

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

echo "TaskManager agent uninstall (Linux)"
echo "[1/2] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
echo "[2/2] Stopping and removing the agent..."
if agent_installed; then
    if command -v systemctl >/dev/null 2>&1; then as_root systemctl disable --now taskmgr-agent 2>/dev/null || true; fi
    as_root rm -f "$UNIT" "$WANTS" "$ENV_FILE"
    if command -v systemctl >/dev/null 2>&1; then as_root systemctl daemon-reload; fi
    as_root rm -rf "$DEST"
    echo "TaskManager agent removed."
else
    echo "The TaskManager agent is not installed; nothing to remove."
fi
