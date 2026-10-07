#!/usr/bin/env bash
# Remove the TaskManager agent systemd service.
set -euo pipefail
[ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }
systemctl disable --now taskmgr-agent 2>/dev/null || true
rm -f /etc/systemd/system/taskmgr-agent.service /etc/taskmgr-agent.env
systemctl daemon-reload
rm -rf /opt/taskmgr-agent
echo "TaskManager agent removed."
