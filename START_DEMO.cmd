@echo off
start "WorkTime API" "%~dp0START_BACKEND.cmd"
start "WorkTime Frontend" "%~dp0START_FRONTEND.cmd"
echo Two windows launched. After startup, open http://localhost:5173
pause
