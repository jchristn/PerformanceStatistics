#!/usr/bin/env bash
# Install the TaskManager agent as a systemd service.
# Usage: sudo ./install.sh --server http://monitor:8080 --key AGENT_KEY [--host-id ID] [--allowed-actions Kill,Suspend,Resume,SetPriority]
set -euo pipefail

SERVER=""; KEY=""; HOST_ID=""; ACTIONS=""
while [ $# -gt 0 ]; do
  case "$1" in
    --server) SERVER="$2"; shift 2 ;;
    --key) KEY="$2"; shift 2 ;;
    --host-id) HOST_ID="$2"; shift 2 ;;
    --allowed-actions) ACTIONS="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[ -n "$SERVER" ] && [ -n "$KEY" ] || { echo "Usage: sudo $0 --server URL --key AGENT_KEY [--host-id ID] [--allowed-actions LIST]" >&2; exit 2; }
[ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo) so the agent can read every process." >&2; exit 1; }

REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
case "$(uname -m)" in aarch64|arm64) RID=linux-arm64 ;; *) RID=linux-x64 ;; esac
DEST=/opt/taskmgr-agent

dotnet publish "$REPO/src/TaskManager.Agent" -c Release -f net10.0 -r "$RID" --self-contained true -p:PublishSingleFile=true -o "$DEST"

umask 077
cat > /etc/taskmgr-agent.env <<ENV
TASKMGR__SERVER=$SERVER
TASKMGR__AGENTKEY=$KEY
TASKMGR__HOSTID=$HOST_ID
TASKMGR__ALLOWEDACTIONS=$ACTIONS
ENV

cat > /etc/systemd/system/taskmgr-agent.service <<UNIT
[Unit]
Description=TaskManager agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=$DEST/taskmgr-agent
EnvironmentFile=/etc/taskmgr-agent.env
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable --now taskmgr-agent
systemctl --no-pager status taskmgr-agent | head -5
