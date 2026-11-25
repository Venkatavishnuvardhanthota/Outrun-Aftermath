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
    -ArgumentList "-batchmode", "-nographics", "-server", "-port", "$ServerPort", "-autoclose", "30", "-logfile", "`"$serverLog`"" `
    -PassThru

Write-Host "   Server started. PID: $($serverProc.Id)"

# Wait for server socket
Start-Sleep -Seconds 2

# 2. Start Client B (Observer) first
Write-Host "2. Launching Client B (Observer)..."
$clientBProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autoclose", "26", "-logfile", "`"$clientBLog`"" `
    -PassThru

Write-Host "   Client B started. PID: $($clientBProc.Id)"

# Wait 2 seconds for Client B to connect and initialize
Start-Sleep -Seconds 2

# 3. Start Client A (Actor / Sender)
Write-Host "3. Launching Client A (Actor running animation routine)..."
$clientAProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autotest-anim", "-autoclose", "22", "-logfile", "`"$clientALog`"" `
    -PassThru

Write-Host "   Client A started. PID: $($clientAProc.Id)"

# 4. Wait for Client A to finish
Write-Host "4. Waiting for Client A lifecycle..."
$timeoutA = (Get-Date).AddSeconds(32)
while (-not $clientAProc.HasExited -and (Get-Date) -lt $timeoutA) { Start-Sleep -Milliseconds 250 }
$clientAExited = $clientAProc.HasExited
Write-Host "   Client A exited in time: $clientAExited, ExitCode: $($clientAProc.ExitCode)"

# 5. Wait for Client B to finish
Write-Host "5. Waiting for Client B lifecycle..."
$timeoutB = (Get-Date).AddSeconds(30)
while (-not $clientBProc.HasExited -and (Get-Date) -lt $timeoutB) { Start-Sleep -Milliseconds 250 }
$clientBExited = $clientBProc.HasExited
Write-Host "   Client B exited in time: $clientBExited, ExitCode: $($clientBProc.ExitCode)"

# 6. Wait for Server to finish
Write-Host "6. Waiting for Server lifecycle..."
$timeoutS = (Get-Date).AddSeconds(25)
while (-not $serverProc.HasExited -and (Get-Date) -lt $timeoutS) { Start-Sleep -Milliseconds 250 }
$serverExited = $serverProc.HasExited
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

$clientBHasRemote = ($clientBLogText -match "OwnerClientId=(\d+),\s*IsOwner=False")
$clientAHasRemote = ($clientALogText -match "OwnerClientId=(\d+),\s*IsOwner=False")

$clientASentMatches = [regex]::Matches($clientALogText, "\[NET_ANIM_SEND\][^\r\n]+")
$clientBRecvMatches = [regex]::Matches($clientBLogText, "\[NET_ANIM_RECV\][^\r\n]+")

# Correlate all 17 mandatory animation replication scenarios (Phase C.1.3.2)
$mandatoryStates = @(
    @{ State = "WalkSpeed"; Expected = "3.5" },
    @{ State = "RunSpeed"; Expected = "5.5" },
    @{ State = "SprintSpeed"; Expected = "7.0" },
    @{ State = "CrouchEnabled"; Expected = "True" },
    @{ State = "CrouchDisabled"; Expected = "False" },
    @{ State = "AimEnabled"; Expected = "True" },
    @{ State = "FireAction"; Expected = "True" },
    @{ State = "AimDisabled"; Expected = "False" },
    @{ State = "ReloadEnabled"; Expected = "True" },
    @{ State = "ReloadDisabled"; Expected = "False" },
    @{ State = "WeaponSlot0"; Expected = "0" },
    @{ State = "WeaponSlot1"; Expected = "1" },
    @{ State = "WeaponSlot2"; Expected = "2" },
    @{ State = "DownedState"; Expected = "1" },
    @{ State = "ReviveState"; Expected = "2" },
    @{ State = "DeathState"; Expected = "3" },
    @{ State = "RespawnAction"; Expected = "0" }
)

$correlatedEvents = @()
$missingEvents = @()
$mismatchedEvents = @()

foreach ($ms in $mandatoryStates) {
    $state = $ms.State
    $exp = $ms.Expected

    # Strict Sender Check: Client A (Actor) MUST be the sender
    $sendMatch = [regex]::Match($clientALogText, "\[NET_ANIM_SEND\][^\r\n]*State=$state\s+Expected=([^\s\r\n]+)")
    $senderFound = $sendMatch.Success
    $senderExpected = if ($senderFound) { $sendMatch.Groups[1].Value } else { $null }

    $corrMatch = if ($senderFound) { [regex]::Match($sendMatch.Value, "CorrId=([^\s\r\n]+)") } else { $null }
    $corrId = if ($corrMatch -and $corrMatch.Success) { $corrMatch.Groups[1].Value } else { "N/A" }

    # Strict Server Check: Relay MUST process event with matching CorrId
    $serverMatch = if ($corrId -ne "N/A") {
        [regex]::Match($serverLogText, "\[NET_ANIM_SERVER\][^\r\n]*CorrId=$corrId[^\r\n]*State=$state")
    } else {
        [regex]::Match($serverLogText, "\[NET_ANIM_SERVER\][^\r\n]*State=$state")
    }
    $serverFound = $serverMatch.Success

    # Strict Receiver Check: Client B (Observer) MUST be the receiver
    $recvMatch = if ($corrId -ne "N/A") {
        [regex]::Match($clientBLogText, "\[NET_ANIM_RECV\][^\r\n]*CorrId=$corrId[^\r\n]*State=$state\s+Value=([^\s\r\n]+)")
    } else {
        [regex]::Match($clientBLogText, "\[NET_ANIM_RECV\][^\r\n]*State=$state\s+Value=([^\s\r\n]+)")
    }
    $receiverFound = $recvMatch.Success
    $receivedVal = if ($receiverFound) { $recvMatch.Groups[1].Value } else { $null }

    # Strict matching: requires exact sender (A), server, and receiver (B) correlation without cross-client fallbacks
    $isMatch = $senderFound -and $serverFound -and $receiverFound -and ($receivedVal -eq $exp)
    if (-not $receiverFound) {
        $missingEvents += $state
    } elseif (-not $isMatch) {
        $mismatchedEvents += @{ State = $state; Expected = $exp; Actual = $receivedVal }
    }

    $correlatedEvents += [ordered]@{
        CorrelationId = $corrId
        State = $state
        Expected = $exp
        SenderFound = $senderFound
        ServerObserved = $serverFound
        ReceiverObserved = $receiverFound
        ActualReceivedValue = $receivedVal
        Correlated = $isMatch
    }
}

$allMandatoryCorrelated = ($missingEvents.Count -eq 0) -and ($mismatchedEvents.Count -eq 0)
$cleanExit = ($serverProc.ExitCode -eq 0) -and ($clientAProc.ExitCode -eq 0) -and ($clientBProc.ExitCode -eq 0)

$replicationPassed = ($serverClientIds.Count -ge 2) -and $clientAConnected -and $clientBConnected -and $clientBHasRemote -and $clientAHasRemote -and $allMandatoryCorrelated -and $cleanExit

$report = [ordered]@{
    Timestamp = (Get-Date).ToString("o")
    Phase = "Phase C.1.3.7: Strict Correlated Two-Client Multiplayer Animation Replication"
    ReplicationPassed = $replicationPassed
    AllMandatoryStatesCorrelated = $allMandatoryCorrelated
    TotalMandatoryScenarios = $mandatoryStates.Count
    CorrelatedScenariosCount = ($correlatedEvents | Where-Object { $_.Correlated }).Count
    MissingScenarios = $missingEvents
    MismatchedScenarios = $mismatchedEvents
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
        StateTransitionsSent = $clientASentMatches.Count
        Evidence = @($clientASentMatches | ForEach-Object { $_.Value })
    }
    ClientB = [ordered]@{
        PID = $clientBProc.Id
        Connected = $clientBConnected
        ExitCode = $clientBProc.ExitCode
        ReplicationsObserved = $clientBRecvMatches.Count
        Evidence = @($clientBRecvMatches | ForEach-Object { $_.Value })
    }
    CorrelatedEvents = $correlatedEvents
}

$json = $report | ConvertTo-Json -Depth 6
Set-Content -Path $reportPath -Value $json -Force
Write-Host "Report written to $reportPath" -ForegroundColor Green

# Save copies to Phase C.1.3.7.1 docs and test-results directories
$phaseDocDir = "$PSScriptRoot\docs\phase-c1.3.7.1"
$phaseResultsDir = "$phaseDocDir\test-results"
if (-not (Test-Path $phaseDocDir)) { New-Item -ItemType Directory -Path $phaseDocDir -Force | Out-Null }
if (-not (Test-Path $phaseResultsDir)) { New-Item -ItemType Directory -Path $phaseResultsDir -Force | Out-Null }
Set-Content -Path "$phaseDocDir\multiplayer-correlation-report.json" -Value $json -Force
Set-Content -Path "$phaseResultsDir\multiplayer_replication_report.json" -Value $json -Force
Write-Host "Replication report archived to $phaseDocDir\multiplayer-correlation-report.json" -ForegroundColor Green

if ($replicationPassed) {
    Write-Host "[SUCCESS] Two-client animation replication PASSED with $($serverClientIds.Count) clients and $($correlatedEvents.Count)/$($mandatoryStates.Count) correlated states!" -ForegroundColor Green
    exit 0
} else {
    Write-Error "[FAIL] Two-client animation replication failed! Missing: $($missingEvents.Count), Mismatches: $($mismatchedEvents.Count)"
    exit 1
}
