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

# Linux and WSL (TASKMGR_ASSUME_WSL=1 forces the WSL path, for scripts/tests/test-scripts.py).
IS_LINUX=0; IS_WSL=0
if [ "$(uname -s)" = Linux ]; then
  IS_LINUX=1
  if [ "${TASKMGR_ASSUME_WSL:-0}" = 1 ] || { grep -qi microsoft /proc/version 2>/dev/null \
      && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; }; then
    IS_WSL=1
  fi
fi

# .NET needs ICU (libicu) for globalization: the SDK itself and every app that does not run with
# invariant globalization. Exits with the install command when ldconfig shows it is missing; does
# nothing when DOTNET_SYSTEM_GLOBALIZATION_INVARIANT is 1/true or there is no ldconfig to ask. (The
# WSL scripts run the same check through wsl_require_cmd.) NATIVE_LDCONFIG overrides ldconfig (tests).
check_icu() {
  local ldc="${NATIVE_LDCONFIG:-}" cache arch icu="" c
  case "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" in 1|true|TRUE|True) return 0 ;; esac
  if [ -z "$ldc" ]; then
    for c in ldconfig /sbin/ldconfig /usr/sbin/ldconfig; do
      if command -v "$c" >/dev/null 2>&1; then ldc="$c"; break; fi
    done
  fi
  [ -n "$ldc" ] || return 0
  cache="$("$ldc" -p 2>/dev/null || true)"
  [ -n "$cache" ] || return 0
  case "$(uname -m)" in x86_64|amd64) arch=x86-64 ;; aarch64|arm64) arch=AArch64 ;; *) arch="" ;; esac
  # Only this machine's architecture counts (an i386 libicu does not help an x86-64 dotnet).
  if printf '%s\n' "$cache" | awk -v arch="$arch" 'index($1, "libicuuc.so.") == 1 && (arch == "" || index($0, arch)) { found = 1 } END { exit !found }'; then
    return 0
  fi
  if command -v apt-cache >/dev/null 2>&1; then
    icu="$(apt-cache pkgnames libicu 2>/dev/null | awk '/^libicu[0-9]+(t64)?$/' | sort -V | tail -n 1 || true)"
  fi
  echo "Error: .NET needs ICU (libicu), and this system does not have it. Without it dotnet fails with" >&2
  echo "\"Couldn't find a valid ICU package installed on the system\". Install it with:" >&2
  if command -v apt-get >/dev/null 2>&1; then
    echo "  sudo apt install ${icu:-libicu-dev}" >&2
  elif command -v dnf >/dev/null 2>&1; then
    echo "  sudo dnf install libicu" >&2
  else
    printf '  Debian/Ubuntu: sudo apt install %s\n  Fedora:        sudo dnf install libicu\n' "${icu:-libicu-dev}" >&2
  fi
  exit 1
}
if [ "$IS_LINUX" = 1 ]; then check_icu; fi

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

# Prints the install command for Debian/Ubuntu and Fedora packages ($1 deb, $2 rpm).
install_hint() {
  if command -v apt-get >/dev/null 2>&1; then printf '  sudo apt install %s\n' "$1"
  elif command -v dnf >/dev/null 2>&1; then printf '  sudo dnf install %s\n' "$2"
  else printf '  Debian/Ubuntu: sudo apt install %s\n  Fedora:        sudo dnf install %s\n' "$1" "$2"
  fi
}

if [ "$IS_LINUX" = 1 ]; then
  # TUIKit copies to the clipboard by running wl-copy, xclip or xsel; minimal installs and the stock
  # WSL images have none.
  if ! command -v wl-copy >/dev/null 2>&1 && ! command -v xclip >/dev/null 2>&1 && ! command -v xsel >/dev/null 2>&1; then
    echo "Note: copy to clipboard will not work in taskmgr until a clipboard tool is installed:" >&2
    install_hint xclip xclip >&2
  fi
fi

if [ "$IS_WSL" = 1 ]; then
  # Give the TUI the Windows side, as the scripts/agent/wsl* helper's wsl_prepare_app_env does: the
  # Linux PATH entries first, then the Windows (/mnt/<drive>) ones, so clip.exe, explorer.exe and
  # wslview are reachable and Linux tools win. sudo resets PATH and drops the Windows entries, so then
  # add the standard Windows directories back.
  linux_path=""; windows_path=""
  IFS=: read -r -a path_entries <<< "$PATH"
  for entry in "${path_entries[@]}"; do
    case "$entry" in
      /mnt/[a-zA-Z]/*|/mnt/[a-zA-Z]) windows_path="${windows_path:+$windows_path:}$entry" ;;
      *) linux_path="${linux_path:+$linux_path:}$entry" ;;
    esac
  done
  if [ -z "$windows_path" ]; then
    for entry in /mnt/c/Windows/System32 /mnt/c/Windows /mnt/c/Windows/System32/WindowsPowerShell/v1.0; do
      if [ -d "$entry" ]; then windows_path="${windows_path:+$windows_path:}$entry"; fi
    done
  fi
  PATH="$linux_path${windows_path:+:$windows_path}"; export PATH
  # wslview (from wslu) hands links to the Windows browser; xdg-open alone usually finds none.
  if [ -z "${BROWSER:-}" ] && command -v wslview >/dev/null 2>&1; then
    BROWSER=wslview; export BROWSER
  fi
  if ! command -v wslview >/dev/null 2>&1; then
    echo "Note: links may not open from the app until wslview (and xdg-open) are installed:" >&2
    install_hint "wslu xdg-utils" "wslu xdg-utils" >&2
  fi
fi

exec "$DOTNET" "src/TaskManager.Tui/bin/Release/$FRAMEWORK/taskmgr.dll" "$@"
