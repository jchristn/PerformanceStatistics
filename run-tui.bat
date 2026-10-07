@echo off
rem Run taskmgr, the terminal task manager, on this machine.
rem
rem   run-tui.bat                                monitor this machine
rem   run-tui.bat --record %USERPROFILE%\taskmgr.db
rem   run-tui.bat --server http://host:8080 --user admin --password ...
rem
rem Without admin privileges, other users' processes may be hidden or show n/a.
rem To see everything, run from a terminal opened with "Run as administrator".
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1 || (echo The .NET SDK 8 or 10 is required: https://dot.net & exit /b 1)

set "FRAMEWORK=net10.0"
dotnet --list-sdks | findstr /b "10." >nul || set "FRAMEWORK=net8.0"

dotnet build src\TaskManager.Tui\TaskManager.Tui.csproj -c Release -f %FRAMEWORK% -nologo -v quiet >nul || (
  dotnet build src\TaskManager.Tui\TaskManager.Tui.csproj -c Release -f %FRAMEWORK% -nologo
  exit /b 1
)

net session >nul 2>&1
if errorlevel 1 (
  echo Note: not running as Administrator. Some processes or details may be hidden; admin privileges may be required.
  echo       To see everything, open the terminal with "Run as administrator" and run run-tui.bat again.
  timeout /t 2 >nul
)

dotnet "src\TaskManager.Tui\bin\Release\%FRAMEWORK%\taskmgr.dll" %*
