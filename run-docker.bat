@echo off
rem Run the TaskManager server and web app with Docker Compose.
rem
rem   run-docker.bat                  build and start (creates docker\.env with random secrets on first run)
rem   run-docker.bat down             stop (history is kept in the taskmgr-data volume)
rem   run-docker.bat logs ^| ps ^| restart
rem
rem To monitor this Windows machine, install the native agent as Administrator:
rem   scripts\agent\windows\install.bat http://localhost:8080 AGENT_KEY
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "ACTION=%~1"
if "%ACTION%"=="" set "ACTION=up"
if /i "%ACTION%"=="-h" goto usage
if /i "%ACTION%"=="--help" goto usage

where docker >nul 2>&1 || (echo Docker is required: https://docs.docker.com/get-docker/ & exit /b 1)
docker info >nul 2>&1 || (echo The Docker daemon is not running. Start Docker Desktop and try again. & exit /b 1)

set "ENV_FILE=docker\.env"
if not exist "%ENV_FILE%" (
  for /f %%p in ('powershell -NoProfile -Command "[guid]::NewGuid().ToString('N')"') do set "ADMIN=%%p"
  for /f %%k in ('powershell -NoProfile -Command "[guid]::NewGuid().ToString('N')"') do set "AGENTKEY=%%k"
  (for /f "usebackq delims=" %%l in ("docker\.env.example") do (
    set "LINE=%%l"
    if "!LINE:~0,23!"=="TASKMGR_ADMIN_PASSWORD=" set "LINE=TASKMGR_ADMIN_PASSWORD=!ADMIN!"
    if "!LINE:~0,18!"=="TASKMGR_AGENT_KEY=" set "LINE=TASKMGR_AGENT_KEY=!AGENTKEY!"
    echo !LINE!
  )) > "%ENV_FILE%"
  echo Created %ENV_FILE% with a random admin password and agent key.
)

set "WEB_PORT=3000"
set "SERVER_PORT=8080"
for /f "usebackq tokens=1,* delims==" %%a in ("%ENV_FILE%") do (
  if "%%a"=="TASKMGR_WEB_PORT" if not "%%b"=="" set "WEB_PORT=%%b"
  if "%%a"=="TASKMGR_SERVER_PORT" if not "%%b"=="" set "SERVER_PORT=%%b"
)

set "COMPOSE=docker compose -f docker\compose.yaml --env-file %ENV_FILE%"

if /i "%ACTION%"=="up" (
  %COMPOSE% up -d --build || exit /b 1
  echo.
  echo TaskManager is running:
  echo   Web app:   http://localhost:!WEB_PORT!   ^(user: admin, password: TASKMGR_ADMIN_PASSWORD in %ENV_FILE%^)
  echo   Server:    http://localhost:!SERVER_PORT! ^(agents use TASKMGR_AGENT_KEY from %ENV_FILE%^)
  echo.
  echo Add machines by installing the agent on each one as Administrator or root, for example:
  echo   scripts\agent\windows\install.bat http://^<this-host^>:!SERVER_PORT! ^<agent key^>
  echo An agent without admin privileges reports only what it can see: some processes may be hidden or show n/a.
  exit /b 0
)
if /i "%ACTION%"=="down" (%COMPOSE% --profile linux-agent down & exit /b !errorlevel!)
if /i "%ACTION%"=="restart" (%COMPOSE% restart & exit /b !errorlevel!)
if /i "%ACTION%"=="logs" (%COMPOSE% --profile linux-agent logs -f --tail 100 & exit /b !errorlevel!)
if /i "%ACTION%"=="ps" (%COMPOSE% --profile linux-agent ps & exit /b !errorlevel!)
echo Unknown argument: %ACTION%

:usage
echo Usage: run-docker.bat [up^|down^|logs^|ps^|restart]
exit /b 2
