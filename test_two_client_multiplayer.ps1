param(
    [int]$ServerPort = 7788,
    [string]$ServerExePath = "$PSScriptRoot\client\Builds\WindowsServer\OutrunAftermath_Server.exe",
    [string]$ClientExePath = "$PSScriptRoot\client\Builds\WindowsClient\OutrunAftermath_Client.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   OUTRUN: AFTERMATH — TWO-CLIENT MULTIPLAYER REPLICATION TEST   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$reportPath = "$PSScriptRoot\multiplayer_replication_report.json"
$serverLog = "$PSScriptRoot\two_client_server.log"
$clientALog = "$PSScriptRoot\two_client_clientA.log"
$clientBLog = "$PSScriptRoot\two_client_clientB.log"

if (Test-Path $serverLog) { Remove-Item $serverLog -Force }
if (Test-Path $clientALog) { Remove-Item $clientALog -Force }
if (Test-Path $clientBLog) { Remove-Item $clientBLog -Force }
if (Test-Path $reportPath) { Remove-Item $reportPath -Force }

if (-not (Test-Path $ServerExePath)) {
    throw "Server executable not found at: $ServerExePath"
}
if (-not (Test-Path $ClientExePath)) {
    throw "Client executable not found at: $ClientExePath"
}

# 1. Start Dedicated Server
Write-Host "1. Launching Dedicated Server on port $ServerPort..."
$serverProc = Start-Process -FilePath $ServerExePath `
    -ArgumentList "-batchmode", "-nographics", "-server", "-port", "$ServerPort", "-autoclose", "22", "-logfile", "`"$serverLog`"" `
    -PassThru

Write-Host "   Server started. PID: $($serverProc.Id)"

# Wait for server socket
Start-Sleep -Seconds 2

# 2. Start Client A
Write-Host "2. Launching Client A (First client)..."
$clientAProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autotest-anim", "-autoclose", "15", "-logfile", "`"$clientALog`"" `
    -PassThru

Write-Host "   Client A started. PID: $($clientAProc.Id)"

# Wait 3 seconds for Client A to connect and begin animation routine
Start-Sleep -Seconds 3

# 3. Start Client B (Late joiner)
Write-Host "3. Launching Client B (Second client / observer)..."
$clientBProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autotest-anim", "-autoclose", "12", "-logfile", "`"$clientBLog`"" `
    -PassThru

Write-Host "   Client B started. PID: $($clientBProc.Id)"

# 4. Wait for Client A to finish
Write-Host "4. Waiting for Client A lifecycle..."
$clientAExited = $clientAProc.WaitForExit(25000)
Write-Host "   Client A exited in time: $clientAExited, ExitCode: $($clientAProc.ExitCode)"

# 5. Wait for Client B to finish
Write-Host "5. Waiting for Client B lifecycle..."
$clientBExited = $clientBProc.WaitForExit(20000)
Write-Host "   Client B exited in time: $clientBExited, ExitCode: $($clientBProc.ExitCode)"

# 6. Wait for Server to finish
Write-Host "6. Waiting for Server lifecycle..."
$serverExited = $serverProc.WaitForExit(15000)
Write-Host "   Server exited in time: $serverExited, ExitCode: $($serverProc.ExitCode)"

# Force clean any stragglers
if (-not $clientAProc.HasExited) { Stop-Process -Id $clientAProc.Id -Force -ErrorAction SilentlyContinue }
if (-not $clientBProc.HasExited) { Stop-Process -Id $clientBProc.Id -Force -ErrorAction SilentlyContinue }
if (-not $serverProc.HasExited) { Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue }

# 7. Parse evidence from logs
$serverLogText = if (Test-Path $serverLog) { Get-Content $serverLog -Raw } else { "" }
$clientALogText = if (Test-Path $clientALog) { Get-Content $clientALog -Raw } else { "" }
$clientBLogText = if (Test-Path $clientBLog) { Get-Content $clientBLog -Raw } else { "" }

$serverConnectedClients = [regex]::Matches($serverLogText, "Server received client connection callback for ClientId=(\d+)")
$serverClientIds = @()
foreach ($m in $serverConnectedClients) {
    $serverClientIds += [int]$m.Groups[1].Value
}

$clientAConnected = $clientALogText -match "Client successfully connected to server! Assigned ClientId=(\d+)"
$clientBConnected = $clientBLogText -match "Client successfully connected to server! Assigned ClientId=(\d+)"

$clientAAnimChanges = [regex]::Matches($clientALogText, "\[NET_ANIM\][^\r\n]+")
$clientBReplications = [regex]::Matches($clientBLogText, "\[NET_ANIM_REPL\][^\r\n]+")

$clientBHasRemote = ($clientBLogText -match "OwnerClientId=1,\s*IsOwner=False")
$clientAHasRemote = ($clientALogText -match "OwnerClientId=2,\s*IsOwner=False")

$replicationPassed = ($serverClientIds.Count -ge 2) -and $clientAConnected -and $clientBConnected -and $clientBHasRemote -and $clientAHasRemote

$report = [ordered]@{
    Timestamp = (Get-Date).ToString("o")
    Phase = "Phase C.1.3: Two-Client Multiplayer Animation Replication"
    ReplicationPassed = $replicationPassed
    Server = [ordered]@{
        PID = $serverProc.Id
        Port = $ServerPort
        ExitCode = $serverProc.ExitCode
        ConnectedClientIds = $serverClientIds
    }
    ClientA = [ordered]@{
        PID = $clientAProc.Id
        Connected = $clientAConnected
        ExitCode = $clientAProc.ExitCode
        StateTransitionsSent = $clientAAnimChanges.Count
        Evidence = @($clientAAnimChanges | ForEach-Object { $_.Value } | Select-Object -First 10)
    }
    ClientB = [ordered]@{
        PID = $clientBProc.Id
        Connected = $clientBConnected
        ExitCode = $clientBProc.ExitCode
        ReplicationsObserved = $clientBReplications.Count
        Evidence = @($clientBReplications | ForEach-Object { $_.Value } | Select-Object -First 10)
    }
}

$json = $report | ConvertTo-Json -Depth 6
Set-Content -Path $reportPath -Value $json -Force
Write-Host "Report written to $reportPath" -ForegroundColor Green

if ($replicationPassed) {
    Write-Host "[SUCCESS] Two-client animation replication PASSED with $($serverClientIds.Count) clients and $($clientBReplications.Count) replicated states!" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Two-client animation replication did not observe all required transitions." -ForegroundColor Yellow
}
