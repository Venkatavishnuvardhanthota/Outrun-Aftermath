<#
.SYNOPSIS
    Comprehensive End-to-End Production Verification Suite for OUTRUN: AFTERMATH.
.DESCRIPTION
    Validates the entire game ecosystem with strict integrity:
    1. ASP.NET Core 8 Web API: Health, JWT auth, player registration, room management.
    2. Authoritative Anti-Cheat & Cryptographic Match Signatures: HMAC-SHA256 validation & tamper rejection.
    3. Dedicated Headless Server & Client Multiplayer Netcode: Real network handshake & packet exchange over UDP.
       - Enforces mandatory binary checks (no silent skips).
       - Enforces UDP socket ownership by the actual server process ID.
       - Enforces two-sided complete RPC roundtrip evidence.
       - Verifies process lifecycle timeouts and graceful termination.
    4. Unit & Integration Test Suites: All simulation and API tests dynamically executed and parsed.
#>

param(
    [int]$ApiPort = 5099,
    [int]$ServerPort = 7799,
    [string]$ServerExePath = "$PSScriptRoot\client\Builds\WindowsServer\OutrunAftermath_Server.exe",
    [string]$ClientExePath = "$PSScriptRoot\client\Builds\WindowsClient\OutrunAftermath_Client.exe",
    [string]$UniversalExePath = "$PSScriptRoot\client\Builds\Windows\OutrunAftermath.exe"
)

$ErrorActionPreference = "Stop"

# Initialize Console Transcript
$consoleLogPath = "$PSScriptRoot\e2e_production_test_console.log"
try {
    if (Test-Path $consoleLogPath) { Remove-Item $consoleLogPath -Force }
    Start-Transcript -Path $consoleLogPath -Force
} catch {
    Write-Warning "Could not initialize Start-Transcript: $_"
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   OUTRUN: AFTERMATH — END-TO-END PRODUCTION VERIFICATION SUITE   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$apiProcess = $null
$serverProc = $null
$clientProc = $null
$passCount = 0
$failCount = 0
$skippedCount = 0

$stage1Results = [ordered]@{}
$stage2Results = [ordered]@{}
$netcodeStages = [ordered]@{}
$stage4Results = [ordered]@{}

function Report-Pass($name) {
    Write-Host "[PASS] $name" -ForegroundColor Green
    $script:passCount++
}

function Report-Fail($name, $reason) {
    Write-Host "[FAIL] $name - $reason" -ForegroundColor Red
    $script:failCount++
}

function Report-Skip($name, $reason) {
    Write-Host "[SKIPPED] $name - $reason" -ForegroundColor Yellow
    $script:skippedCount++
}

try {
    # -------------------------------------------------------------
    # STAGE 1: Backend API Startup & Authentication Testing
    # -------------------------------------------------------------
    Write-Host "`n--- [STAGE 1] Backend API & Database Verification ---" -ForegroundColor Yellow
    $apiUrl = "http://127.0.0.1:$ApiPort"
    $apiProject = "$PSScriptRoot\backend\src\OutrunAftermath.Api"

    Write-Host "Starting ASP.NET Core API on $apiUrl..."
    $apiStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $apiStartInfo.FileName = "dotnet"
    $apiStartInfo.Arguments = "run --project `"$apiProject`" --urls $apiUrl --no-build"
    $apiStartInfo.UseShellExecute = $false
    $apiStartInfo.RedirectStandardOutput = $true
    $apiStartInfo.RedirectStandardError = $true
    $apiStartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"
    $apiStartInfo.EnvironmentVariables["Matchmaking__ServerKey"] = "DEV_MATCH_SERVER_KEY_CHANGE"

    $apiProcess = [System.Diagnostics.Process]::Start($apiStartInfo)

    # Wait for API to become healthy
    $healthy = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        try {
            $healthRes = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get -TimeoutSec 2
            if ($healthRes.status -eq "ok") {
                $healthy = $true
                break
            }
        } catch {}
    }

    if ($healthy) {
        Report-Pass "API Health Check (/health returned status: ok)"
        $stage1Results["HealthCheck"] = @{ Status = "PASS"; Endpoint = "/health"; Response = $healthRes.status }
    } else {
        Report-Fail "API Health Check" "API failed to respond on $apiUrl within 15 seconds."
        $stage1Results["HealthCheck"] = @{ Status = "FAIL"; Endpoint = "/health"; Response = "timeout" }
        throw "API failed to respond on $apiUrl within 15 seconds."
    }

    # Register Pilot
    $suffix = [guid]::NewGuid().ToString('N').Substring(0,8)
    $email = "e2e_pilot_$suffix@outrun.test"
    $password = "correcthorse1"
    $displayName = "Alpha_$suffix"
    $regBody = @{ email = $email; password = $password; displayName = $displayName } | ConvertTo-Json
    $regRes = Invoke-RestMethod -Uri "$apiUrl/api/auth/register" -Method Post -Body $regBody -ContentType "application/json"

    if ($regRes.accessToken -and $regRes.userId) {
        Report-Pass "User Registration & JWT Issuance ($displayName / $($regRes.userId))"
        $stage1Results["Registration"] = @{ Status = "PASS"; UserId = $regRes.userId; Callsign = $displayName }
        $stage1Results["JwtIssuance"] = @{ Status = "PASS"; TokenLength = $regRes.accessToken.Length }
    } else {
        Report-Fail "User Registration" "Missing accessToken or userId"
        $stage1Results["Registration"] = @{ Status = "FAIL"; Details = "Missing accessToken or userId" }
        $stage1Results["JwtIssuance"] = @{ Status = "FAIL"; Details = "Missing accessToken" }
    }

    $jwt = $regRes.accessToken
    $headers = @{ "Authorization" = "Bearer $jwt" }

    # Query Profile
    $meRes = Invoke-RestMethod -Uri "$apiUrl/api/me" -Method Get -Headers $headers
    if ($meRes.displayName -eq $displayName) {
        Report-Pass "Profile Retrieval (/api/me returned correct callsign: $($meRes.displayName))"
        $stage1Results["ProfileRetrieval"] = @{ Status = "PASS"; Callsign = $meRes.displayName; UserId = $meRes.userId }
    } else {
        Report-Fail "Profile Retrieval" "Callsign mismatch: $($meRes.displayName) vs $displayName"
        $stage1Results["ProfileRetrieval"] = @{ Status = "FAIL"; Expected = $displayName; Actual = $meRes.displayName }
    }

    # Create Room
    $roomBody = @{ mode = "GroundOperations"; matchType = "Extraction"; mapId = "deadcity" } | ConvertTo-Json
    $roomRes = Invoke-RestMethod -Uri "$apiUrl/api/rooms" -Method Post -Headers $headers -Body $roomBody -ContentType "application/json"
    if ($roomRes.code -and $roomRes.code.Length -eq 6) {
        Report-Pass "Lobby Room Creation (Room Code: $($roomRes.code), Map: $($roomRes.mapId))"
        $stage1Results["RoomCreation"] = @{ Status = "PASS"; RoomCode = $roomRes.code; MapId = $roomRes.mapId }
    } else {
        Report-Fail "Lobby Room Creation" "Invalid room code"
        $stage1Results["RoomCreation"] = @{ Status = "FAIL"; Details = "Invalid room code" }
    }

    # -------------------------------------------------------------
    # STAGE 2: Authoritative Match Security & HMAC Signing
    # -------------------------------------------------------------
    Write-Host "`n--- [STAGE 2] Authoritative Match Security & HMAC Signing ---" -ForegroundColor Yellow
    $matchId = [guid]::NewGuid().ToString()
    $serverKey = "DEV_MATCH_SERVER_KEY_CHANGE"
    $winnerFaction = "Rogue"
    $totalKills = 7
    $duration = 180

    # Generate HMAC-SHA256 signature using .NET Cryptography
    $payload = "$matchId`:$winnerFaction`:$totalKills`:$duration"
    $keyBytes = [System.Text.Encoding]::UTF8.GetBytes($serverKey)
    $dataBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $keyBytes
    $hashBytes = $hmac.ComputeHash($dataBytes)
    $validSignature = [BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()

    # Submit valid match completion
    $matchBody = @{
        matchId = $matchId
        mode = "GroundOperations"
        matchType = "Extraction"
        mapId = "deadcity"
        startedAtUtc = ([DateTime]::UtcNow.AddSeconds(-$duration)).ToString("o")
        winnerFaction = $winnerFaction
        seed = 42
        players = @(
            @{
                userId = $regRes.userId
                faction = "Rogue"
                operatorId = "rex"
                kills = $totalKills
                revives = 2
                objectiveProgress = 3
                sideComplete = $true
            }
        )
    } | ConvertTo-Json -Depth 5

    $matchHeaders = @{
        "X-Match-Server-Key" = $serverKey
        "X-Match-Signature"  = $validSignature
    }

    $completeRes = Invoke-RestMethod -Uri "$apiUrl/internal/matches/complete" -Method Post -Headers $matchHeaders -Body $matchBody -ContentType "application/json"
    if ($completeRes.duplicate -eq $false) {
        Report-Pass "Authoritative Match Completion with Valid HMAC-SHA256 Signature"
        $stage2Results["HmacValidation"] = @{ Status = "PASS"; Signature = $validSignature; MatchId = $matchId }
    } else {
        Report-Fail "Match Completion" "Unexpected duplicate response"
        $stage2Results["HmacValidation"] = @{ Status = "FAIL"; Details = "Unexpected duplicate response" }
    }

    # Verify XP was awarded
    $updatedMe = Invoke-RestMethod -Uri "$apiUrl/api/me" -Method Get -Headers $headers
    if ($updatedMe.xp -gt 0 -and $updatedMe.wins -eq 1) {
        Report-Pass "Match XP & Win Stats Persistence (XP: $($updatedMe.xp), Wins: $($updatedMe.wins))"
        $stage2Results["StatsPersistence"] = @{ Status = "PASS"; XP = $updatedMe.xp; Wins = $updatedMe.wins }
    } else {
        Report-Fail "Stats Persistence" "XP or win count not incremented ($($updatedMe.xp), $($updatedMe.wins))"
        $stage2Results["StatsPersistence"] = @{ Status = "FAIL"; XP = $updatedMe.xp; Wins = $updatedMe.wins }
    }

    # Test Tampered Signature Rejection
    $tamperedHeaders = @{
        "X-Match-Server-Key" = $serverKey
        "X-Match-Signature"  = "0000000000000000000000000000000000000000000000000000000000000000"
    }
    $tamperRejected = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/internal/matches/complete" -Method Post -Headers $tamperedHeaders -Body $matchBody -ContentType "application/json"
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 400) {
            $tamperRejected = $true
        }
    }
    if ($tamperRejected) {
        Report-Pass "Anti-Cheat Rejection of Forged/Tampered Match Signature (400 Bad Request)"
        $stage2Results["TamperRejection"] = @{ Status = "PASS"; HttpStatusCode = 400; Defense = "Forged HMAC signature rejected" }
    } else {
        Report-Fail "Tamper Defense" "Failed to reject invalid signature"
        $stage2Results["TamperRejection"] = @{ Status = "FAIL"; Details = "Forged signature was not rejected with 400" }
    }

    # -------------------------------------------------------------
    # STAGE 3: Dedicated Headless Server & Client Netcode
    # -------------------------------------------------------------
    Write-Host "`n--- [STAGE 3] Dedicated Server & Client Netcode Lifecycle (12 Stages) ---" -ForegroundColor Yellow

    # Mandatory Executable Checks (Strict Separation - Universal Executables Prohibited)
    $srvTarget = $null
    if (Test-Path $ServerExePath) {
        $srvTarget = (Resolve-Path $ServerExePath).Path
        Report-Pass "Dedicated Server Executable Verified ($srvTarget)"
        $netcodeStages["ServerExecutableCheck"] = @{ 
            Status = "PASS"
            Path = $srvTarget
            BuildType = "DedicatedServer"
            Details = "Dedicated server binary verified at configured server path"
        }
    } else {
        Report-Fail "Dedicated Server Executable Missing" "Required dedicated server binary '$ServerExePath' not found. Universal executables are rejected."
        $netcodeStages["ServerExecutableCheck"] = @{ 
            Status = "FAIL"
            Path = $ServerExePath
            BuildType = "Missing"
            Details = "Dedicated server binary missing at configured path"
        }
    }

    $cltTarget = $null
    if (Test-Path $ClientExePath) {
        $cltTarget = (Resolve-Path $ClientExePath).Path
        Report-Pass "Standalone Game Client Executable Verified ($cltTarget)"
        $netcodeStages["ClientExecutableCheck"] = @{ 
            Status = "PASS"
            Path = $cltTarget
            BuildType = "StandaloneClient"
            Details = "Standalone client binary verified at configured client path"
        }
    } else {
        Report-Fail "Standalone Game Client Executable Missing" "Required client binary '$ClientExePath' not found. Universal executables are rejected."
        $netcodeStages["ClientExecutableCheck"] = @{ 
            Status = "FAIL"
            Path = $ClientExePath
            BuildType = "Missing"
            Details = "Standalone client binary missing at configured path"
        }
    }

    if (-not $srvTarget -or -not $cltTarget) {
        Report-Fail "Mandatory Binary Gate" "Cannot proceed with live multiplayer verification because dedicated server or standalone client binary is missing."
        throw "Multiplayer binaries missing on disk."
    }

    $serverLog = "$PSScriptRoot\e2e_server.log"
    $clientLog = "$PSScriptRoot\e2e_client.log"
    if (Test-Path $serverLog) { Remove-Item $serverLog -Force }
    if (Test-Path $clientLog) { Remove-Item $clientLog -Force }

    Write-Host "1. Spawning Dedicated Server ($srvTarget) on UDP port $ServerPort..."
    $serverProc = Start-Process -FilePath $srvTarget `
        -ArgumentList "-batchmode", "-nographics", "-server", "-port", "$ServerPort", "-autoclose", "20", "-logfile", "`"$serverLog`"" `
        -PassThru

    if ($serverProc -and -not $serverProc.HasExited) {
        Report-Pass "Stage 1: Dedicated Server process spawned (PID: $($serverProc.Id), Path: $srvTarget)"
        $netcodeStages["Stage01_ServerSpawn"] = @{ 
            Status = "PASS"
            Details = "Server process active"
            PID = $serverProc.Id
            Path = $srvTarget
        }
    } else {
        Report-Fail "Stage 1: Dedicated Server Process" "Server process failed to start"
        $netcodeStages["Stage01_ServerSpawn"] = @{ 
            Status = "FAIL"
            Details = "Process failed to start"
            PID = if ($serverProc) { $serverProc.Id } else { $null }
            Path = $srvTarget
        }
    }

    # Poll up to 10 seconds for server socket binding & verify UDP ownership by PID
    $socketBound = $false
    $boundPid = $null
    $boundEndpoint = $null
    $ownershipEvidence = $null

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500

        # Method A: Get-NetUDPEndpoint
        try {
            $endpoint = Get-NetUDPEndpoint -LocalPort $ServerPort -ErrorAction SilentlyContinue
            if ($endpoint) {
                $boundPid = $endpoint.OwningProcess
                $boundEndpoint = "$($endpoint.LocalAddress):$($endpoint.LocalPort)"
            }
        } catch {}

        # Method B: netstat fallback
        if (-not $boundPid) {
            $netstatLine = netstat -ano -p UDP | Select-String ":$ServerPort\s"
            if ($netstatLine) {
                $parts = ($netstatLine.Line.Trim() -split "\s+")
                if ($parts.Length -ge 4) {
                    $boundEndpoint = $parts[1]
                    $boundPid = [int]$parts[-1]
                }
            }
        }

        if ($boundPid) {
            if ($boundPid -eq $serverProc.Id) {
                $socketBound = $true
                $ownershipEvidence = "UDP endpoint $boundEndpoint owned by Server PID $boundPid"
                break
            } else {
                $ownershipEvidence = "Port :$ServerPort is owned by PID $boundPid, NOT Server PID $($serverProc.Id)"
                break
            }
        }
    }

    Write-Host "4. Spawning Game Client ($cltTarget) connecting to 127.0.0.1:$ServerPort..."
    $clientProc = Start-Process -FilePath $cltTarget `
        -ArgumentList "-batchmode", "-nographics", "-client", "-ip", "127.0.0.1", "-port", "$ServerPort", "-autotest-shoot", "-autoclose", "12", "-logfile", "`"$clientLog`"" `
        -PassThru

    if ($clientProc -and -not $clientProc.HasExited) {
        Report-Pass "Stage 4: Client process spawned (PID: $($clientProc.Id), Path: $cltTarget)"
        $netcodeStages["Stage04_ClientSpawn"] = @{ 
            Status = "PASS"
            Details = "Client process active"
            PID = $clientProc.Id
            Path = $cltTarget
        }
    } else {
        Report-Fail "Stage 4: Client Process" "Client process failed to start"
        $netcodeStages["Stage04_ClientSpawn"] = @{ 
            Status = "FAIL"
            Details = "Process failed to start"
            PID = if ($clientProc) { $clientProc.Id } else { $null }
            Path = $cltTarget
        }
    }

    # Wait for client to complete handshake, spawn, shoot RPC, and disconnect
    Write-Host "Waiting for client session lifecycle (timeout 30s)..."
    $clientExitedInTime = $clientProc.WaitForExit(30000)
    $clientExitCode = if ($clientProc.HasExited) { $clientProc.ExitCode } else { -1 }
    $clientCleanupType = if ($clientExitedInTime) { "Graceful" } else { "Forced" }

    if (-not $clientExitedInTime) {
        Write-Warning "Client process PID $($clientProc.Id) timed out after 30s. Terminating forcefully."
        Stop-Process -Id $clientProc.Id -Force -ErrorAction SilentlyContinue
    }

    # Allow server to log disconnection and auto-close
    Write-Host "Waiting for server shutdown (timeout 15s)..."
    $serverExitedInTime = $serverProc.WaitForExit(15000)
    $serverExitCode = if ($serverProc.HasExited) { $serverProc.ExitCode } else { -1 }
    $serverCleanupType = if ($serverExitedInTime) { "Graceful" } else { "Forced" }

    if (-not $serverExitedInTime) {
        Write-Warning "Server process PID $($serverProc.Id) timed out after 15s. Terminating forcefully."
        Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue
    }

    $serverTerminated = $serverProc.HasExited

    Start-Sleep -Milliseconds 500

    # Retrieve log outputs
    $serverLogContent = if (Test-Path $serverLog) { Get-Content $serverLog -Raw } else { "" }
    $clientLogContent = if (Test-Path $clientLog) { Get-Content $clientLog -Raw } else { "" }

    # Stage 2: NGO NetworkManager Initialized
    if ($serverLogContent -match "Server initialized NGO NetworkManager successfully") {
        Report-Pass "Stage 2: Server initialized NGO NetworkManager successfully"
        $netcodeStages["Stage02_NGOInit"] = @{ Status = "PASS"; Details = "Confirmed in server log" }
    } else {
        Report-Fail "Stage 2: Server NGO Init" "Initialization signature missing in server log"
        $netcodeStages["Stage02_NGOInit"] = @{ Status = "FAIL"; Details = "Signature missing in log" }
    }

    # Stage 3: UDP Socket Ownership
    if ($socketBound -and $boundPid -eq $serverProc.Id) {
        Report-Pass "Stage 3: Server actively listening on UDP socket :$ServerPort (Ownership verified: PID $boundPid, Endpoint $boundEndpoint)"
        $netcodeStages["Stage03_SocketListening"] = @{ 
            Status = "PASS"
            Details = "UDP socket bound and owned by dedicated server process"
            ServerPID = $serverProc.Id
            OwningPID = $boundPid
            Endpoint = $boundEndpoint
            Port = $ServerPort
            Evidence = $ownershipEvidence
        }
    } else {
        $reason = if (-not $boundPid) { "Socket :$ServerPort not bound within 10s timeout" } else { $ownershipEvidence }
        Report-Fail "Stage 3: UDP Socket Ownership" $reason
        $netcodeStages["Stage03_SocketListening"] = @{ 
            Status = "FAIL"
            Details = $reason
            ServerPID = $serverProc.Id
            OwningPID = $boundPid
            Port = $ServerPort
            Evidence = $ownershipEvidence
        }
    }

    # Stage 5: Client Connection Handshake
    $assignedId = $null
    if ($clientLogContent -match "Client successfully connected to server! Assigned ClientId=(\d+)") {
        $assignedId = [int]$Matches[1]
        Report-Pass "Stage 5: Client completed connection handshake (Assigned ClientId=$assignedId)"
        $netcodeStages["Stage05_ClientHandshake"] = @{ 
            Status = "PASS"
            AssignedClientId = $assignedId
            Details = "Handshake completed, Assigned ClientId=$assignedId" 
        }
    } else {
        Report-Fail "Stage 5: Client Connection Handshake" "Connection confirmation missing in client log"
        $netcodeStages["Stage05_ClientHandshake"] = @{ 
            Status = "FAIL"
            AssignedClientId = $null
            Details = "Confirmation missing in log" 
        }
    }

    # Stage 6: Server Connection Callback & Identity Correlation
    $srvClientId = $null
    if ($serverLogContent -match "Server received client connection callback for ClientId=(\d+)") {
        $srvClientId = [int]$Matches[1]
    }

    $stage6Match = ($assignedId -ne $null -and $srvClientId -ne $null -and $assignedId -eq $srvClientId)
    if ($stage6Match) {
        Report-Pass "Stage 6: Server connection callback verified with matched ClientId ($srvClientId == Expected $assignedId)"
        $netcodeStages["Stage06_ServerConnectionCallback"] = @{ 
            Status = "PASS"
            ExpectedClientId = $assignedId
            CallbackClientId = $srvClientId
            IdentityMatchConfirmed = $true
            Details = "Server callback ClientId matches client assigned ClientId ($srvClientId)" 
        }
    } else {
        $reason = if (-not $srvClientId) { "Callback missing in server log" } else { "ClientId mismatch: Server reported $srvClientId, Expected $assignedId" }
        Report-Fail "Stage 6: Server Connection Callback" $reason
        $netcodeStages["Stage06_ServerConnectionCallback"] = @{ 
            Status = "FAIL"
            ExpectedClientId = $assignedId
            CallbackClientId = $srvClientId
            IdentityMatchConfirmed = $false
            Details = $reason 
        }
    }

    # Stage 7: Server Spawns Authoritative NetworkPlayer & Identity Correlation
    $srvSpawnClientId = $null
    if ($serverLogContent -match "Spawned NetworkPlayer for client (\d+)") {
        $srvSpawnClientId = [int]$Matches[1]
    }

    $stage7Match = ($assignedId -ne $null -and $srvSpawnClientId -ne $null -and $assignedId -eq $srvSpawnClientId)
    if ($stage7Match) {
        Report-Pass "Stage 7: Server spawned authoritative NetworkPlayer for ClientId=$srvSpawnClientId (Matches Expected $assignedId)"
        $netcodeStages["Stage07_ServerSpawnPlayer"] = @{ 
            Status = "PASS"
            ExpectedClientId = $assignedId
            SpawnedClientId = $srvSpawnClientId
            IdentityMatchConfirmed = $true
            Details = "Authoritative player spawned on server for ClientId=$srvSpawnClientId" 
        }
    } else {
        $reason = if (-not $srvSpawnClientId) { "Spawn record missing in server log" } else { "Spawned ClientId mismatch: Server spawned for $srvSpawnClientId, Expected $assignedId" }
        Report-Fail "Stage 7: Server Player Spawn" $reason
        $netcodeStages["Stage07_ServerSpawnPlayer"] = @{ 
            Status = "FAIL"
            ExpectedClientId = $assignedId
            SpawnedClientId = $srvSpawnClientId
            IdentityMatchConfirmed = $false
            Details = $reason 
        }
    }

    # Stage 8: Client Receives Replicated NetworkPlayer & Confirms Ownership
    $clientNetObjId = $null
    $clientOwnerId = $null
    if ($clientLogContent -match "Client/Server NetworkPlayer spawned! NetworkObjectId=(\d+),\s*OwnerClientId=(\d+)") {
        $clientNetObjId = [int]$Matches[1]
        $clientOwnerId = [int]$Matches[2]
    } elseif ($clientLogContent -match "Client/Server NetworkPlayer spawned! NetworkObjectId=(\d+)") {
        $clientNetObjId = [int]$Matches[1]
    }

    $stage8Match = ($assignedId -ne $null -and $clientOwnerId -ne $null -and $assignedId -eq $clientOwnerId)
    if ($stage8Match) {
        Report-Pass "Stage 8: Client received replicated NetworkPlayer (NetworkObjectId=$clientNetObjId, OwnerClientId=$clientOwnerId == Expected $assignedId)"
        $netcodeStages["Stage08_ClientReceivePlayer"] = @{ 
            Status = "PASS"
            ExpectedClientId = $assignedId
            ReplicatedOwnerClientId = $clientOwnerId
            NetworkObjectId = $clientNetObjId
            IdentityMatchConfirmed = $true
            Details = "Replicated player received and verified for OwnerClientId=$clientOwnerId" 
        }
    } else {
        $reason = if (-not $clientNetObjId) { "Replicated player spawn confirmation missing in client log" } elseif (-not $clientOwnerId) { "OwnerClientId missing in client player spawn log" } else { "OwnerClientId mismatch: Client has $clientOwnerId, Expected $assignedId" }
        Report-Fail "Stage 8: Client Player Replication" $reason
        $netcodeStages["Stage08_ClientReceivePlayer"] = @{ 
            Status = "FAIL"
            ExpectedClientId = $assignedId
            ReplicatedOwnerClientId = $clientOwnerId
            NetworkObjectId = $clientNetObjId
            IdentityMatchConfirmed = $false
            Details = $reason 
        }
    }

    # Extract Shot Correlation ID from Client Log
    $shotCorrelationId = $null
    if ($clientLogContent -match "Sending test gameplay RPC FireWeaponRpc with ShotId=(\d+)") {
        $shotCorrelationId = [int]$Matches[1]
    }

    # Stage 9: Correlated RPC Request Proof (Client sent, Server processed)
    $cSentRpc = ($shotCorrelationId -ne $null -and $clientLogContent -match "Sending test gameplay RPC FireWeaponRpc with ShotId=$shotCorrelationId")
    $sProcRpc = ($assignedId -ne $null -and $shotCorrelationId -ne $null -and $serverLogContent -match "Server processed gameplay RPC: FireWeaponRpc from ClientId=$assignedId, WeaponSlot=\d+, ShotId=$shotCorrelationId")

    if ($cSentRpc -and $sProcRpc) {
        Report-Pass "Stage 9: Correlated RPC Request Verified (Client sent FireWeaponRpc ShotId=$shotCorrelationId, Server processed for ClientId=$assignedId)"
        $netcodeStages["Stage09_ClientFireWeaponRpc"] = @{ 
            Status = "PASS"
            Details = "Two-sided request proof confirmed with exact Shot Correlation ID"
            ClientSent = $true
            ServerProcessed = $true
            ClientId = $assignedId
            ShotCorrelationId = $shotCorrelationId
        }
    } else {
        $reason = if (-not $shotCorrelationId) { "Shot Correlation ID missing in client log" } elseif (-not $cSentRpc) { "Client send record missing for ShotId=$shotCorrelationId" } elseif (-not $sProcRpc) { "Server processing record missing for ClientId=$assignedId, ShotId=$shotCorrelationId" } else { "Unknown" }
        Report-Fail "Stage 9: FireWeaponRpc Processing" $reason
        $netcodeStages["Stage09_ClientFireWeaponRpc"] = @{ 
            Status = "FAIL"
            Details = $reason
            ClientSent = $cSentRpc
            ServerProcessed = $sProcRpc
            ClientId = $assignedId
            ShotCorrelationId = $shotCorrelationId
        }
    }

    # Stage 10: Correlated RPC Response Proof (Server broadcast effects, Client received PlayShotEffectsRpc for same ShotId)
    $sBroadcastEffects = ($assignedId -ne $null -and $shotCorrelationId -ne $null -and $serverLogContent -match "Server broadcasting PlayShotEffectsRpc: ClientId=$assignedId,\s*ShotId=$shotCorrelationId")
    $cReceivedEffects = ($shotCorrelationId -ne $null -and $clientLogContent -match "Client processed PlayShotEffectsRpc from server confirming shot fired! ShotId=$shotCorrelationId")

    if ($sProcRpc -and $sBroadcastEffects -and $cReceivedEffects) {
        Report-Pass "Stage 10: Correlated RPC Response Verified (Server broadcast and Client processed PlayShotEffectsRpc with ShotId=$shotCorrelationId)"
        $netcodeStages["Stage10_ClientPlayShotEffectsRpc"] = @{ 
            Status = "PASS"
            Details = "Complete correlated round-trip verified: Server broadcast effects and Client confirmed receipt for ShotId=$shotCorrelationId"
            ServerProcessed = $true
            ServerBroadcast = $true
            ClientReceived = $true
            ShotCorrelationId = $shotCorrelationId
        }
    } else {
        $reason = if (-not $sProcRpc) { "Server did not process request" } elseif (-not $sBroadcastEffects) { "Server did not broadcast PlayShotEffectsRpc for ShotId=$shotCorrelationId" } elseif (-not $cReceivedEffects) { "Client did not log receipt of PlayShotEffectsRpc for ShotId=$shotCorrelationId" } else { "Unknown failure" }
        Report-Fail "Stage 10: PlayShotEffectsRpc Reception" $reason
        $netcodeStages["Stage10_ClientPlayShotEffectsRpc"] = @{ 
            Status = "FAIL"
            Details = $reason
            ServerProcessed = $sProcRpc
            ServerBroadcast = $sBroadcastEffects
            ClientReceived = $cReceivedEffects
            ShotCorrelationId = $shotCorrelationId
        }
    }

    # Stage 11: Client Clean Disconnect
    $clientLogClean = ($clientLogContent -match "Client initiated clean disconnect" -or $clientLogContent -match "Disconnected from server")
    if ($clientExitedInTime -and $clientExitCode -eq 0 -and $clientLogClean) {
        Report-Pass "Stage 11: Client disconnected cleanly (ExitCode 0, Graceful shutdown in $($clientCleanupType) mode)"
        $netcodeStages["Stage11_ClientCleanDisconnect"] = @{ 
            Status = "PASS"
            Details = "Clean disconnect confirmed in client log and process exit code 0"
            ExitCode = $clientExitCode
            CleanupType = $clientCleanupType
            TimedOut = (-not $clientExitedInTime)
        }
    } else {
        $reason = "TimedOut=$(-not $clientExitedInTime), ExitCode=$clientExitCode, CleanupType=$clientCleanupType, LogClean=$clientLogClean"
        Report-Fail "Stage 11: Client Disconnect" $reason
        $netcodeStages["Stage11_ClientCleanDisconnect"] = @{ 
            Status = "FAIL"
            Details = $reason
            ExitCode = $clientExitCode
            CleanupType = $clientCleanupType
            TimedOut = (-not $clientExitedInTime)
        }
    }

    # Stage 12: Server Clean Shutdown
    $serverLogClean = ($serverLogContent -match "shutting down cleanly" -or $serverLogContent -match "All clients disconnected")
    if ($serverExitedInTime -and $serverExitCode -eq 0 -and $serverLogClean -and $serverTerminated) {
        Report-Pass "Stage 12: Server shut down cleanly (ExitCode 0, Graceful termination confirmed)"
        $netcodeStages["Stage12_ServerCleanShutdown"] = @{ 
            Status = "PASS"
            Details = "Clean shutdown confirmed in server log, process exit code 0, terminated"
            ExitCode = $serverExitCode
            CleanupType = $serverCleanupType
            Terminated = $serverTerminated
            TimedOut = (-not $serverExitedInTime)
        }
    } else {
        $reason = "TimedOut=$(-not $serverExitedInTime), ExitCode=$serverExitCode, CleanupType=$serverCleanupType, LogClean=$serverLogClean, Terminated=$serverTerminated"
        Report-Fail "Stage 12: Server Shutdown" $reason
        $netcodeStages["Stage12_ServerCleanShutdown"] = @{ 
            Status = "FAIL"
            Details = $reason
            ExitCode = $serverExitCode
            CleanupType = $serverCleanupType
            Terminated = $serverTerminated
            TimedOut = (-not $serverExitedInTime)
        }
    }

    # -------------------------------------------------------------
    # STAGE 4: Automated Test Suites
    # -------------------------------------------------------------
    Write-Host "`n--- [STAGE 4] Automated .NET Test Suites ---" -ForegroundColor Yellow

    # Execute and dynamically parse Simulation tests
    $simOutput = & dotnet test "shared/OutrunAftermath.Simulation.Tests/OutrunAftermath.Simulation.Tests.csproj" --no-build --logger "console;verbosity=minimal"
    $simExitCode = $LASTEXITCODE
    $simPassed = 0
    $simTotal = 0
    $simFailed = 0
    foreach ($line in $simOutput) {
        if ($line -match "Failed:\s+(\d+),\s+Passed:\s+(\d+).*Total:\s+(\d+)") {
            $simFailed = [int]$Matches[1]
            $simPassed = [int]$Matches[2]
            $simTotal = [int]$Matches[3]
        }
    }
    if ($simExitCode -eq 0 -and $simFailed -eq 0 -and $simPassed -gt 0) {
        Report-Pass "Simulation Engine Test Suite ($simPassed/$simTotal unit tests passed, ExitCode 0)"
        $stage4Results["SimulationTests"] = @{ Status = "PASS"; Passed = $simPassed; Total = $simTotal; Failed = $simFailed; ExitCode = $simExitCode }
    } else {
        Report-Fail "Simulation Engine Test Suite" "Failed=$simFailed, Passed=$simPassed, Total=$simTotal, ExitCode=$simExitCode"
        $stage4Results["SimulationTests"] = @{ Status = "FAIL"; Passed = $simPassed; Total = $simTotal; Failed = $simFailed; ExitCode = $simExitCode }
    }

    # Execute and dynamically parse API tests
    $apiOutput = & dotnet test "backend/tests/OutrunAftermath.Api.Tests/OutrunAftermath.Api.Tests.csproj" --no-build --logger "console;verbosity=minimal"
    $apiExitCode = $LASTEXITCODE
    $apiPassed = 0
    $apiTotal = 0
    $apiFailed = 0
    foreach ($line in $apiOutput) {
        if ($line -match "Failed:\s+(\d+),\s+Passed:\s+(\d+).*Total:\s+(\d+)") {
            $apiFailed = [int]$Matches[1]
            $apiPassed = [int]$Matches[2]
            $apiTotal = [int]$Matches[3]
        }
    }
    if ($apiExitCode -eq 0 -and $apiFailed -eq 0 -and $apiPassed -gt 0) {
        Report-Pass "Web API & Integration Test Suite ($apiPassed/$apiTotal unit tests passed, ExitCode 0)"
        $stage4Results["ApiIntegrationTests"] = @{ Status = "PASS"; Passed = $apiPassed; Total = $apiTotal; Failed = $apiFailed; ExitCode = $apiExitCode }
    } else {
        Report-Fail "Web API & Integration Test Suite" "Failed=$apiFailed, Passed=$apiPassed, Total=$apiTotal, ExitCode=$apiExitCode"
        $stage4Results["ApiIntegrationTests"] = @{ Status = "FAIL"; Passed = $apiPassed; Total = $apiTotal; Failed = $apiFailed; ExitCode = $apiExitCode }
    }

    # -------------------------------------------------------------
    # GENERATE DYNAMIC MACHINE-READABLE REPORT
    # -------------------------------------------------------------
    $overallStatus = if ($failCount -eq 0 -and $skippedCount -eq 0 -and $passCount -gt 0) { "PASS" } else { "FAIL" }

    $reportData = [ordered]@{
        Timestamp = (Get-Date).ToUniversalTime().ToString("o")
        TestSuite = "OUTRUN: AFTERMATH End-to-End Production Verification Suite"
        OverallStatus = $overallStatus
        Summary = @{
            TotalPassed = $passCount
            TotalFailed = $failCount
            TotalSkipped = $skippedCount
        }
        Stage1_BackendApi = $stage1Results
        Stage2_SecurityHmac = $stage2Results
        Stage3_Netcode12Stages = $netcodeStages
        Stage4_UnitSuites = $stage4Results
        ExecutionEnvironment = @{
            ServerExecutable = $srvTarget
            ServerBuildType = "DedicatedServer"
            ClientExecutable = $cltTarget
            ClientBuildType = "StandaloneClient"
            ServerPID = if ($serverProc) { $serverProc.Id } else { $null }
            ClientPID = if ($clientProc) { $clientProc.Id } else { $null }
            ServerExitCode = $serverExitCode
            ClientExitCode = $clientExitCode
            ServerCleanup = $serverCleanupType
            ClientCleanup = $clientCleanupType
            ServerTerminated = $serverTerminated
            AssignedClientId = if ($assignedId) { [int]$assignedId } else { $null }
            ServerCallbackClientId = if ($srvClientId) { [int]$srvClientId } else { $null }
            ServerSpawnClientId = if ($srvSpawnClientId) { [int]$srvSpawnClientId } else { $null }
            ClientReplicatedOwnerId = if ($clientOwnerId) { [int]$clientOwnerId } else { $null }
            IdentityMatchConfirmed = ($stage6Match -and $stage7Match -and $stage8Match)
            ShotCorrelationId = $shotCorrelationId
            RpcRequestVerified = ($cSentRpc -and $sProcRpc)
            RpcResponseVerified = ($sProcRpc -and $sBroadcastEffects -and $cReceivedEffects)
        }
    }

    $reportJsonPath = "$PSScriptRoot\e2e_production_test_report.json"
    $brainArtifact = "C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5\e2e_production_test_report.json"

    $reportJson = $reportData | ConvertTo-Json -Depth 6
    Set-Content -Path $reportJsonPath -Value $reportJson -Encoding UTF8
    try { Set-Content -Path $brainArtifact -Value $reportJson -Encoding UTF8 } catch {}

    Write-Host "`nMachine-readable verification report saved to: $reportJsonPath" -ForegroundColor Cyan

} finally {
    Write-Host "`nCleaning up processes..." -ForegroundColor DarkGray
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($serverProc -and -not $serverProc.HasExited) {
        Stop-Process -Id $serverProc.Id -Force -ErrorAction SilentlyContinue
    }
    if ($clientProc -and -not $clientProc.HasExited) {
        Stop-Process -Id $clientProc.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "                E2E VERIFICATION SUITE SUMMARY                   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Total Passed:  $passCount" -ForegroundColor Green
Write-Host "Total Failed:  $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "Total Skipped: $skippedCount" -ForegroundColor $(if ($skippedCount -eq 0) { "Green" } else { "Yellow" })

# Mirror console log to brain artifact
try {
    Stop-Transcript -ErrorAction SilentlyContinue
    $brainConsoleLog = "C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5\e2e_production_test_console.log"
    if (Test-Path $consoleLogPath) {
        Copy-Item -Path $consoleLogPath -Destination $brainConsoleLog -Force -ErrorAction SilentlyContinue
    }
} catch {}

if ($failCount -eq 0 -and $skippedCount -eq 0 -and $passCount -gt 0) {
    Write-Host "`n>>> ALL END-TO-END PRODUCTION CHECKS SUCCEEDED (100% PASS) <<<" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n>>> PRODUCTION VERIFICATION FAILED WITH $failCount ISSUES ($skippedCount SKIPPED) <<<" -ForegroundColor Red
    exit 1
}
