@echo off
setlocal EnableDelayedExpansion
title Restore Stock System Settings

:: Auto-elevate to Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

if exist "%~dp0RobloxNetworkTuner.exe" (
    "%~dp0RobloxNetworkTuner.exe" --restore
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RobloxNetworkTuner.ps1" -Mode Restore
)

echo.
echo Press any key to close...
pause >nul
exit /b 0
