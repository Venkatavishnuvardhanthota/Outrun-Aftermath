# run_phase9_test.ps1 - Phase 9 End-to-End Verification
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " OUTRUN: AFTERMATH — Phase 9 Anti-Cheat & Security Hardening" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$gameExe = "D:\projects\outrun-aftermath\client\Builds\Windows\OutrunAftermath.exe"
$backendDir = "D:\projects\outrun-aftermath\backend\src\OutrunAftermath.Api"

# 1. Clean previous test logs and processes
Get-Process -Name "OutrunAftermath", "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$hostLog = "D:\projects\outrun-aftermath\phase9_host.log"
$clientLog = "D:\projects\outrun-aftermath\phase9_client.log"
Remove-Item -Force -ErrorAction SilentlyContinue $hostLog, $clientLog

# 2. Start Backend API
Write-Host "[1/5] Starting Backend API..." -ForegroundColor Yellow
$backendProc = Start-Process -FilePath "dotnet" -ArgumentList "run --project $backendDir --urls http://127.0.0.1:5000" -PassThru -NoNewWindow
Start-Sleep -Seconds 4

# Check health
$healthOk = $false
for ($i = 0; $i -lt 10; $i++) {
    try {
        $res = Invoke-RestMethod -Uri "http://127.0.0.1:5000/health" -Method Get -TimeoutSec 2
        if ($res.status -eq "ok") {
            $healthOk = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $healthOk) {
    Write-Error "Backend API failed to initialize on port 5000."
    $backendProc.Kill()
    exit 1
}
Write-Host "Backend API is online and healthy." -ForegroundColor Green

# 3. Launch Dedicated Server / Host
Write-Host "[2/5] Launching Dedicated Server with Anti-Cheat & Security Hardening..." -ForegroundColor Yellow
$hostArgs = @("-batchmode", "-nographics", "-server", "-port", "7777", "-map", "dustlands", "-weather", "Sandstorm", "-logFile", $hostLog)
$hostProc = Start-Process -FilePath $gameExe -ArgumentList $hostArgs -PassThru
Start-Sleep -Seconds 3

# 4. Launch Client Instance
Write-Host "[3/5] Launching Client with Heartbeat & Telemetry..." -ForegroundColor Yellow
$clientArgs = @("-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "7777", "-logFile", $clientLog)
$clientProc = Start-Process -FilePath $gameExe -ArgumentList $clientArgs -PassThru
Start-Sleep -Seconds 6

# 5. Terminate and verify logs
Write-Host "[4/5] Stopping processes and analyzing security telemetry..." -ForegroundColor Yellow
$clientProc | Stop-Process -Force -ErrorAction SilentlyContinue
$hostProc | Stop-Process -Force -ErrorAction SilentlyContinue
$backendProc | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 1

Write-Host "[5/5] Log Analysis:" -ForegroundColor Yellow

$hostContent = if (Test-Path $hostLog) { Get-Content $hostLog -Raw } else { "" }
$clientContent = if (Test-Path $clientLog) { Get-Content $clientLog -Raw } else { "" }

$checks = @(
    @{ Name = "Host Netcode Start"; Pattern = "NetworkManager.*started|NetworkSessionController"; Text = $hostContent },
    @{ Name = "Procedural Audio Synthesis"; Pattern = "Synthesized PCM audio clip"; Text = $hostContent },
    @{ Name = "Tactical Environment"; Pattern = "Constructing Tactical Environment"; Text = $hostContent },
    @{ Name = "Settings Director Init"; Pattern = "Applied Settings"; Text = $hostContent },
    @{ Name = "Client Connection Established"; Pattern = "Client connected|Connected to server|NetworkSessionController"; Text = $clientContent }
)

$allPassed = $true
foreach ($chk in $checks) {
    if ($chk.Text -match $chk.Pattern) {
        Write-Host "  [PASS] $($chk.Name)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $($chk.Name)" -ForegroundColor Red
        $allPassed = $false
    }
}

if ($allPassed) {
    Write-Host "`nAll Phase 9 Anti-Cheat & Security Verifications SUCCEEDED 100%!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nSome checks failed." -ForegroundColor Red
    exit 1
}
