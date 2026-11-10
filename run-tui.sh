#!/usr/bin/env bash
# Run taskmgr, the terminal task manager, on this machine.
#
#   ./run-tui.sh                                  monitor this machine
#   ./run-tui.sh --record ~/taskmgr.db            ...and record history to SQLite
#   ./run-tui.sh --server http://host:8080 --user admin --password '...'
#   sudo ./run-tui.sh                             see every process (admin/root privileges)
#
# Without admin/root privileges, other users' processes may be hidden or show n/a
# (macOS hides their CPU, I/O, and command line; Linux hides their I/O).
set -euo pipefail
cd "$(dirname "$0")"

# sudo resets PATH, so look for dotnet in the usual places too.
DOTNET="${DOTNET:-}"
if [ -z "$DOTNET" ]; then
  for candidate in "$(command -v dotnet 2>/dev/null || true)" /usr/local/share/dotnet/dotnet /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet "${SUDO_USER:+/Users/$SUDO_USER/.dotnet/dotnet}" "${SUDO_USER:+/home/$SUDO_USER/.dotnet/dotnet}" "$HOME/.dotnet/dotnet"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then DOTNET="$candidate"; break; fi
  done
fi
[ -n "$DOTNET" ] || { echo "The .NET SDK (8 or 10) is required: https://dot.net" >&2; exit 1; }

# Capture first: under 'set -o pipefail', '"$DOTNET" --list-sdks | grep -q' can fail with SIGPIPE when
# grep exits early, which would silently pick net8.0 on a machine with a .NET 10 SDK.
SDKS="$("$DOTNET" --list-sdks 2>/dev/null || true)"
case "
$SDKS" in
  *"
1"[0-9]"."*) FRAMEWORK=net10.0 ;;
  *) FRAMEWORK=net8.0 ;;
esac

# Build as the invoking user so a sudo run does not leave root-owned build output behind.
BUILD=("$DOTNET" build src/TaskManager.Tui/TaskManager.Tui.csproj -c Release -f "$FRAMEWORK" -nologo -v quiet)
if [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ]; then
  sudo -u "$SUDO_USER" "${BUILD[@]}" >/dev/null || { "${BUILD[@]}"; exit 1; }
else
  "${BUILD[@]}" >/dev/null || { "${BUILD[@]}"; exit 1; }
fi

if [ "$(id -u)" -ne 0 ]; then
  echo "Note: not running as root. Some processes or details may be hidden; admin privileges may be required."
  echo "      To see everything: sudo ./run-tui.sh $*"
  sleep 2
fi

exec "$DOTNET" "src/TaskManager.Tui/bin/Release/$FRAMEWORK/taskmgr.dll" "$@"
