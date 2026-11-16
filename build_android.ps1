<#
.SYNOPSIS
    Compiles or scaffolds OUTRUN: AFTERMATH Android build (AAB/APK).
.DESCRIPTION
    Invokes Unity 6 in batchmode using OutrunAftermath.Client.Editor.BuildScript.BuildAndroid.
#>
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe",
    [string]$ProjectPath = "$PSScriptRoot\client",
    [string]$LogFile = "$PSScriptRoot\client\build_android.log"
)

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "   OUTRUN: AFTERMATH — ANDROID BUILD PIPELINE   " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

if (-not (Test-Path $UnityPath)) {
    Write-Warning "Unity executable not found at '$UnityPath'. Please specify -UnityPath."
    exit 1
}

Write-Host "Project Path: $ProjectPath"
Write-Host "Log File:     $LogFile"
Write-Host "Executing OutrunAftermath.Client.Editor.BuildScript.BuildAndroid..." -ForegroundColor Yellow

$proc = Start-Process -FilePath $UnityPath `
    -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$ProjectPath`"", "-executeMethod", "OutrunAftermath.Client.Editor.BuildScript.BuildAndroid", "-logFile", "`"$LogFile`"" `
    -PassThru -NoNewWindow -Wait

if ($proc.ExitCode -eq 0) {
    Write-Host "Android Build Pipeline completed successfully (Exit Code 0)." -ForegroundColor Green
    if (Test-Path "$ProjectPath\Builds\Android") {
        Get-ChildItem "$ProjectPath\Builds\Android" | Format-Table Name, Length, LastWriteTime
    }
} else {
    Write-Error "Android Build Pipeline exited with error code $($proc.ExitCode). See '$LogFile' for details."
}

exit $proc.ExitCode
