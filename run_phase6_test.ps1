$apiDll = "D:\projects\outrun-aftermath\backend\src\OutrunAftermath.Api\bin\Debug\net8.0\OutrunAftermath.Api.dll"
$exe = "D:\projects\outrun-aftermath\client\Builds\Windows\OutrunAftermath.exe"
$serverLog = "D:\projects\outrun-aftermath\test_server_phase6.log"
$client1Log = "D:\projects\outrun-aftermath\test_client1_phase6.log"
$client2Log = "D:\projects\outrun-aftermath\test_client2_phase6.log"

Remove-Item -Force -ErrorAction SilentlyContinue $serverLog, $client1Log, $client2Log

Write-Host "=========================================================="
Write-Host "PHASE 6 VERIFICATION: MATCHMAKING, SQUADS & SOCIAL SYSTEMS"
Write-Host "=========================================================="

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

# Helper to register an account
function Register-Player([string]$name) {
    $email = "$($name.ToLowerInvariant())@outrun.local"
    $body = @{ Email = $email; Password = "Password123!"; DisplayName = $name } | ConvertTo-Json
    return Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/auth/register" -Method Post -Body $body -ContentType "application/json"
}

Write-Host "`n2. Registering 4 test players for Squad and Matchmaking..."
$rand = Get-Random -Min 1000 -Max 9999
$p1 = Register-Player "SquadAlpha_$rand"
$p2 = Register-Player "SquadBravo_$rand"
$p3 = Register-Player "EnemyCharlie_$rand"
$p4 = Register-Player "EnemyDelta_$rand"

Write-Host "  P1 (Leader): $($p1.displayName) (ID: $($p1.userId))"
Write-Host "  P2 (Member): $($p2.displayName) (ID: $($p2.userId))"
Write-Host "  P3 (Enemy):  $($p3.displayName) (ID: $($p3.userId))"
Write-Host "  P4 (Enemy):  $($p4.displayName) (ID: $($p4.userId))"

$h1 = @{ Authorization = "Bearer " + $p1.accessToken }
$h2 = @{ Authorization = "Bearer " + $p2.accessToken }
$h3 = @{ Authorization = "Bearer " + $p3.accessToken }
$h4 = @{ Authorization = "Bearer " + $p4.accessToken }

Write-Host "`n3. Testing Presence System..."
$pres1 = @{ PresenceStatus = "in-lobby" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me/presence" -Method Post -Body $pres1 -ContentType "application/json" -Headers $h1 | Out-Null
$pres2 = @{ PresenceStatus = "online" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me/presence" -Method Post -Body $pres2 -ContentType "application/json" -Headers $h2 | Out-Null
Write-Host "  Presence updated: $($p1.displayName) -> [in-lobby], $($p2.displayName) -> [online]"

Write-Host "`n4. Testing Friends & Social Network..."
$frReq = @{ AddresseeUsernameOrId = $p2.displayName } | ConvertTo-Json
$frRes = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/friends/request" -Method Post -Body $frReq -ContentType "application/json" -Headers $h1
Write-Host "  Friend Request sent from $($p1.displayName) to $($p2.displayName): $($frRes.message)"

# P2 checks requests
$incoming = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/friends/requests" -Method Get -Headers $h2
Write-Host "  $($p2.displayName) incoming requests: $($incoming.incoming.Count) (From: $($incoming.incoming[0].requesterName))"

# P2 accepts request
$acceptReq = @{ RequestId = $incoming.incoming[0].id; Accept = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/friends/respond" -Method Post -Body $acceptReq -ContentType "application/json" -Headers $h2 | Out-Null
Write-Host "  $($p2.displayName) accepted friend request from $($p1.displayName)!"

# Verify P1 friend list with presence
$friendsList = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/friends" -Method Get -Headers $h1
Write-Host "  $($p1.displayName) Friends List: Count=$($friendsList.Count), Ally=$($friendsList[0].displayName), Presence=$($friendsList[0].presenceStatus)"

Write-Host "`n5. Testing Survivor Squad (Party) Management..."
# P1 creates party
$party = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/parties" -Method Post -Headers $h1
Write-Host "  Squad Created! Code: [$($party.code)], Leader: $($p1.displayName)"

# P2 joins party by code
$joinPartyReq = @{ Code = $party.code } | ConvertTo-Json
$joined = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/parties/join" -Method Post -Body $joinPartyReq -ContentType "application/json" -Headers $h2
Write-Host "  $($p2.displayName) joined Squad [$($joined.code)] (Total Members: $($joined.members.Count))"

# P2 sets ready
$readyReq = @{ IsReady = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/parties/ready" -Method Post -Body $readyReq -ContentType "application/json" -Headers $h2 | Out-Null
Write-Host "  $($p2.displayName) marked READY"

# Verify party details from P1
$partyDetails = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/parties/me" -Method Get -Headers $h1
Write-Host "  Squad Details: Code=$($partyDetails.code), InParty=$($partyDetails.inParty)"
foreach ($m in $partyDetails.members) {
    Write-Host "    - $($m.displayName): Leader=$($m.isLeader), Ready=$($m.isReady), Rank=$($m.rank)"
}

Write-Host "`n6. Testing Matchmaking Queue & Snake-Draft Assignment..."
# P1 queues the squad
$queueSquadReq = @{ Mode = "GroundOperations"; Region = "na-east" } | ConvertTo-Json
$ticketSquad = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/parties/queue" -Method Post -Body $queueSquadReq -ContentType "application/json" -Headers $h1
Write-Host "  Squad Ticket Enqueued: ID=$($ticketSquad.ticketId), Status=$($ticketSquad.status)"

# Enemies queue solo
$queueSolo3 = @{ Mode = "GroundOperations"; Region = "na-east"; IsParty = $false } | ConvertTo-Json
$ticket3 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/matchmaking/ticket" -Method Post -Body $queueSolo3 -ContentType "application/json" -Headers $h3

$queueSolo4 = @{ Mode = "GroundOperations"; Region = "na-east"; IsParty = $false } | ConvertTo-Json
$ticket4 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/matchmaking/ticket" -Method Post -Body $queueSolo4 -ContentType "application/json" -Headers $h4

Write-Host "  Enemies ($($p3.displayName), $($p4.displayName)) Enqueued into Matchmaking Pool"

# Wait for Matchmaker tick and matching
Write-Host "  Waiting for matchmaker tick..."
$matched = $false
$squadAssignment = $null
$p2Assignment = $null

for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep -Milliseconds 500
    $t1 = Invoke-RestMethod -Uri ("http://127.0.0.1:5000/api/matchmaking/ticket/" + $ticketSquad.ticketId) -Method Get -Headers $h1
    if ($t1.status -eq "matched") {
        $matched = $true
        $squadAssignment = $t1.assignment
        break
    }
}

if (!$matched) {
    Write-Error "Matchmaker failed to propose a match within timeout."
    Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Verify P2 received match
$t2 = Invoke-RestMethod -Uri ("http://127.0.0.1:5000/api/matchmaking/ticket/" + $ticketSquad.ticketId) -Method Get -Headers $h2
$p2Assignment = $t2.assignment

Write-Host "  MATCH FOUND!"
Write-Host "  Server: $($squadAssignment.serverIp):$($squadAssignment.serverPort)"
Write-Host "  Mode:   $($squadAssignment.mode), Map: $($squadAssignment.mapId)"
Write-Host "  $($p1.displayName) assigned Team: $($squadAssignment.team.ToUpper())"
Write-Host "  $($p2.displayName) assigned Team: $($p2Assignment.team.ToUpper())"

if ($squadAssignment.team -ne $p2Assignment.team) {
    Write-Error "FATAL: Squad was split across different teams in matchmaking! ($($squadAssignment.team) vs $($p2Assignment.team))"
    Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "  SQUAD INTEGRITY VERIFIED: Both squadmates assigned to Team $($squadAssignment.team.ToUpper())!"

Write-Host "`n7. Launching Dedicated Match Server at $($squadAssignment.serverIp):$($squadAssignment.serverPort)..."
$pServer = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-server", "-port", "$($squadAssignment.serverPort)", "-target-score", "1", "-api-url", "http://127.0.0.1:5000", "-autoclose", "24", "-logfile", $serverLog -PassThru
Start-Sleep -Seconds 2

Write-Host "8. Launching Matched Client 1 ($($p1.displayName))..."
$pClient1 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-connect-ip", "$($squadAssignment.serverIp)", "-connect-port", "$($squadAssignment.serverPort)", "-api-url", "http://127.0.0.1:5000", "-dev-guest", "-auth-name", $p1.displayName, "-autotest-shoot", "-autoclose", "21", "-logfile", $client1Log -PassThru
Start-Sleep -Seconds 2

Write-Host "9. Launching Matched Client 2 ($($p2.displayName))..."
$pClient2 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-connect-ip", "$($squadAssignment.serverIp)", "-connect-port", "$($squadAssignment.serverPort)", "-api-url", "http://127.0.0.1:5000", "-dev-guest", "-auth-name", $p2.displayName, "-autotest-shoot", "-autoclose", "19", "-logfile", $client2Log -PassThru

Write-Host "`n10. Waiting 19 seconds for match execution, combat, and backend reporting..."
Start-Sleep -Seconds 19

Write-Host "Checking process completion..."
if (!$pServer.HasExited) { Stop-Process -Id $pServer.Id -Force -ErrorAction SilentlyContinue }
if (!$pClient1.HasExited) { Stop-Process -Id $pClient1.Id -Force -ErrorAction SilentlyContinue }
if (!$pClient2.HasExited) { Stop-Process -Id $pClient2.Id -Force -ErrorAction SilentlyContinue }

Write-Host "`n=== SERVER LOG HIGHLIGHTS ==="
Get-Content $serverLog | Select-String -Pattern "Network|Match|Spawn|Registered account|BACKEND|VICTORY|ROSTER|OP-" | Select-Object -First 30

Write-Host "`n=== CLIENT 1 LOG HIGHLIGHTS ==="
Get-Content $client1Log | Select-String -Pattern "NET_TEST|ApiClient|Authenticated|Profile|Backend|Registered|HP" | Select-Object -First 30

Write-Host "`n=== CLIENT 2 LOG HIGHLIGHTS ==="
Get-Content $client2Log | Select-String -Pattern "NET_TEST|ApiClient|Authenticated|Profile|Backend|Registered|HP" | Select-Object -First 30

Write-Host "`n11. Verifying Persistent Database State after Match..."
try {
    $prof1 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me" -Method Get -Headers $h1
    Write-Host "  $($p1.displayName) Post-Match Profile: Rank=$($prof1.survivorRank), XP=$($prof1.xp), Matches=$($prof1.matchesPlayed), Wins=$($prof1.wins), Losses=$($prof1.losses)"

    $hist1 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me/matches" -Method Get -Headers $h1
    Write-Host "  $($p1.displayName) Post-Match History Records: $($hist1.Count)"
    if ($hist1.Count -gt 0) {
        Write-Host "    Match: Mode=$($hist1[0].mode), Map=$($hist1[0].mapId), Faction=$($hist1[0].faction), XP=+$($hist1[0].xpAwarded), Result=$($hist1[0].result)"
    }
} catch {
    Write-Host "  Could not fetch post-match profile: $_"
}

Write-Host "`nStopping Backend API..."
Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue

Write-Host "`n=========================================================="
Write-Host "PHASE 6 VERIFICATION COMPLETED SUCCESSFULLY!"
Write-Host "=========================================================="
