@echo off
rem Install the TaskManager agent as a Windows service (run from an elevated prompt).
rem Usage: install.bat SERVER_URL AGENT_KEY [HOST_ID] [ALLOWED_ACTIONS]
rem Example: install.bat http://monitor:8080 secret-key "" "Kill,Suspend,Resume,SetPriority"
rem Quote ALLOWED_ACTIONS (and any value containing , ; or =): cmd.exe splits unquoted arguments there.
setlocal
if "%~2"=="" (
  echo Usage: %~nx0 SERVER_URL AGENT_KEY [HOST_ID] [ALLOWED_ACTIONS]
  exit /b 2
)
net session >nul 2>&1 || (echo Run this from an elevated ^(Administrator^) prompt. & exit /b 1)

set "REPO=%~dp0..\..\.."
set "DEST=%ProgramFiles%\TaskManagerAgent"
set "RID=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "RID=win-arm64"

rem Stop a running agent first: its executable is locked while it runs, so publishing over it fails.
sc.exe stop TaskManagerAgent >nul 2>&1
call :waitstop

dotnet publish "%REPO%\src\TaskManager.Agent" -c Release -f net10.0 -r %RID% --self-contained true -p:PublishSingleFile=true -o "%DEST%" || exit /b 1

sc.exe delete TaskManagerAgent >nul 2>&1
sc.exe create TaskManagerAgent binPath= "\"%DEST%\taskmgr-agent.exe\"" start= auto DisplayName= "TaskManager Agent" || exit /b 1
reg add "HKLM\SYSTEM\CurrentControlSet\Services\TaskManagerAgent" /v Environment /t REG_MULTI_SZ /f /d "TASKMGR__SERVER=%~1\0TASKMGR__AGENTKEY=%~2\0TASKMGR__HOSTID=%~3\0TASKMGR__ALLOWEDACTIONS=%~4" >nul || exit /b 1
sc.exe failure TaskManagerAgent reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
sc.exe start TaskManagerAgent
echo TaskManager agent installed.
exit /b 0

rem Waits up to 30 seconds for the service to stop (returns at once if it is not installed).
:waitstop
set /a "_tries=0"
:waitstop_loop
sc.exe query TaskManagerAgent 2>nul | findstr /i "RUNNING STOP_PENDING START_PENDING" >nul || exit /b 0
set /a "_tries+=1"
if %_tries% geq 30 exit /b 0
ping -n 2 127.0.0.1 >nul
goto waitstop_loop
