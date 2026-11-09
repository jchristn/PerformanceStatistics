#!/usr/bin/env bash
# Reinstall the TaskManager agent from WSL 2: runs uninstall.sh, then install.sh from this
# directory. WSL 2 counterpart of scripts/agent/linux/reinstall.sh.
# Usage: ./reinstall.sh --server http://monitor:8080 --key AGENT_KEY [--host-id ID] [--allowed-actions Kill,Suspend,Resume,SetPriority]
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

# Check the install arguments before removing anything.
has_server=0; has_key=0
for a in "$@"; do
  case "$a" in
    --server) has_server=1 ;;
    --key) has_key=1 ;;
  esac
done
[ "$has_server" = 1 ] && [ "$has_key" = 1 ] || { echo "Usage: $0 --server URL --key AGENT_KEY [--host-id ID] [--allowed-actions LIST]" >&2; exit 2; }
pid1_is_systemd() { [ "$(ps -p 1 -o comm= 2>/dev/null)" = systemd ] && command -v systemctl >/dev/null 2>&1; }
if ! pid1_is_systemd && [ -z "${WSL_DISTRO_NAME:-}" ]; then
  echo "WSL_DISTRO_NAME is not set (sudo removes it). Run this script without sudo; it calls sudo itself." >&2; exit 1
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"

bash "$SCRIPT_DIR/uninstall.sh"
exec bash "$SCRIPT_DIR/install.sh" "$@"
