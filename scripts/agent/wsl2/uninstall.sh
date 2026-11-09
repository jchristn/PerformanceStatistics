#!/usr/bin/env bash
# Remove the TaskManager agent from WSL 2. WSL 2 counterpart of scripts/agent/linux/uninstall.sh.
# Removes both install modes of scripts/agent/wsl2/install.sh (systemd service and Windows Startup
# launcher), including a unit written by scripts/agent/linux/install.sh. The Windows agent
# (scripts\agent\windows) is not touched.
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

as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
[ "$(id -u)" -eq 0 ] || command -v sudo >/dev/null 2>&1 || wsl_die "sudo is required (or run as root)."
pid1_is_systemd() { [ "$(ps -p 1 -o comm= 2>/dev/null)" = systemd ] && command -v systemctl >/dev/null 2>&1; }

DEST=/opt/taskmgr-agent
UNIT=/etc/systemd/system/taskmgr-agent.service

if pid1_is_systemd; then as_root systemctl disable --now taskmgr-agent 2>/dev/null || true; fi
as_root rm -f "$UNIT"
if pid1_is_systemd; then as_root systemctl daemon-reload; fi

if (wsl_win_exe cmd.exe) >/dev/null 2>&1; then
  (wsl_startup_remove taskmgr-agent) || true
else
  wsl_log "Windows interop is off; remove taskmgr-agent-wsl.vbs from the Windows Startup folder by hand if it exists."
fi
# The [r] keeps the pattern from matching the sudo process that runs pkill.
as_root pkill -f "$DEST/[r]un-wsl.sh" 2>/dev/null || true
as_root pkill -x taskmgr-agent 2>/dev/null || true

as_root rm -f /etc/taskmgr-agent.env
as_root rm -rf "$DEST"
echo "TaskManager agent removed."
