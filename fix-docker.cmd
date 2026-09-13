@echo off
chcp 65001 >nul
echo.
echo 1) In the tray: Docker whale -^> Quit  (NOT Restart)
echo 2) This script will detach the broken disk and start Desktop again.
echo.
pause
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\start-docker-fresh.ps1"
echo.
echo Code: %ERRORLEVEL%
echo Log:  D:\start-docker-fresh.log
echo.
echo When the whale is idle, run:  docker version
echo Then:  .\up-docker.ps1
echo.
pause
