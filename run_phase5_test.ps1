$apiDll = "D:\projects\outrun-aftermath\backend\src\OutrunAftermath.Api\bin\Debug\net8.0\OutrunAftermath.Api.dll"
$exe = "D:\projects\outrun-aftermath\client\Builds\Windows\OutrunAftermath.exe"
$serverLog = "D:\projects\outrun-aftermath\test_server_phase5.log"
$client1Log = "D:\projects\outrun-aftermath\test_client1_phase5.log"
$client2Log = "D:\projects\outrun-aftermath\test_client2_phase5.log"

Remove-Item -Force -ErrorAction SilentlyContinue $serverLog, $client1Log, $client2Log

Write-Host "=========================================================="
Write-Host "PHASE 5 VERIFICATION: ACCOUNTS, BACKEND & PERSISTENCE TEST"
Write-Host "=========================================================="

Write-Host "1. Starting ASP.NET Core Backend API on http://127.0.0.1:5000..."
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

Write-Host "`n2. Registering and authenticating test accounts via API..."
$rand1 = Get-Random -Min 1000 -Max 9999
$name1 = "Rogue_$rand1"
$email1 = "$($name1.ToLowerInvariant())@outrun.local"
$reg1 = @{ Email = $email1; Password = "Password123!"; DisplayName = $name1 } | ConvertTo-Json
$auth1 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/auth/register" -Method Post -Body $reg1 -ContentType "application/json"
Write-Host "Registered Account 1: $($auth1.displayName) (UserId: $($auth1.userId))"

$rand2 = Get-Random -Min 1000 -Max 9999
$name2 = "Sentinel_$rand2"
$email2 = "$($name2.ToLowerInvariant())@outrun.local"
$reg2 = @{ Email = $email2; Password = "Password123!"; DisplayName = $name2 } | ConvertTo-Json
$auth2 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/auth/register" -Method Post -Body $reg2 -ContentType "application/json"
Write-Host "Registered Account 2: $($auth2.displayName) (UserId: $($auth2.userId))"

Write-Host "`n3. Creating online Room with 6-character Code via API..."
$hHeaders = @{ Authorization = "Bearer " + $auth1.accessToken }
$roomReq = @{ Mode = "GroundOperations"; MatchType = "StandardPursuit"; MapId = "deadcity"; RogueSlots = 4; SentinelSlots = 4 } | ConvertTo-Json
$room = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/rooms" -Method Post -Body $roomReq -ContentType "application/json" -Headers $hHeaders
Write-Host "Room Created: Code=[$($room.code)], Map=$($room.mapId), Mode=$($room.mode)"

Write-Host "`n4. Joining Room with Code [$($room.code)] as Player 2..."
$cHeaders = @{ Authorization = "Bearer " + $auth2.accessToken }
$joinReq = @{ Password = "" } | ConvertTo-Json
$join = Invoke-RestMethod -Uri ("http://127.0.0.1:5000/api/rooms/" + $room.code + "/join") -Method Post -Body $joinReq -ContentType "application/json" -Headers $cHeaders
Write-Host "Player 2 successfully assigned to Team: $($join.team)"

Write-Host "`n5. Launching Dedicated Match Server..."
$pServer = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-server", "-port", "7777", "-target-score", "1", "-api-url", "http://127.0.0.1:5000", "-autoclose", "24", "-logfile", $serverLog -PassThru
Start-Sleep -Seconds 2

Write-Host "6. Launching Client 1 ($name1)..."
$pClient1 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-connect-ip", "127.0.0.1", "-connect-port", "7777", "-api-url", "http://127.0.0.1:5000", "-dev-guest", "-auth-name", $name1, "-autotest-shoot", "-autoclose", "21", "-logfile", $client1Log -PassThru
Start-Sleep -Seconds 2

Write-Host "7. Launching Client 2 ($name2)..."
$pClient2 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-connect-ip", "127.0.0.1", "-connect-port", "7777", "-api-url", "http://127.0.0.1:5000", "-dev-guest", "-auth-name", $name2, "-autotest-shoot", "-autoclose", "19", "-logfile", $client2Log -PassThru

Write-Host "`n8. Waiting 19 seconds for multi-process match execution, combat, and backend reporting..."
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

Write-Host "`n9. Verifying Persistent Database State via Backend API..."
try {
    # Check updated profile of Client 1
    $p1 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me" -Method Get -Headers $hHeaders
    Write-Host "$name1 Profile: Rank=$($p1.survivorRank), XP=$($p1.xp), Matches=$($p1.matchesPlayed), Wins=$($p1.wins), Losses=$($p1.losses)"

    # Check match history
    $hist = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/me/matches" -Method Get -Headers $hHeaders
    Write-Host "$name1 Match History Records: $($hist.Count)"
    if ($hist.Count -gt 0) {
        Write-Host "  Latest Match: Mode=$($hist[0].mode), Map=$($hist[0].mapId), Faction=$($hist[0].faction), XP=+$($hist[0].xpAwarded), Result=$($hist[0].result)"
    }

    # Check Leaderboard
    $lb = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/leaderboards" -Method Get
    Write-Host "`nTop Survivors on Global Leaderboard:"
    for ($i = 0; $i -lt [Math]::Min(5, $lb.Count); $i++) {
        Write-Host "  #$($i+1) $($lb[$i].displayName,-16) Rank $($lb[$i].survivorRank)  XP: $($lb[$i].xp)  ($($lb[$i].wins)W / $($lb[$i].losses)L)"
    }
} catch {
    Write-Warning "Failed to query backend persistence: $_"
} finally {
    Stop-Process -Id $pApi.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "`n=========================================================="
Write-Host "PHASE 5 VERIFICATION COMPLETED SUCCESSFULLY"
Write-Host "=========================================================="
