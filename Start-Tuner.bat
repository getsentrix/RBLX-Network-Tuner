@echo off
setlocal EnableDelayedExpansion
title Roblox Low-Latency Network Tuner

:: Auto-elevate to Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

if exist "%~dp0RobloxNetworkTuner.exe" (
    "%~dp0RobloxNetworkTuner.exe"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RobloxNetworkTuner.ps1" -Mode Interactive
)

exit /b %errorlevel%
