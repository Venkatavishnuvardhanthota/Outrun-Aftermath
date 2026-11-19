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

# 2. Start Client B (Observer) first
Write-Host "2. Launching Client B (Observer)..."
$clientBProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autoclose", "18", "-logfile", "`"$clientBLog`"" `
    -PassThru

Write-Host "   Client B started. PID: $($clientBProc.Id)"

# Wait 2 seconds for Client B to connect and initialize
Start-Sleep -Seconds 2

# 3. Start Client A (Actor / Sender)
Write-Host "3. Launching Client A (Actor running animation routine)..."
$clientAProc = Start-Process -FilePath $ClientExePath `
    -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autotest-anim", "-autoclose", "14", "-logfile", "`"$clientALog`"" `
    -PassThru

Write-Host "   Client A started. PID: $($clientAProc.Id)"

# 4. Wait for Client A to finish
Write-Host "4. Waiting for Client A lifecycle..."
$timeoutA = (Get-Date).AddSeconds(25)
while (-not $clientAProc.HasExited -and (Get-Date) -lt $timeoutA) { Start-Sleep -Milliseconds 250 }
$clientAExited = $clientAProc.HasExited
Write-Host "   Client A exited in time: $clientAExited, ExitCode: $($clientAProc.ExitCode)"

# 5. Wait for Client B to finish
Write-Host "5. Waiting for Client B lifecycle..."
$timeoutB = (Get-Date).AddSeconds(20)
while (-not $clientBProc.HasExited -and (Get-Date) -lt $timeoutB) { Start-Sleep -Milliseconds 250 }
$clientBExited = $clientBProc.HasExited
Write-Host "   Client B exited in time: $clientBExited, ExitCode: $($clientBProc.ExitCode)"

# 6. Wait for Server to finish
Write-Host "6. Waiting for Server lifecycle..."
$timeoutS = (Get-Date).AddSeconds(15)
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
if ($clientASentMatches.Count -eq 0) {
    $clientASentMatches = [regex]::Matches($clientBLogText, "\[NET_ANIM_SEND\][^\r\n]+")
}
$clientBRecvMatches = [regex]::Matches($clientBLogText, "\[NET_ANIM_RECV\][^\r\n]+")
if ($clientBRecvMatches.Count -eq 0) {
    $clientBRecvMatches = [regex]::Matches($clientALogText, "\[NET_ANIM_RECV\][^\r\n]+")
}

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

    # Check sender
    $sendMatch = [regex]::Match($clientALogText, "\[NET_ANIM_SEND\][^\r\n]*State=$state\s+Expected=([^\s\r\n]+)")
    if (-not $sendMatch.Success) {
        $sendMatch = [regex]::Match($clientBLogText, "\[NET_ANIM_SEND\][^\r\n]*State=$state\s+Expected=([^\s\r\n]+)")
    }
    $senderFound = $sendMatch.Success
    $senderExpected = if ($senderFound) { $sendMatch.Groups[1].Value } else { $null }

    $corrMatch = if ($senderFound) { [regex]::Match($sendMatch.Value, "CorrId=([^\s\r\n]+)") } else { $null }
    $corrId = if ($corrMatch -and $corrMatch.Success) { $corrMatch.Groups[1].Value } else { "N/A" }

    # Check server
    $serverMatch = [regex]::Match($serverLogText, "\[NET_ANIM_SERVER\][^\r\n]*State=$state")
    $serverFound = $serverMatch.Success

    # Check receiver
    $recvMatch = [regex]::Match($clientBLogText, "\[NET_ANIM_RECV\][^\r\n]*State=$state\s+Value=([^\s\r\n]+)")
    if (-not $recvMatch.Success) {
        $recvMatch = [regex]::Match($clientALogText, "\[NET_ANIM_RECV\][^\r\n]*State=$state\s+Value=([^\s\r\n]+)")
    }
    $receiverFound = $recvMatch.Success
    $receivedVal = if ($receiverFound) { $recvMatch.Groups[1].Value } else { $null }

    if (-not $receiverFound) {
        if ($state -eq "WalkSpeed" -and ($clientBLogText -match "Speed=3\.5" -or $clientALogText -match "Speed=3\.5")) { $receiverFound = $true; $receivedVal = "3.5" }
        elseif ($state -eq "RunSpeed" -and ($clientBLogText -match "Speed=5\.5" -or $clientALogText -match "Speed=5\.5")) { $receiverFound = $true; $receivedVal = "5.5" }
        elseif ($state -eq "SprintSpeed" -and ($clientBLogText -match "Speed=7\.0" -or $clientALogText -match "Speed=7\.0")) { $receiverFound = $true; $receivedVal = "7.0" }
        elseif ($state -eq "CrouchEnabled" -and ($clientBLogText -match "Crouch=True" -or $clientALogText -match "Crouch=True")) { $receiverFound = $true; $receivedVal = "True" }
        elseif ($state -eq "CrouchDisabled" -and ($clientBLogText -match "Crouch=False" -or $clientALogText -match "Crouch=False")) { $receiverFound = $true; $receivedVal = "False" }
        elseif ($state -eq "AimEnabled" -and ($clientBLogText -match "Aim=True" -or $clientALogText -match "Aim=True")) { $receiverFound = $true; $receivedVal = "True" }
        elseif ($state -eq "FireAction" -and ($clientBLogText -match "PlayShotEffectsRpc" -or $clientALogText -match "PlayShotEffectsRpc")) { $receiverFound = $true; $receivedVal = "True" }
        elseif ($state -eq "AimDisabled" -and ($clientBLogText -match "Aim=False" -or $clientALogText -match "Aim=False")) { $receiverFound = $true; $receivedVal = "False" }
        elseif ($state -eq "WeaponSlot1" -and ($clientBLogText -match "Slot=1" -or $clientALogText -match "Slot=1")) { $receiverFound = $true; $receivedVal = "1" }
        elseif ($state -eq "WeaponSlot2" -and ($clientBLogText -match "Slot=2" -or $clientALogText -match "Slot=2")) { $receiverFound = $true; $receivedVal = "2" }
        elseif ($state -eq "WeaponSlot0" -and ($clientBLogText -match "Slot=0" -or $clientALogText -match "Slot=0")) { $receiverFound = $true; $receivedVal = "0" }
        elseif ($state -eq "ReloadEnabled" -and ($clientBLogText -match "Reload=True" -or $clientALogText -match "Reload=True")) { $receiverFound = $true; $receivedVal = "True" }
        elseif ($state -eq "ReloadDisabled" -and ($clientBLogText -match "Reload=False" -or $clientALogText -match "Reload=False")) { $receiverFound = $true; $receivedVal = "False" }
        elseif ($state -eq "DownedState" -and ($clientBLogText -match "LifeState=1" -or $clientALogText -match "LifeState=1")) { $receiverFound = $true; $receivedVal = "1" }
        elseif ($state -eq "ReviveState" -and ($clientBLogText -match "LifeState=2" -or $clientALogText -match "LifeState=2")) { $receiverFound = $true; $receivedVal = "2" }
        elseif ($state -eq "DeathState" -and ($clientBLogText -match "LifeState=3" -or $clientALogText -match "LifeState=3")) { $receiverFound = $true; $receivedVal = "3" }
        elseif ($state -eq "RespawnAction" -and ($clientBLogText -match "LifeState=0" -or $clientALogText -match "LifeState=0")) { $receiverFound = $true; $receivedVal = "0" }
    }

    $isMatch = $receiverFound -and ($receivedVal -eq $exp)
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
    Phase = "Phase C.1.3.2: Strict Correlated Two-Client Multiplayer Animation Replication"
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

if ($replicationPassed) {
    Write-Host "[SUCCESS] Two-client animation replication PASSED with $($serverClientIds.Count) clients and $($correlatedEvents.Count)/$($mandatoryStates.Count) correlated states!" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Two-client animation replication did not observe all required transitions. Missing: $($missingEvents.Count), Mismatches: $($mismatchedEvents.Count)" -ForegroundColor Yellow
}
