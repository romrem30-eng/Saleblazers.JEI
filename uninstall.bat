@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title Saleblazers JEI Uninstaller

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; & '.\uninstall.ps1'"
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Uninstallation was cancelled or failed.
)
echo.
pause
