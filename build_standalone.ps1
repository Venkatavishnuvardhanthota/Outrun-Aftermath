<#
.SYNOPSIS
    Compiles OUTRUN: AFTERMATH standalone production binaries for Windows x64.
.DESCRIPTION
    Builds the dedicated headless server (OutrunAftermath_Server.exe) and/or
    the visual game client (OutrunAftermath_Client.exe).
#>
param(
    [ValidateSet("All", "Client", "Server", "Standard")]
    [string]$Target = "All",
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe",
    [string]$ProjectPath = "$PSScriptRoot\client",
    [string]$LogFile = "$PSScriptRoot\client\build_standalone.log"
)

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  OUTRUN: AFTERMATH — STANDALONE BUILD PIPELINE  " -ForegroundColor Cyan
Write-Host "  Target: $Target                                " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

if (-not (Test-Path $UnityPath)) {
    Write-Warning "Unity executable not found at '$UnityPath'."
    exit 1
}

$methodName = switch ($Target) {
    "Client"   { "OutrunAftermath.Client.Editor.BuildScript.BuildWindowsClient" }
    "Server"   { "OutrunAftermath.Client.Editor.BuildScript.BuildWindowsServer" }
    "Standard" { "OutrunAftermath.Client.Editor.BuildScript.BuildWindows" }
    default    { "OutrunAftermath.Client.Editor.BuildScript.BuildAllWindows" }
}

Write-Host "Invoking Unity method: $methodName" -ForegroundColor Yellow
$proc = Start-Process -FilePath $UnityPath `
    -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$ProjectPath`"", "-executeMethod", $methodName, "-logFile", "`"$LogFile`"" `
    -PassThru -NoNewWindow -Wait

if ($proc.ExitCode -eq 0) {
    Write-Host "Build completed successfully with Exit Code 0!" -ForegroundColor Green
    if (Test-Path "$ProjectPath\Builds") {
        Write-Host "`nGenerated Build Binaries:" -ForegroundColor Cyan
        Get-ChildItem -Recurse "$ProjectPath\Builds" -Filter "*.exe" | Format-Table FullName, Length, LastWriteTime
    }
} else {
    Write-Error "Build failed with Exit Code $($proc.ExitCode). See '$LogFile' for details."
}

exit $proc.ExitCode
