#!/usr/bin/env bash
# Reinstall the TaskManager agent systemd service: runs uninstall.sh, then install.sh.
# Usage: sudo ./reinstall.sh --server http://monitor:8080 --key AGENT_KEY [--host-id ID] [--allowed-actions Kill,Suspend,Resume,SetPriority]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Check the install arguments before removing anything.
has_server=0; has_key=0
for a in "$@"; do
  case "$a" in
    --server) has_server=1 ;;
    --key) has_key=1 ;;
  esac
done
[ "$has_server" = 1 ] && [ "$has_key" = 1 ] || { echo "Usage: sudo $0 --server URL --key AGENT_KEY [--host-id ID] [--allowed-actions LIST]" >&2; exit 2; }
[ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo "dotnet was not found on root's PATH. Try: sudo env \"PATH=\$PATH\" $0 ..." >&2; exit 1; }

bash "$SCRIPT_DIR/uninstall.sh"
exec bash "$SCRIPT_DIR/install.sh" "$@"
