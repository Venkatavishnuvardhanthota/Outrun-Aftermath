@echo off
echo ===================================================
echo   OUTRUN: AFTERMATH - STOPPING ALL GAME SERVICES
echo ===================================================
taskkill /f /im OutrunAftermath_Client.exe 2>nul
taskkill /f /im OutrunAftermath_Server.exe 2>nul
taskkill /f /im OutrunAftermath.exe 2>nul
taskkill /f /fi "WINDOWTITLE eq Outrun API*" 2>nul
taskkill /f /fi "WINDOWTITLE eq Outrun Server*" 2>nul
echo All OUTRUN processes terminated.
timeout /t 2 /nobreak >nul
