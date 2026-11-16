@echo off
echo ===================================================
echo   OUTRUN: AFTERMATH - STARTING FULL MULTIPLAYER
echo ===================================================
echo 1. Starting Backend Web API on http://localhost:5000...
start /min "Outrun API" dotnet run --project "%~dp0backend\src\OutrunAftermath.Api" --urls http://0.0.0.0:5000 --no-build

timeout /t 3 /nobreak >nul

echo 2. Starting Dedicated Server on UDP port 7777...
start /min "Outrun Server" "%~dp0client\Builds\WindowsServer\OutrunAftermath_Server.exe" -batchmode -nographics -server -port 7777

timeout /t 2 /nobreak >nul

echo 3. Launching Game Client...
start "" "%~dp0client\Builds\WindowsClient\OutrunAftermath_Client.exe"

echo.
echo All services are running!
echo To stop everything later, double-click stop_game.bat
