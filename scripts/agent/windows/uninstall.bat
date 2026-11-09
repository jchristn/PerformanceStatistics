@echo off
rem Remove the TaskManager agent Windows service (run from an elevated prompt).
setlocal
net session >nul 2>&1 || (echo Run this from an elevated ^(Administrator^) prompt. & exit /b 1)
sc.exe stop TaskManagerAgent >nul 2>&1
rem Wait for the service to exit so its executable is no longer locked.
call :waitstop
sc.exe delete TaskManagerAgent
if exist "%ProgramFiles%\TaskManagerAgent" rmdir /s /q "%ProgramFiles%\TaskManagerAgent"
echo TaskManager agent removed.
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
