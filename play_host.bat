@echo off
echo ===================================================
echo   OUTRUN: AFTERMATH - STARTING LOCAL HOST MATCH
echo ===================================================
echo Launching client as local authoritative host on port 7777...
echo.
start "" "%~dp0client\Builds\WindowsClient\OutrunAftermath_Client.exe" -host -port 7777
