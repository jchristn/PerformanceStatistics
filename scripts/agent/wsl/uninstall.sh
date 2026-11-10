#!/usr/bin/env bash
# Remove the TaskManager agent in this WSL 1 distro.
# Succeeds with a message when it is not installed.
# WSL 1 has no systemd, so a hidden launcher in the Windows Startup folder starts the agent as root
# (wsl.exe -u root) inside this distro at Windows login and keeps the distro running; no admin
# rights on Windows. /opt/taskmgr-agent/run-wsl.sh restarts the agent when it exits and logs to
# /var/log/taskmgr-agent.log; settings (including the agent key) are in /etc/taskmgr-agent.env
# (root:root, mode 600). The self-contained agent needs no DOTNET_ROOT.
# WSL 1 has no Linux kernel: the agent sees this distro's processes and WSL's emulated /proc, so
# some metrics are missing or approximate, and it does not describe the Windows host. To monitor
# Windows, use scripts\agent\windows from an elevated prompt.
# Removes every file the install wrote, including the log, in either mode, and a unit left by scripts/agent/linux.
# Run it as your normal user (it calls sudo for the steps that need root) or with sudo.
# Usage: ./scripts/agent/wsl/uninstall.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }
# Prints the header comment above as usage text.
usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
case "$REPO_ROOT" in
    /mnt/[a-zA-Z]/*) echo "Warning: obj/ and bin/ in this checkout are shared with Windows builds of it; build from one side at a time, or clone separately inside WSL." >&2 ;;
esac

case "${1:-}" in -h|--help) usage; exit 0 ;; esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

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

echo "TaskManager agent uninstall (WSL 1)"
echo "[1/2] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
# Check Windows interop and the Startup folder now, so a failure cannot leave a half install.
wsl_startup_dir >/dev/null
echo "[2/2] Stopping and removing the agent..."
if agent_installed; then
    # Stop the restart loop before the agent, or it starts the agent again. The [r] keeps the
    # pattern from matching the pkill command line itself.
    as_root pkill -f "$DEST/[r]un-wsl.sh" 2>/dev/null || true
    as_root pkill -x taskmgr-agent 2>/dev/null || true
    if wsl_startup_exists taskmgr-agent; then wsl_startup_remove taskmgr-agent; fi
    # Remove the enablement link directly: without systemd as PID 1 nothing else removes it.
    as_root rm -f "$UNIT" "$WANTS" "$ENV_FILE"
    as_root rm -rf "$DEST"
    as_root rm -f "$LOG"
    echo "TaskManager agent removed."
else
    echo "The TaskManager agent is not installed; nothing to remove."
fi
