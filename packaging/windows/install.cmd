@echo off
rem Runs install.ps1 without an execution policy prompt. Parameters: -Autostart, -Uninstall
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
pause
