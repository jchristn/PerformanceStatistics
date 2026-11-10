#!/usr/bin/env bash
# Rebuild and reinstall the TaskManager agent (the taskmgr-agent systemd service).
# It builds first, then stops and replaces the installed agent; works when none is installed, too.
# The agent runs as root so it can read every process. It is published self-contained to
# /opt/taskmgr-agent; its settings (including the agent key) are in /etc/taskmgr-agent.env
# (root:root, mode 600) and the unit is /etc/systemd/system/taskmgr-agent.service (Restart=always).
# Run it as your normal user (it calls sudo for the steps that need root) or as root.
# Usage: ./scripts/agent/linux/reinstall.sh --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
#   --server           TaskManager server URL, for example http://monitor:8080
#   --key              agent key (TASKMGR_AGENT_KEY on the server); asked for without echo when omitted
#   --host-id          host id to report (default: the machine name)
#   --allowed-actions  comma-separated: Kill, KillTree, SetPriority, Suspend, Resume (default: none)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }
# Prints the header comment above as usage text.
usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/agent/macos on macOS or scripts\\agent\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL: Docker Desktop containers have one too, but no WSL interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/agent/wsl2 (WSL 2) or scripts/agent/wsl (WSL 1); systemd services do not start at Windows login."
fi

# Native-library checks and installs: the scripts/wsl*/_wsl-common.sh "Native libraries" section
# with native_ names (generated from it, same wording).
native_log() { printf '%s\n' "$*" >&2; }
native_die() { printf 'Error: %s\n' "$*" >&2; exit 1; }

# --- Native libraries -------------------------------------------------------------------------------
# Minimal and server installs often lack these libraries. Building never loads them, so
# without a check the failure only shows when the app starts, as "Unable to load shared library ...".

# Libraries a .NET GUI app built on Avalonia 12 loads on Linux: the sonames inside Avalonia.X11.dll
# (12.1.x) plus SkiaSharp's fontconfig. libGL, GTK 3 and Vulkan are optional (software rendering and
# managed dialogs are the fallbacks). Each entry is soname:Debian/Ubuntu package:Fedora package.
# shellcheck disable=SC2034 # used by the scripts that source this file
NATIVE_AVALONIA_LIBS="libX11.so.6:libx11-6:libX11 libICE.so.6:libice6:libICE libSM.so.6:libsm6:libSM libXext.so.6:libxext6:libXext libXrandr.so.2:libxrandr2:libXrandr libXi.so.6:libxi6:libXi libXcursor.so.1:libxcursor1:libXcursor libXfixes.so.3:libxfixes3:libXfixes libfontconfig.so.1:libfontconfig1:fontconfig"

# Prints the ldconfig cache (ldconfig -p), or nothing when there is no ldconfig to ask.
# NATIVE_LDCONFIG overrides the ldconfig command (used by tests).
native_ldconfig_cache() {
    _wl_ldc="${NATIVE_LDCONFIG:-}"
    if [ -z "$_wl_ldc" ]; then
        for _wl_c in ldconfig /sbin/ldconfig /usr/sbin/ldconfig; do
            if command -v "$_wl_c" >/dev/null 2>&1; then _wl_ldc="$_wl_c"; break; fi
        done
    fi
    [ -n "$_wl_ldc" ] || return 0
    "$_wl_ldc" -p 2>/dev/null || true
}

# True when the dynamic linker has <soname> (exact name, or a prefix ending in a dot such as
# "libicuuc.so.") for this machine's architecture. Usage: native_have_lib <cache> <soname>
native_have_lib() {
    case "$(uname -m)" in
        x86_64|amd64) _wl_arch=x86-64 ;;
        aarch64|arm64) _wl_arch=AArch64 ;;
        *) _wl_arch="" ;;
    esac
    printf '%s\n' "$1" | awk -v so="$2" -v arch="$_wl_arch" '
        { name = $1 }
        (name == so || (substr(so, length(so)) == "." && index(name, so) == 1)) && (arch == "" || index($0, arch)) { found = 1 }
        END { exit !found }'
}

# Prints the install command for the given Debian and Fedora package lists.
# Usage: native_install_hint "<deb packages>" "<rpm packages>"
native_install_hint() {
    if command -v apt-get >/dev/null 2>&1; then
        printf '  sudo apt install %s\n' "$1"
    elif command -v dnf >/dev/null 2>&1; then
        printf '  sudo dnf install %s\n' "$2"
    else
        printf '  Debian/Ubuntu: sudo apt install %s\n  Fedora:        sudo dnf install %s\n' "$1" "$2"
    fi
}


# .NET needs ICU (libicu) for globalization: the SDK itself and every app that does not run with
# invariant globalization. dotnet-install.sh and self-contained apps do not bring it. Exits with the
# install command when it is missing; does nothing when DOTNET_SYSTEM_GLOBALIZATION_INVARIANT is set
# to 1/true, or when there is no ldconfig to ask.
native_check_icu() {
    case "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" in 1|true|TRUE|True) return 0 ;; esac
    _wl_cache="$(native_ldconfig_cache)"
    [ -n "$_wl_cache" ] || return 0
    native_have_lib "$_wl_cache" "libicuuc.so." && return 0
    _wl_icu=""
    if command -v apt-cache >/dev/null 2>&1; then
        _wl_icu="$(apt-cache pkgnames libicu 2>/dev/null | awk '/^libicu[0-9]+(t64)?$/' | sort -V | tail -n 1 || true)"
    fi
    native_log "Error: .NET needs ICU (libicu), and this system does not have it. Without it dotnet fails with"
    native_log "\"Couldn't find a valid ICU package installed on the system\". Install it with:"
    native_log "$(native_install_hint "${_wl_icu:-libicu-dev}" libicu)"
    exit 1
}


# Installs the system packages an app needs, when they are missing: install
# scripts call it so the app does not fail later at startup. Uses apt-get (Debian/Ubuntu) or dnf
# (Fedora), through sudo unless running as root. INSTALL_SYSTEM_PACKAGES=0 turns it into a report.
# Usage: native_install_deps "<app name>" [icu] [gui] [clipboard] [browser]
#   icu        ICU for .NET (required)                 gui      the Avalonia libraries (required)
#   clipboard  xclip when no clipboard tool exists    browser  wslu + xdg-utils, to open links in Windows
# Required packages that cannot be installed are fatal; optional ones only warn.
native_install_deps() {
    _wd_app="$1"; shift
    _wd_cache="$(native_ldconfig_cache)"
    _wd_req_deb=""; _wd_req_rpm=""; _wd_opt_deb=""; _wd_opt_rpm=""; _wd_apt_icu=0
    for _wd_want in "$@"; do
        case "$_wd_want" in
            icu)
                case "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" in 1|true|TRUE|True) continue ;; esac
                if [ -n "$_wd_cache" ] && ! native_have_lib "$_wd_cache" "libicuuc.so."; then
                    _wd_apt_icu=1; _wd_req_rpm="$_wd_req_rpm libicu"
                fi ;;
            gui)
                [ -n "$_wd_cache" ] || continue
                for _wd_e in $NATIVE_AVALONIA_LIBS; do
                    native_have_lib "$_wd_cache" "${_wd_e%%:*}" && continue
                    _wd_r="${_wd_e#*:}"
                    _wd_req_deb="$_wd_req_deb ${_wd_r%%:*}"; _wd_req_rpm="$_wd_req_rpm ${_wd_r#*:}"
                done ;;
            clipboard)
                if ! command -v wl-copy >/dev/null 2>&1 && ! command -v xclip >/dev/null 2>&1 && ! command -v xsel >/dev/null 2>&1; then
                    _wd_opt_deb="$_wd_opt_deb xclip"; _wd_opt_rpm="$_wd_opt_rpm xclip"
                fi ;;
            browser)
                if ! command -v wslview >/dev/null 2>&1; then _wd_opt_deb="$_wd_opt_deb wslu"; _wd_opt_rpm="$_wd_opt_rpm wslu"; fi
                if ! command -v xdg-open >/dev/null 2>&1; then _wd_opt_deb="$_wd_opt_deb xdg-utils"; _wd_opt_rpm="$_wd_opt_rpm xdg-utils"; fi ;;
            *) native_die "native_install_deps: unknown package group: $_wd_want" ;;
        esac
    done
    [ -n "$_wd_req_deb$_wd_req_rpm$_wd_opt_deb$_wd_opt_rpm" ] || [ "$_wd_apt_icu" = 1 ] || return 0

    if command -v apt-get >/dev/null 2>&1; then _wd_pm=apt; elif command -v dnf >/dev/null 2>&1; then _wd_pm=dnf; else _wd_pm=""; fi
    if [ "$(id -u)" = 0 ]; then _wd_sudo=""; elif command -v sudo >/dev/null 2>&1; then _wd_sudo=sudo; else _wd_sudo=none; fi
    if [ "${INSTALL_SYSTEM_PACKAGES:-1}" = 0 ] || [ -z "$_wd_pm" ] || [ "$_wd_sudo" = none ]; then
        _wd_icu_deb=""; [ "$_wd_apt_icu" = 1 ] && _wd_icu_deb=" libicu-dev"
        native_log "$_wd_app needs system packages that are not installed, and they were not installed automatically"
        native_log "(INSTALL_SYSTEM_PACKAGES=0, no apt-get/dnf, or no sudo). Install them with:"
        native_log "$(native_install_hint "${_wd_req_deb# }$_wd_icu_deb$_wd_opt_deb" "${_wd_req_rpm# }$_wd_opt_rpm")"
        if [ -n "$_wd_req_deb$_wd_req_rpm" ] || [ "$_wd_apt_icu" = 1 ]; then exit 1; fi
        return 0
    fi

    if [ "$_wd_pm" = apt ]; then
        native_log "Installing the system packages $_wd_app needs (apt-get; sudo may ask for your password)..."
        # shellcheck disable=SC2086 # empty or "sudo"
        $_wd_sudo apt-get update -qq || native_die "apt-get update failed."
        if [ "$_wd_apt_icu" = 1 ]; then
            _wd_icu="$(apt-cache pkgnames libicu 2>/dev/null | awk '/^libicu[0-9]+(t64)?$/' | sort -V | tail -n 1 || true)"
            _wd_req_deb="$_wd_req_deb ${_wd_icu:-libicu-dev}"
        fi
        if [ -n "$_wd_req_deb" ]; then
            # shellcheck disable=SC2086 # word-split package lists on purpose
            DEBIAN_FRONTEND=noninteractive $_wd_sudo apt-get install -y -q $_wd_req_deb \
                || native_die "Could not install the required packages:$_wd_req_deb"
        fi
        if [ -n "$_wd_opt_deb" ]; then
            # shellcheck disable=SC2086 # word-split package lists on purpose
            DEBIAN_FRONTEND=noninteractive $_wd_sudo apt-get install -y -q $_wd_opt_deb \
                || native_log "Warning: could not install the optional packages:$_wd_opt_deb (copy to clipboard or opening links may not work)."
        fi
    else
        native_log "Installing the system packages $_wd_app needs (dnf; sudo may ask for your password)..."
        if [ -n "$_wd_req_rpm" ]; then
            # shellcheck disable=SC2086 # word-split package lists on purpose
            $_wd_sudo dnf install -y $_wd_req_rpm || native_die "Could not install the required packages:$_wd_req_rpm"
        fi
        if [ -n "$_wd_opt_rpm" ]; then
            # shellcheck disable=SC2086 # word-split package lists on purpose
            $_wd_sudo dnf install -y $_wd_opt_rpm \
                || native_log "Warning: could not install the optional packages:$_wd_opt_rpm (copy to clipboard or opening links may not work)."
        fi
    fi
    native_log "System packages for $_wd_app are installed."
}

SERVER=""; KEY=""; HOST_ID=""; ACTIONS=""
# Prints the value of option $1 (the next argument), or fails when it is missing.
need_value() { [ "$#" -ge 2 ] && [ -n "$2" ] && [ "${2#--}" = "$2" ] || die "$1 requires a value."; }
while [ "$#" -gt 0 ]; do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        --server) need_value "$@"; SERVER="$2"; shift ;;
        --key) need_value "$@"; KEY="$2"; shift ;;
        --host-id) need_value "$@"; HOST_ID="$2"; shift ;;
        --allowed-actions) need_value "$@"; ACTIONS="$2"; shift ;;
        *) die "Unexpected argument: $1 (run scripts/agent/linux/reinstall.sh --help for usage)" ;;
    esac
    shift
done
[ -n "$SERVER" ] || die "--server is required (run scripts/agent/linux/reinstall.sh --help for usage)."
# Case-insensitive, as in the Windows script.
case "$(printf '%s' "$SERVER" | tr '[:upper:]' '[:lower:]')" in
    http://*|https://*) ;;
    *) die "--server must be an http:// or https:// URL (got '$SERVER')." ;;
esac
# Validate the action list here: the agent refuses to start with an unknown action.
ACTIONS="$(printf '%s' "$ACTIONS" | tr -d ' ')"
IFS=, read -r -a ACTION_LIST <<< "$ACTIONS,"
for action in "${ACTION_LIST[@]}"; do
    case "$(printf '%s' "$action" | tr '[:upper:]' '[:lower:]')" in
        ''|kill|killtree|setpriority|suspend|resume) ;;
        *) die "Unknown action '$action' in --allowed-actions. Valid actions: Kill, KillTree, SetPriority, Suspend, Resume." ;;
    esac
done
for value in "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS"; do
    case "$value" in
        *"'"*|*'"'*|*$'\n'*) die "Values must not contain quotes or newlines." ;;
    esac
done

# Steps that need root go through sudo when this script runs as a normal user.
as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
# For the script tests only: puts every system path under this directory.
P="${TASKMGR_AGENT_TEST_ROOT:-}"
DEST="$P/opt/taskmgr-agent"
ENV_FILE="$P/etc/taskmgr-agent.env"
UNIT="$P/etc/systemd/system/taskmgr-agent.service"
# shellcheck disable=SC2034 # used by the removal steps
WANTS="$P/etc/systemd/system/multi-user.target.wants/taskmgr-agent.service"
# True when any part of an agent install is present.
agent_installed() { [ -e "$UNIT" ] || [ -e "$DEST" ] || [ -e "$ENV_FILE" ]; }

echo "TaskManager agent reinstall (Linux)"
echo "[1/5] Checking prerequisites..."
if [ "$(id -u)" -ne 0 ]; then
    command -v sudo >/dev/null 2>&1 || die "sudo was not found. Run this script as root."
fi
if [ "$(ps -p 1 -o comm= 2>/dev/null || true)" != systemd ] || ! command -v systemctl >/dev/null 2>&1; then
    die "This script installs a systemd service, but systemd is not running here (PID 1 is $(ps -p 1 -o comm= 2>/dev/null || echo unknown))."
fi
# The .NET SDK needs ICU to build the agent (the agent itself runs with invariant globalization).
# Install it now if it is missing (INSTALL_SYSTEM_PACKAGES=0 only reports it), before the dotnet check.
native_install_deps "The TaskManager agent build" icu
# sudo resets PATH: also look in the invoking user's ~/.dotnet and the usual install directories.
DOTNET="$(command -v dotnet 2>/dev/null || true)"
if [ -z "$DOTNET" ] && [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ]; then
    user_home="$(getent passwd "$SUDO_USER" | cut -d: -f6)"
    for candidate in "$user_home/.dotnet/dotnet" /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet /usr/local/share/dotnet/dotnet; do
        if [ -x "$candidate" ]; then DOTNET="$candidate"; break; fi
    done
    if [ "$DOTNET" = "$user_home/.dotnet/dotnet" ] && [ -z "${DOTNET_ROOT:-}" ]; then export DOTNET_ROOT="$user_home/.dotnet"; fi
fi
[ -n "$DOTNET" ] || die "dotnet was not found on PATH. Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
# Pick the framework from the installed SDKs; capture first, because under 'set -o pipefail'
# 'dotnet --list-sdks | grep -q' can fail with SIGPIPE.
SDKS="$("$DOTNET" --list-sdks 2>/dev/null || true)"
case "
$SDKS" in
    *"
1"[0-9]"."*) FRAMEWORK=net10.0 ;;
    *"
8."*|*"
9."*) FRAMEWORK=net8.0 ;;
    *) die "No .NET SDK was found ($DOTNET --list-sdks is empty). Install the .NET 8 or 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux" ;;
esac
case "$(uname -m)" in
    x86_64|amd64) RID=linux-x64 ;;
    aarch64|arm64) RID=linux-arm64 ;;
    armv7l|armv7*) RID=linux-arm ;;
    *) die "Unsupported processor architecture: $(uname -m). The agent is published for x86_64, aarch64 and armv7l." ;;
esac
# Asked for only now, after the prerequisite and existing-install checks, as on every OS.
if [ -z "$KEY" ]; then
    [ -t 0 ] || die "--key is required (it is asked for only on a terminal)."
    read -r -s -p "Agent key: " KEY
    echo
    [ -n "$KEY" ] || die "An agent key is required."
    case "$KEY" in *"'"*|*'"'*) die "Values must not contain quotes or newlines." ;; esac
fi
echo "[2/5] Building the agent ($FRAMEWORK, $RID)..."
# Build as the invoking user, never as root, so the repository gets no root-owned build output.
as_builder() {
    if [ "$(id -u)" -eq 0 ] && [ -n "${SUDO_USER:-}" ] && [ "$SUDO_USER" != root ]; then
        sudo -u "$SUDO_USER" -H env "PATH=$PATH" ${DOTNET_ROOT:+"DOTNET_ROOT=$DOTNET_ROOT"} "$@"
    else
        "$@"
    fi
}
STAGE="$(as_builder mktemp -d "${TMPDIR:-/tmp}/taskmgr-agent.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
as_builder "$DOTNET" publish "$REPO_ROOT/src/TaskManager.Agent/TaskManager.Agent.csproj" -c Release -f "$FRAMEWORK" \
    -r "$RID" --self-contained true -p:PublishSingleFile=true -o "$STAGE" -nologo -v quiet
[ -f "$STAGE/taskmgr-agent" ] || die "The build did not produce $STAGE/taskmgr-agent."
echo "[3/5] Stopping and removing the installed agent..."
if agent_installed; then
    if command -v systemctl >/dev/null 2>&1; then as_root systemctl disable --now taskmgr-agent 2>/dev/null || true; fi
    as_root rm -f "$UNIT" "$WANTS" "$ENV_FILE"
    if command -v systemctl >/dev/null 2>&1; then as_root systemctl daemon-reload; fi
    as_root rm -rf "$DEST"
else
    echo "The TaskManager agent is not installed; continuing."
fi
echo "[4/5] Installing the agent and its settings in $DEST..."
as_root sh -c 'rm -rf "$1" && mkdir -p "$1" && cp -R "$2/." "$1/" && chmod 755 "$1/taskmgr-agent"' sh "$DEST" "$STAGE"
# Single-quoted values work both for systemd's EnvironmentFile and for sh.
printf "TASKMGR__SERVER='%s'\nTASKMGR__AGENTKEY='%s'\nTASKMGR__HOSTID='%s'\nTASKMGR__ALLOWEDACTIONS='%s'\n" \
    "$SERVER" "$KEY" "$HOST_ID" "$ACTIONS" | as_root sh -c 'umask 077 && mkdir -p "$(dirname "$1")" && cat > "$1"' sh "$ENV_FILE"
# Readable by root only: it holds the agent key.
as_root chown root:root "$ENV_FILE"
as_root chmod 600 "$ENV_FILE"
echo "[5/5] Registering and starting the systemd service..."
as_root mkdir -p "$(dirname "$UNIT")"
as_root tee "$UNIT" >/dev/null <<UNITEOF
[Unit]
Description=TaskManager agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=$DEST/taskmgr-agent
EnvironmentFile=$ENV_FILE
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
UNITEOF
as_root systemctl daemon-reload
as_root systemctl enable --now taskmgr-agent
as_root systemctl --no-pager status taskmgr-agent 2>/dev/null | head -n 5 || true
echo "TaskManager agent installed and running as the taskmgr-agent systemd service (host id: ${HOST_ID:-the machine name})."
echo "Logs: journalctl -u taskmgr-agent"
