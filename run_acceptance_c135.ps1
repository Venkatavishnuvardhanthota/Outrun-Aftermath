param(
    [switch]$SkipBuildAssets,
    [switch]$SkipCapture,
    [switch]$SkipValidation
)

$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe"
$clientDir = "D:\projects\outrun-aftermath\client"

if (-not (Test-Path $unityExe)) {
    Write-Error "Unity executable not found at: $unityExe"
    exit 1
}

# 1. Build All Character Assets (Animations, Controller, Prefabs)
if (-not $SkipBuildAssets) {
    Write-Host "`n=== STEP 1: Building Character Assets in Unity BatchMode ===" -ForegroundColor Cyan
    $logPath = "$clientDir\build_assets_c135.log"
    $proc = Start-Process -FilePath $unityExe `
        -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$clientDir`"", `
        "-executeMethod", "OutrunAftermath.Client.Editor.CharacterAssetPipeline.BuildAllCharacterAssetsBatchMode", `
        "-logFile", "`"$logPath`"" `
        -PassThru -NoNewWindow -Wait

    Write-Host "Build assets exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Asset build failed with exit code $($proc.ExitCode). Check $logPath"
        exit $proc.ExitCode
    }
}

# 2. Run Runtime Acceptance and Screenshot Capture (with hardware GPU)
if (-not $SkipCapture) {
    Write-Host "`n=== STEP 2: Running Runtime Acceptance & Evidence Capture ===" -ForegroundColor Cyan
    $logPath = "$clientDir\acceptance_c135.log"
    $proc = Start-Process -FilePath $unityExe `
        -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$clientDir`"", `
        "-executeMethod", "OutrunAftermath.Client.Editor.CharacterRuntimeAcceptance.RunAcceptanceBatchMode", `
        "-logFile", "`"$logPath`"" `
        -PassThru -NoNewWindow -Wait

    Write-Host "Acceptance capture exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Acceptance capture failed with exit code $($proc.ExitCode). Check $logPath"
        exit $proc.ExitCode
    }
}

# 3. Run Character Validation Pipeline
if (-not $SkipValidation) {
    Write-Host "`n=== STEP 3: Running Character Validation Pipeline ===" -ForegroundColor Cyan
    $logPath = "$clientDir\validation_c135.log"
    $proc = Start-Process -FilePath $unityExe `
        -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$clientDir`"", `
        "-executeMethod", "OutrunAftermath.Client.Editor.CharacterValidationPipeline.RunValidationBatchMode", `
        "-logFile", "`"$logPath`"" `
        -PassThru -NoNewWindow -Wait

    Write-Host "Validation pipeline exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Validation pipeline failed with exit code $($proc.ExitCode). Check $logPath"
        exit $proc.ExitCode
    }
}

Write-Host "`n=== ALL ACCEPTANCE STEPS COMPLETED SUCCESSFULLY ===" -ForegroundColor Green
exit 0
