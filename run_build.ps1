$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe'
$buildLog = 'D:\projects\outrun-aftermath\build_output.log'

Remove-Item -Force -ErrorAction SilentlyContinue $buildLog

$procArgs = @(
    '-batchmode',
    '-quit',
    '-projectPath', 'D:\projects\outrun-aftermath\client',
    '-executeMethod', 'OutrunAftermath.Client.Editor.BuildScript.BuildWindows',
    '-logFile', $buildLog
)

Write-Host "Launching Unity 6 Build..."
$proc = Start-Process -FilePath $unity -ArgumentList $procArgs -PassThru
Write-Host "Unity PID: $($proc.Id). Waiting for completion..."
$proc.WaitForExit()
Write-Host "Unity Exited with Code: $($proc.ExitCode)"

if (Test-Path $buildLog) {
    Get-Content $buildLog | Select-String -Pattern "PHASE 8|PHASE 7|PHASE 6|BuildResult|error CS|Exception" | Select-Object -Last 10
}
