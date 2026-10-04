@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title Saleblazers JEI Installer

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Installation failed.
)
echo.
pause
