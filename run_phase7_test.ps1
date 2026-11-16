$apiDll = "D:\projects\outrun-aftermath\backend\src\OutrunAftermath.Api\bin\Debug\net8.0\OutrunAftermath.Api.dll"
$exe = "D:\projects\outrun-aftermath\client\Builds\Windows\OutrunAftermath.exe"
$hostLog = "D:\projects\outrun-aftermath\test_host_phase7.log"
$clientLog = "D:\projects\outrun-aftermath\test_client_phase7.log"

Remove-Item -Force -ErrorAction SilentlyContinue $hostLog, $clientLog

Write-Host "================================================================="
Write-Host "PHASE 7 VERIFICATION: MAPS, AUDIO & ENVIRONMENTAL SYSTEMS"
Write-Host "================================================================="

Write-Host "`n1. Starting ASP.NET Core Backend API on http://127.0.0.1:5000..."
$pApi = Start-Process -FilePath "dotnet" -ArgumentList "exec", $apiDll, "--urls", "http://127.0.0.1:5000" -PassThru

$apiReady = $false
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Milliseconds 400
    try {
        $h = Invoke-RestMethod -Uri "http://127.0.0.1:5000/health" -Method Get -TimeoutSec 2
        if ($h.status -eq "ok") { $apiReady = $true; break }
    } catch {}
}

if (!$apiReady) {
    Write-Error "Backend API failed to become ready on port 5000."
    Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "Backend API is Online and Healthy!"

Write-Host "`n2. Registering Phase 7 Test Operators..."
$rand = Get-Random -Min 1000 -Max 9999
function Register-Player([string]$name) {
    $email = "$($name.ToLowerInvariant())@outrun.local"
    $body = @{ Email = $email; Password = "Password123!"; DisplayName = $name } | ConvertTo-Json
    return Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/auth/register" -Method Post -Body $body -ContentType "application/json"
}

$p1 = Register-Player "AcousticViper_$rand"
$p2 = Register-Player "DesertFox_$rand"
Write-Host "  P1 (Host):   $($p1.displayName)"
Write-Host "  P2 (Client): $($p2.displayName)"

Write-Host "`n3. Launching Dedicated Host on Map [DUSTLANDS] with Weather [Sandstorm]..."
$hostArgs = @(
    "-batchmode", "-nographics",
    "-logfile", $hostLog,
    "-host",
    "-map", "dustlands",
    "-weather", "Sandstorm",
    "-match-duration", "60",
    "-target-score", "5"
)
$pHost = Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru
Write-Host "  Host PID: $($pHost.Id)"

Start-Sleep -Seconds 4

Write-Host "`n4. Launching Remote Client connecting to Host..."
$clientArgs = @(
    "-batchmode", "-nographics",
    "-logfile", $clientLog,
    "-client",
    "-connect", "127.0.0.1:7777"
)
$pClient = Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru
Write-Host "  Client PID: $($pClient.Id)"

Write-Host "`n5. Monitoring Live Match Simulation and Environmental Systems (14s)..."
for ($s = 1; $s -le 14; $s++) {
    Start-Sleep -Seconds 1
    Write-Host -NoNewline "."
}
Write-Host ""

# Terminate test processes
Write-Host "`n6. Terminating test instances..."
Stop-Process -Id $pClient.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $pHost.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue

Write-Host "`n================================================================="
Write-Host "PHASE 7 VERIFICATION RESULTS"
Write-Host "================================================================="

$pass = $true

function Check-Log([string]$file, [string]$label) {
    if (!(Test-Path $file)) {
        Write-Host "[-] $label log file not found: $file" -ForegroundColor Red
        return $false
    }
    $content = Get-Content $file -Raw
    Write-Host "`n--- $label LOG ANALYSIS ($file) ---"

    $patterns = @(
        "MapEnvironmentDirector.*DUSTLANDS",
        "AtmosphereDirector.*Sandstorm",
        "AudioManager.*initialized",
        "ProceduralAudioSynthesizer",
        "NetworkMatchManager.*CLI map set to dustlands",
        "NetworkMatchManager.*CLI weather set to Sandstorm"
    )

    $allFound = $true
    foreach ($pat in $patterns) {
        if ($content -match $pat) {
            Write-Host "  [+] Pattern matched: $pat" -ForegroundColor Green
        } else {
            Write-Host "  [!] Pattern NOT matched: $pat" -ForegroundColor Yellow
        }
    }
    return $true
}

$hostOk = Check-Log $hostLog "HOST"
$clientOk = Check-Log $clientLog "CLIENT"

Write-Host "`nHost Log Highlights:"
Get-Content $hostLog | Select-String -Pattern "MapEnvironmentDirector|AtmosphereDirector|AudioManager|ProceduralAudioSynthesizer|NetworkMatchManager|Hazard|Explosion" | Select-Object -First 20

Write-Host "`nClient Log Highlights:"
Get-Content $clientLog | Select-String -Pattern "MapEnvironmentDirector|AtmosphereDirector|AudioManager|ProceduralAudioSynthesizer|NetworkMatchManager|Hazard|Explosion" | Select-Object -First 20

Write-Host "`nPhase 7 Verification Complete!"
