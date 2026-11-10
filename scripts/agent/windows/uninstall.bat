@echo off
REM Remove the TaskManager agent Windows service (run from an elevated prompt).
REM Succeeds with a message when it is not installed. Removes every file the install wrote.
REM The agent runs as LocalSystem so it can read every process. It is published self-contained to
REM %ProgramFiles%\TaskManagerAgent; its settings (including the agent key) are in appsettings.json
REM there, readable only by SYSTEM and Administrators, and Windows restarts it when it fails. The
REM service name is TaskManagerAgent (display name "TaskManager Agent"); uninstall also removes the
REM agent's Application event log source.
REM This agent monitors Windows. To monitor a WSL distro too, run scripts/agent/wsl2/install.sh
REM (or scripts/agent/wsl/install.sh) inside it.
REM Usage: scripts\agent\windows\uninstall.bat
setlocal
for %%i in ("%~dp0..\..\..") do set "REPO=%%~fi"
set "DEST=%ProgramFiles%\TaskManagerAgent"
set "MSG="
set "STAGE="
if "%~1"=="" goto parsed
if /i "%~1"=="-h" goto help
if /i "%~1"=="--help" goto help
if "%~1"=="/?" goto help
set "MSG=Unexpected argument: %~1 (this script takes no arguments)"
goto fail
:parsed

echo TaskManager agent uninstall (Windows)
echo [1/2] Checking prerequisites...
net session >nul 2>&1
if errorlevel 1 set "MSG=Run this from an elevated (Administrator) prompt: the service runs as LocalSystem."
if defined MSG goto fail
echo [2/2] Stopping and removing the agent...
call :agent_installed
if errorlevel 1 goto not_installed
call :remove_agent
if errorlevel 1 set "MSG=Could not remove the agent (is %DEST% in use?)."
if defined MSG goto fail
rem The agent's event log provider registers its application name (the assembly name, taskmgr-agent)
rem as an Application log source the first time it logs a warning or error. Remove it too.
powershell -NoProfile -NonInteractive -Command "foreach ($n in @('taskmgr-agent', 'TaskManagerAgent')) { try { if ([Diagnostics.EventLog]::SourceExists($n)) { [Diagnostics.EventLog]::DeleteEventSource($n) } } catch { } }" >nul 2>&1
echo TaskManager agent removed.
endlocal & exit /b 0
:not_installed
echo The TaskManager agent is not installed; nothing to remove.
endlocal & exit /b 0

:help
echo Usage: scripts\agent\windows\uninstall.bat
endlocal & exit /b 0

:fail
rem Delayed expansion prints MSG as is, even when it holds ( ) & or | from an argument.
setlocal EnableDelayedExpansion
echo Error: !MSG! 1>&2
endlocal
if defined STAGE if exist "%STAGE%\" rmdir /s /q "%STAGE%"
endlocal & exit /b 1

rem True when the TaskManagerAgent service or its directory exists.
:agent_installed
sc.exe query TaskManagerAgent >nul 2>&1
if not errorlevel 1 exit /b 0
if exist "%DEST%\" exit /b 0
exit /b 1

rem Stops the service through the service manager, deletes it and removes its directory.
:remove_agent
sc.exe stop TaskManagerAgent >nul 2>&1
call :wait_state "RUNNING STOP_PENDING START_PENDING"
sc.exe query TaskManagerAgent >nul 2>&1
if errorlevel 1 goto remove_files
sc.exe delete TaskManagerAgent >nul
if errorlevel 1 exit /b 1
rem A deleted service stays "marked for deletion" until its handles close.
call :wait_gone
:remove_files
if exist "%DEST%\" rmdir /s /q "%DEST%"
if exist "%DEST%\" exit /b 1
exit /b 0

rem Waits up to 30 seconds while the service is in one of the states in %1.
:wait_state
set /a "_tries=0"
:wait_state_loop
sc.exe query TaskManagerAgent 2>nul | findstr /i "%~1" >nul || exit /b 0
set /a "_tries+=1"
if %_tries% geq 30 exit /b 0
ping -n 2 127.0.0.1 >nul
goto wait_state_loop

rem Waits up to 30 seconds for the deleted service to disappear.
:wait_gone
set /a "_tries=0"
:wait_gone_loop
sc.exe query TaskManagerAgent >nul 2>&1 || exit /b 0
set /a "_tries+=1"
if %_tries% geq 30 exit /b 0
ping -n 2 127.0.0.1 >nul
goto wait_gone_loop
