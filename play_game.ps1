<#
.SYNOPSIS
    1-Click Master Game Launcher for OUTRUN: AFTERMATH.
.DESCRIPTION
    Provides turnkey execution profiles for solo, local host, and full online multiplayer.
.EXAMPLE
    .\play_game.ps1               # Launches Solo / Offline Practice client immediately
    .\play_game.ps1 -Mode Host    # Launches client and starts local host
    .\play_game.ps1 -Mode Online  # Spawns Web API + Dedicated Server + Client
    .\play_game.ps1 -Stop         # Shuts down background API & Dedicated Server
#>
param(
    [ValidateSet("Solo", "Host", "Online", "Stop")]
    [string]$Mode = "Solo"
)

$clientExe = "$PSScriptRoot\client\Builds\WindowsClient\OutrunAftermath_Client.exe"
if (-not (Test-Path $clientExe)) {
    $clientExe = "$PSScriptRoot\client\Builds\Windows\OutrunAftermath.exe"
}
$serverExe = "$PSScriptRoot\client\Builds\WindowsServer\OutrunAftermath_Server.exe"
if (-not (Test-Path $serverExe)) {
    $serverExe = "$PSScriptRoot\client\Builds\Windows\OutrunAftermath.exe"
}
$apiDir = "$PSScriptRoot\backend\src\OutrunAftermath.Api"

if ($Mode -eq "Stop") {
    Write-Host "Stopping all OUTRUN background processes..." -ForegroundColor Yellow
    Get-Process OutrunAftermath* -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -match "OutrunAftermath" -or $_.CommandLine -match "OutrunAftermath.Api" } | Stop-Process -Force
    Write-Host "All background game processes stopped." -ForegroundColor Green
    exit 0
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "       OUTRUN: AFTERMATH — MASTER LAUNCHER       " -ForegroundColor Cyan
Write-Host "       Mode: $Mode                               " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

if (-not (Test-Path $clientExe)) {
    Write-Error "Client executable not found at '$clientExe'. Please run .\build_standalone.ps1 first."
    exit 1
}

switch ($Mode) {
    "Solo" {
        Write-Host "Launching OUTRUN: AFTERMATH Game Client..." -ForegroundColor Green
        Write-Host "TIP: In the lobby menu, select your Map & Weather, then click [OFFLINE PRACTICE] to enter combat!" -ForegroundColor Yellow
        Start-Process -FilePath $clientExe
    }

    "Host" {
        Write-Host "Launching OUTRUN: AFTERMATH in Local Host Mode..." -ForegroundColor Green
        Start-Process -FilePath $clientExe -ArgumentList "-host", "-port", "7777"
    }

    "Online" {
        Write-Host "1. Starting Backend Web API on http://localhost:5000 in background..." -ForegroundColor Green
        $apiProc = Start-Process -FilePath "dotnet" `
            -ArgumentList "run", "--project", "`"$apiDir`"", "--urls", "http://0.0.0.0:5000", "--no-build" `
            -PassThru -WindowStyle Minimized

        Start-Sleep -Seconds 3

        Write-Host "2. Starting Dedicated Server on UDP :7777 in background..." -ForegroundColor Green
        $srvProc = Start-Process -FilePath $serverExe `
            -ArgumentList "-batchmode", "-nographics", "-server", "-port", "7777" `
            -PassThru

        Start-Sleep -Seconds 2

        Write-Host "3. Launching Game Client..." -ForegroundColor Green
        Start-Process -FilePath $clientExe

        Write-Host "`nAll services active!" -ForegroundColor Cyan
        Write-Host "- Backend API:      http://localhost:5000"
        Write-Host "- Dedicated Server: UDP port 7777"
        Write-Host "- Visual Client:    Running"
        Write-Host "`nTo shut down background services later, run: .\play_game.ps1 -Stop" -ForegroundColor Yellow
    }
}
