@echo off
net session >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Run as administrator.
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0INSTALL-SERVICE.ps1"
exit /b %ERRORLEVEL%
