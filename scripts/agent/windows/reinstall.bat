@echo off
rem Reinstall the TaskManager agent Windows service: runs uninstall.bat, then install.bat
rem (run from an elevated prompt).
rem Usage: reinstall.bat SERVER_URL AGENT_KEY [HOST_ID] [ALLOWED_ACTIONS]
rem Example: reinstall.bat http://monitor:8080 secret-key "" "Kill,Suspend,Resume,SetPriority"
setlocal
rem Check the install arguments before removing anything.
if "%~2"=="" (
  echo Usage: %~nx0 SERVER_URL AGENT_KEY [HOST_ID] [ALLOWED_ACTIONS]
  exit /b 2
)
net session >nul 2>&1 || (echo Run this from an elevated ^(Administrator^) prompt. & exit /b 1)
where dotnet >nul 2>&1 || (echo dotnet was not found on PATH; install the .NET SDK first. & exit /b 1)

call "%~dp0uninstall.bat"
call "%~dp0install.bat" %*
exit /b %errorlevel%
