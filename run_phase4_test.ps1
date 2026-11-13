$exe = "D:\projects\outrun-aftermath\client\Builds\Windows\OutrunAftermath.exe"
$serverLog = "D:\projects\outrun-aftermath\test_server_convoy.log"
$client1Log = "D:\projects\outrun-aftermath\test_client1_convoy.log"
$client2Log = "D:\projects\outrun-aftermath\test_client2_convoy.log"

Remove-Item -Force -ErrorAction SilentlyContinue $serverLog, $client1Log, $client2Log

Write-Host "Starting Dedicated Server in Convoy Operations Mode (-convoy)..."
$pServer = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-server", "-convoy", "-port", "7777", "-autoclose", "18", "-logfile", $serverLog -PassThru
Start-Sleep -Seconds 2

Write-Host "Starting Client 1 (Rogue Convoy Driver)..."
$pClient1 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-convoy", "-connect-ip", "127.0.0.1", "-connect-port", "7777", "-autotest-shoot", "-autoclose", "16", "-logfile", $client1Log -PassThru
Start-Sleep -Seconds 2

Write-Host "Starting Client 2 (Sentinel Interceptor Driver)..."
$pClient2 = Start-Process -FilePath $exe -ArgumentList "-batchmode", "-nographics", "-client", "-convoy", "-connect-ip", "127.0.0.1", "-connect-port", "7777", "-autotest-shoot", "-autoclose", "14", "-logfile", $client2Log -PassThru

Write-Host "Waiting 14 seconds for multi-process Convoy Operations & vehicle tests..."
Start-Sleep -Seconds 14

Write-Host "Checking process completion..."
if (!$pServer.HasExited) { Stop-Process -Id $pServer.Id -Force -ErrorAction SilentlyContinue }
if (!$pClient1.HasExited) { Stop-Process -Id $pClient1.Id -Force -ErrorAction SilentlyContinue }
if (!$pClient2.HasExited) { Stop-Process -Id $pClient2.Id -Force -ErrorAction SilentlyContinue }

Write-Host "`n=== SERVER LOG HIGHLIGHTS ==="
Get-Content $serverLog | Select-String -Pattern "Network|Match|Spawn|Vehicle|Convoy|Raptor|Board|DAMAGE|ROSTER" | Select-Object -First 40

Write-Host "`n=== CLIENT 1 LOG HIGHLIGHTS ==="
Get-Content $client1Log | Select-String -Pattern "NET_TEST|Convoy|Vehicle|Speed|Nitro|Turret|Seat|Switch|Damage" | Select-Object -First 40

Write-Host "`n=== CLIENT 2 LOG HIGHLIGHTS ==="
Get-Content $client2Log | Select-String -Pattern "NET_TEST|Convoy|Vehicle|Speed|Nitro|Turret|Seat|Switch|Damage" | Select-Object -First 40
