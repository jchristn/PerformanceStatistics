@echo off
rem Remove the TaskManager agent Windows service (run from an elevated prompt).
net session >nul 2>&1 || (echo Run this from an elevated (Administrator) prompt. & exit /b 1)
sc.exe stop TaskManagerAgent >nul 2>&1
sc.exe delete TaskManagerAgent
rmdir /s /q "%ProgramFiles%\TaskManagerAgent"
echo TaskManager agent removed.
