@echo off
REM Install the TaskManager agent as the TaskManagerAgent Windows service (run from an elevated prompt).
REM Fails if it is already installed; use reinstall.bat to replace it.
REM The agent runs as LocalSystem so it can read every process. It is published self-contained to
REM %ProgramFiles%\TaskManagerAgent; its settings (including the agent key) are in appsettings.json
REM there, readable only by SYSTEM and Administrators, and Windows restarts it when it fails. The
REM service name is TaskManagerAgent (display name "TaskManager Agent"); uninstall also removes the
REM agent's Application event log source.
REM This agent monitors Windows. To monitor a WSL distro too, run scripts/agent/wsl2/install.sh
REM (or scripts/agent/wsl/install.sh) inside it.
REM Usage: scripts\agent\windows\install.bat --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
REM   --server           TaskManager server URL, for example http://monitor:8080
REM   --key              agent key (TASKMGR_AGENT_KEY on the server); asked for without echo when omitted
REM   --host-id          host id to report (default: the computer name)
REM   --allowed-actions  comma-separated: Kill, KillTree, SetPriority, Suspend, Resume (default: none)
REM Quote values that contain , ; = or spaces (cmd.exe splits unquoted arguments there), for example
REM --allowed-actions "Kill,Suspend,Resume". Values cannot contain double quotes.
setlocal
for %%i in ("%~dp0..\..\..") do set "REPO=%%~fi"
set "DEST=%ProgramFiles%\TaskManagerAgent"
set "MSG="
set "SERVER="
set "KEY="
set "HOST_ID="
set "ACTIONS="
set "STAGE="
:parse
if "%~1"=="" goto parsed
if /i "%~1"=="-h" goto help
if /i "%~1"=="--help" goto help
if "%~1"=="/?" goto help
if /i "%~1"=="--server" goto opt_server
if /i "%~1"=="--key" goto opt_key
if /i "%~1"=="--host-id" goto opt_host_id
if /i "%~1"=="--allowed-actions" goto opt_actions
set "MSG=Unexpected argument: %~1 (run scripts\agent\windows\install.bat --help for usage)"
goto fail
:opt_server
call :need_value "%~1" "%~2" || goto fail
set "SERVER=%~2"
goto shift2
:opt_key
call :need_value "%~1" "%~2" || goto fail
set "KEY=%~2"
goto shift2
:opt_host_id
call :need_value "%~1" "%~2" || goto fail
set "HOST_ID=%~2"
goto shift2
:opt_actions
call :need_value "%~1" "%~2" || goto fail
set "ACTIONS=%~2"
:shift2
shift
shift
goto parse

:parsed
if not defined SERVER set "MSG=--server is required (run scripts\agent\windows\install.bat --help for usage)."
if not defined SERVER goto fail
set "SCHEME=%SERVER:~0,8%"
if /i not "%SCHEME:~0,7%"=="http://" if /i not "%SCHEME%"=="https://" set "MSG=--server must be an http:// or https:// URL (got '%SERVER%')."
if defined MSG goto fail
rem Validate the action list here: the agent refuses to start with an unknown action.
if defined ACTIONS set "ACTIONS=%ACTIONS: =%"
set "BAD_ACTION="
if defined ACTIONS for %%a in (%ACTIONS%) do call :check_action %%a
if defined BAD_ACTION set "MSG=Unknown action '%BAD_ACTION%' in --allowed-actions. Valid actions: Kill, KillTree, SetPriority, Suspend, Resume."
if defined MSG goto fail

echo TaskManager agent install (Windows)
echo [1/5] Checking prerequisites...
net session >nul 2>&1
if errorlevel 1 set "MSG=Run this from an elevated (Administrator) prompt: the service runs as LocalSystem."
if defined MSG goto fail
where dotnet >nul 2>&1
if errorlevel 1 set "MSG=dotnet was not found on PATH. Install the .NET 8 or 10 SDK: https://dotnet.microsoft.com/download"
if defined MSG goto fail
rem Pick the framework from the installed SDKs: net10.0 with a .NET 10 (or later) SDK, otherwise net8.0.
set "FRAMEWORK="
for /f "tokens=1 delims=." %%v in ('dotnet --list-sdks 2^>nul') do (
    if %%v GEQ 10 set "FRAMEWORK=net10.0"
    if %%v GEQ 8 if not defined FRAMEWORK set "FRAMEWORK=net8.0"
)
if not defined FRAMEWORK set "MSG=No .NET 8 or 10 SDK was found (dotnet --list-sdks). Install one: https://dotnet.microsoft.com/download"
if defined MSG goto fail
rem A 32-bit cmd.exe on 64-bit Windows reports x86; PROCESSOR_ARCHITEW6432 has the real one.
set "ARCH=%PROCESSOR_ARCHITECTURE%"
if defined PROCESSOR_ARCHITEW6432 set "ARCH=%PROCESSOR_ARCHITEW6432%"
set "RID="
if /i "%ARCH%"=="AMD64" set "RID=win-x64"
if /i "%ARCH%"=="ARM64" set "RID=win-arm64"
if not defined RID set "MSG=Unsupported processor architecture: %ARCH%. The agent is published for AMD64 and ARM64."
if defined MSG goto fail
echo [2/5] Checking for an existing install...
call :agent_installed
if not errorlevel 1 set "MSG=The TaskManager agent is already installed. Run scripts\agent\windows\reinstall.bat to replace it."
if defined MSG goto fail
rem Asked for only now, after the prerequisite and existing-install checks, as on every OS. Without
rem a console (redirected input) it fails like the Unix scripts. Each line PowerShell prints starts
rem with "k:", so for /f never drops it (a key starting with ";" would match the default eol).
if defined KEY goto have_key
for /f "usebackq delims=" %%k in (`powershell -NoProfile -Command "if ([Console]::IsInputRedirected) { exit 1 }; $k = Read-Host 'Agent key' -AsSecureString; 'k:' + [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($k))"`) do set "KEY=%%k"
if not defined KEY set "MSG=--key is required (it is asked for only on a console)."
if defined MSG goto fail
set "KEY=%KEY:~2%"
:have_key
if not defined KEY set "MSG=An agent key is required."
if defined MSG goto fail
echo [3/5] Building the agent (%FRAMEWORK%, %RID%)...
set "STAGE=%TEMP%\taskmgr-agent-%RANDOM%%RANDOM%"
if exist "%STAGE%\" rmdir /s /q "%STAGE%"
dotnet publish "%REPO%\src\TaskManager.Agent\TaskManager.Agent.csproj" -c Release -f %FRAMEWORK% -r %RID% --self-contained true -p:PublishSingleFile=true -o "%STAGE%" -nologo -v quiet
if errorlevel 1 set "MSG=The build failed (see above)."
if defined MSG goto fail
if not exist "%STAGE%\taskmgr-agent.exe" set "MSG=The build did not produce taskmgr-agent.exe."
if defined MSG goto fail
echo [4/5] Installing the agent in %DEST%...
if not exist "%DEST%\" mkdir "%DEST%"
xcopy "%STAGE%\*" "%DEST%\" /e /i /y /q >nul
if errorlevel 1 set "MSG=Could not copy the agent to %DEST%."
if defined MSG goto fail
rmdir /s /q "%STAGE%"
set "STAGE="
rem The settings (including the agent key) go in appsettings.json next to the exe, readable only
rem by SYSTEM and Administrators; the service's registry Environment value is readable by Users.
rem A service's working directory is System32, so its content root, where appsettings.json is
rem loaded from, is the exe directory. The ACL is set on the empty file before the key is written.
set "SETTINGS=%DEST%\appsettings.json"
type nul > "%SETTINGS%"
if errorlevel 1 set "MSG=Could not create %SETTINGS%."
if defined MSG goto fail
icacls "%SETTINGS%" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F" >nul
if errorlevel 1 set "MSG=Could not restrict access to %SETTINGS%."
if defined MSG goto fail
set "TM_FILE=%SETTINGS%"
set "TM_SERVER=%SERVER%"
set "TM_KEY=%KEY%"
set "TM_HOST_ID=%HOST_ID%"
set "TM_ACTIONS=%ACTIONS%"
powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference = 'Stop'; $s = [ordered]@{ Server = $env:TM_SERVER; AgentKey = $env:TM_KEY; HostId = $env:TM_HOST_ID; AllowedActions = $env:TM_ACTIONS }; [IO.File]::WriteAllText($env:TM_FILE, (@{ TASKMGR = $s } | ConvertTo-Json))"
if errorlevel 1 set "MSG=Could not write %SETTINGS%."
set "TM_KEY="
if defined MSG goto fail
echo [5/5] Registering and starting the TaskManagerAgent service...
sc.exe create TaskManagerAgent binPath= "\"%DEST%\taskmgr-agent.exe\"" start= auto DisplayName= "TaskManager Agent" >nul
if errorlevel 1 set "MSG=sc.exe create failed."
if defined MSG goto fail
sc.exe description TaskManagerAgent "Reports this computer to a TaskManager server." >nul
sc.exe failure TaskManagerAgent reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
if errorlevel 1 set "MSG=sc.exe failure failed."
if defined MSG goto fail
sc.exe start TaskManagerAgent >nul
if errorlevel 1 set "MSG=The service was installed but did not start; see Event Viewer, Windows Logs, Application."
if defined MSG goto fail
echo TaskManager agent installed and running as the TaskManagerAgent service.
if defined HOST_ID echo Host id: %HOST_ID%
if not defined HOST_ID echo Host id: %COMPUTERNAME%
echo This agent reports Windows. To monitor a WSL distro too, run scripts/agent/wsl2/install.sh inside it.
endlocal & exit /b 0

:help
echo Usage: scripts\agent\windows\install.bat --server URL [--key AGENT_KEY] [--host-id ID] [--allowed-actions LIST]
echo   --server           TaskManager server URL, for example http://monitor:8080
echo   --key              agent key; asked for without echo when omitted
echo   --host-id          host id to report (default: the computer name)
echo   --allowed-actions  comma-separated: Kill, KillTree, SetPriority, Suspend, Resume (default: none)
echo Quote values that contain , ; = or spaces, for example --allowed-actions "Kill,Suspend,Resume".
endlocal & exit /b 0

:fail
rem Delayed expansion prints MSG as is, even when it holds ( ) & or | from an argument.
setlocal EnableDelayedExpansion
echo Error: !MSG! 1>&2
endlocal
if defined STAGE if exist "%STAGE%\" rmdir /s /q "%STAGE%"
endlocal & exit /b 1

rem Fails (with MSG set) when option %1 has no value %2.
:need_value
set "_value=%~2"
if not defined _value goto need_value_missing
if "%_value:~0,2%"=="--" goto need_value_missing
exit /b 0
:need_value_missing
set "MSG=%~1 requires a value."
exit /b 1

rem Sets BAD_ACTION when %1 is not an agent action name.
:check_action
for %%v in (Kill KillTree SetPriority Suspend Resume) do if /i "%~1"=="%%v" exit /b 0
set "BAD_ACTION=%~1"
exit /b 0

rem True when the TaskManagerAgent service or its directory exists.
:agent_installed
sc.exe query TaskManagerAgent >nul 2>&1
if not errorlevel 1 exit /b 0
if exist "%DEST%\" exit /b 0
exit /b 1
