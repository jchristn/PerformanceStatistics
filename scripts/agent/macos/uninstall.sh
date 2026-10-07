#!/usr/bin/env bash
# Remove the TaskManager agent launchd daemon.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }
PLIST=/Library/LaunchDaemons/com.taskmgr.agent.plist
launchctl bootout system "$PLIST" 2>/dev/null || true
rm -f "$PLIST"
rm -rf /usr/local/taskmgr-agent
echo "TaskManager agent removed."
