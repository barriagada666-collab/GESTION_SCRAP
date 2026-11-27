@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\Build-Release.ps1" %*
exit /b %errorlevel%
