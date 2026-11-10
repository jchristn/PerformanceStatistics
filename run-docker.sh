#!/usr/bin/env bash
# Run the TaskManager server and web app with Docker Compose.
#
#   ./run-docker.sh                   build and start (creates docker/.env with random secrets on first run)
#   ./run-docker.sh --linux-agent     also monitor this Docker host (Linux hosts only)
#   ./run-docker.sh down              stop (history is kept in the taskmgr-data volume)
#   ./run-docker.sh logs | ps | restart
set -euo pipefail
cd "$(dirname "$0")"

usage() { sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'; }

ACTION=up
PROFILE=()
for arg in "$@"; do
  case "$arg" in
    up|down|logs|ps|restart) ACTION="$arg" ;;
    --linux-agent) PROFILE=(--profile linux-agent) ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage >&2; exit 2 ;;
  esac
done

command -v docker >/dev/null || { echo "Docker is required: https://docs.docker.com/get-docker/" >&2; exit 1; }
docker info >/dev/null 2>&1 || { echo "The Docker daemon is not running. Start Docker and try again." >&2; exit 1; }

ENV_FILE=docker/.env
if [ ! -f "$ENV_FILE" ]; then
  # head closes the pipe early, so tr gets SIGPIPE; '|| true' keeps that from counting under pipefail.
  random() { LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 24 || true; }
  umask 077
  sed -e "s/^TASKMGR_ADMIN_PASSWORD=.*/TASKMGR_ADMIN_PASSWORD=$(random)/" \
      -e "s/^TASKMGR_AGENT_KEY=.*/TASKMGR_AGENT_KEY=$(random)/" \
      docker/.env.example > "$ENV_FILE"
  echo "Created $ENV_FILE with a random admin password and agent key."
fi

# Prints the last value of $1 in the env file, or nothing. awk instead of grep: under 'set -o pipefail'
# a missing key made 'grep | tail' fail, which aborted the script after 'compose up'.
value() { awk -v k="$1" 'index($0, k "=") == 1 { v = substr($0, length(k) + 2) } END { print v }' "$ENV_FILE"; }
COMPOSE=(docker compose -f docker/compose.yaml --env-file "$ENV_FILE")

case "$ACTION" in
  up)
    if [ ${#PROFILE[@]} -gt 0 ]; then
      if [ "$(uname -s)" = Darwin ]; then
        echo "Note: on Docker Desktop the linux-agent monitors Docker's Linux VM, not this machine."
        echo "      To monitor this machine, install the native agent: scripts/agent/macos/install.sh"
      elif [ -n "${WSL_DISTRO_NAME:-}" ]; then
        echo "Note: under WSL the linux-agent monitors Docker's Linux VM, not Windows or this distro."
        echo "      Install the native agent instead: scripts\\agent\\windows\\install.bat (Windows) or scripts/agent/wsl2/install.sh (this distro)."
      fi
    fi
    "${COMPOSE[@]}" ${PROFILE[@]+"${PROFILE[@]}"} up -d --build
    WEB_PORT="$(value TASKMGR_WEB_PORT)"; WEB_PORT="${WEB_PORT:-3000}"
    SERVER_PORT="$(value TASKMGR_SERVER_PORT)"; SERVER_PORT="${SERVER_PORT:-8080}"
    echo
    echo "TaskManager is running:"
    echo "  Web app:   http://localhost:$WEB_PORT   (user: admin, password: TASKMGR_ADMIN_PASSWORD in $ENV_FILE)"
    echo "  Server:    http://localhost:$SERVER_PORT (agents use TASKMGR_AGENT_KEY from $ENV_FILE)"
    echo
    echo "Add machines by installing the agent on each one as root/Administrator, for example:"
    echo "  scripts/agent/linux/install.sh --server http://<this-host>:$SERVER_PORT --key <agent key>   (also macos, wsl2, wsl; it calls sudo)"
    echo "  scripts\\agent\\windows\\install.bat --server http://<this-host>:$SERVER_PORT --key <agent key>   (from an elevated prompt)"
    echo "An agent without admin privileges reports only what it can see: some processes may be hidden or show n/a."
    ;;
  down) "${COMPOSE[@]}" --profile linux-agent down ;;
  restart) "${COMPOSE[@]}" ${PROFILE[@]+"${PROFILE[@]}"} restart ;;
  logs) "${COMPOSE[@]}" --profile linux-agent logs -f --tail 100 ;;
  ps) "${COMPOSE[@]}" --profile linux-agent ps ;;
esac
