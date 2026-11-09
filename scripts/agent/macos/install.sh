#!/usr/bin/env bash
# Install the TaskManager agent as a launchd daemon (runs as root so it can read every process).
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
[ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }

REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
case "$(uname -m)" in arm64) RID=osx-arm64 ;; *) RID=osx-x64 ;; esac
DEST=/usr/local/taskmgr-agent
PLIST=/Library/LaunchDaemons/com.taskmgr.agent.plist

command -v dotnet >/dev/null 2>&1 || { echo "dotnet was not found on root's PATH. Try: sudo env \"PATH=\$PATH\" $0 ..." >&2; exit 1; }

# Stop a running agent before replacing its binary.
launchctl bootout system "$PLIST" 2>/dev/null || true

dotnet publish "$REPO/src/TaskManager.Agent" -c Release -f net10.0 -r "$RID" --self-contained true -p:PublishSingleFile=true -o "$DEST"

cat > "$PLIST" <<PLISTEOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.taskmgr.agent</string>
  <key>ProgramArguments</key><array><string>$DEST/taskmgr-agent</string></array>
  <key>EnvironmentVariables</key>
  <dict>
    <key>TASKMGR__SERVER</key><string>$SERVER</string>
    <key>TASKMGR__AGENTKEY</key><string>$KEY</string>
    <key>TASKMGR__HOSTID</key><string>$HOST_ID</string>
    <key>TASKMGR__ALLOWEDACTIONS</key><string>$ACTIONS</string>
  </dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardErrorPath</key><string>/var/log/taskmgr-agent.log</string>
  <key>StandardOutPath</key><string>/var/log/taskmgr-agent.log</string>
</dict>
</plist>
PLISTEOF
chmod 600 "$PLIST"

launchctl bootstrap system "$PLIST"
echo "TaskManager agent installed; logs in /var/log/taskmgr-agent.log"
