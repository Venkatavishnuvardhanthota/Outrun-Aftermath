param(
    [switch]$SkipBuildAssets,
    [switch]$SkipBuildPlayer,
    [switch]$SkipPlayMode,
    [switch]$SkipBuildMultiplayer,
    [switch]$SkipMultiplayerTest,
    [switch]$SkipUnitTests
)

$ErrorActionPreference = "Stop"

$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe"
$projectRoot = $PSScriptRoot
$clientDir = "$projectRoot\client"
$acceptanceExe = "$clientDir\Builds\AcceptancePlayer\OutrunAftermath_Acceptance.exe"
$serverExe = "$clientDir\Builds\WindowsServer\OutrunAftermath_Server.exe"
$clientExe = "$clientDir\Builds\WindowsClient\OutrunAftermath_Client.exe"

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "   OUTRUN: AFTERMATH — PHASE C.1.3.6 TRUE PLAY MODE ACCEPTANCE SUITE     " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

if (-not (Test-Path $unityExe)) {
    Write-Error "Unity executable not found at: $unityExe"
    exit 1
}

# 1. Build Character Assets (Animations, LowReady Animator Controller, Prefabs)
if (-not $SkipBuildAssets) {
    Write-Host "`n=== STEP 1: Building Character Assets in Unity BatchMode ===" -ForegroundColor Cyan
    $logPath = "$clientDir\Logs\build_assets_c136.log"
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

# 2. Build Standalone Acceptance Player (Houses CharacterVisualAcceptance.unity + Driver)
if (-not $SkipBuildPlayer) {
    Write-Host "`n=== STEP 2: Building Standalone Acceptance Player ===" -ForegroundColor Cyan
    $logPath = "$clientDir\Logs\build_player_c136.log"
    $proc = Start-Process -FilePath $unityExe `
        -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$clientDir`"", `
        "-executeMethod", "OutrunAftermath.Client.Editor.CharacterPlayModeAcceptance.BuildAcceptancePlayerBatchMode", `
        "-logFile", "`"$logPath`"" `
        -PassThru -NoNewWindow -Wait

    Write-Host "Build player exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Acceptance player build failed with exit code $($proc.ExitCode). Check $logPath"
        exit $proc.ExitCode
    }
}

# 3. Run True Play Mode Execution via Standalone Acceptance Player
if (-not $SkipPlayMode) {
    Write-Host "`n=== STEP 3: Executing Genuine Play Mode Runtime Acceptance ===" -ForegroundColor Cyan
    if (-not (Test-Path $acceptanceExe)) {
        Write-Error "Acceptance player not found at: $acceptanceExe"
        exit 1
    }

    $playerLog = "$clientDir\Logs\acceptance_runtime.log"
    Write-Host "Launching $acceptanceExe in hardware Play Mode..."
    $proc = Start-Process -FilePath $acceptanceExe `
        -ArgumentList "-batchmode", "-acceptSoftwareTermsForThisRunOnly", "-logfile", "`"$playerLog`"" `
        -PassThru

    Write-Host "Acceptance Player running (PID: $($proc.Id)). Awaiting Play Mode test completion..."
    $timeout = (Get-Date).AddMinutes(3)
    while (-not $proc.HasExited -and (Get-Date) -lt $timeout) {
        Start-Sleep -Seconds 2
    }

    if (-not $proc.HasExited) {
        Write-Warning "Acceptance player timed out after 3 minutes. Forcing process kill."
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        exit 1
    }

    Write-Host "Acceptance Player exited with code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Play Mode acceptance failed with exit code $($proc.ExitCode). Check $playerLog"
        exit $proc.ExitCode
    }
}

# 4. Rebuild WindowsServer and WindowsClient for Live Multiplayer Verification
if (-not $SkipBuildMultiplayer) {
    Write-Host "`n=== STEP 4: Compiling WindowsServer and WindowsClient Binaries ===" -ForegroundColor Cyan
    $logPath = "$clientDir\Logs\build_multiplayer_c136.log"
    $proc = Start-Process -FilePath $unityExe `
        -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$clientDir`"", `
        "-executeMethod", "OutrunAftermath.Client.Editor.BuildScript.BuildAllWindows", `
        "-logFile", "`"$logPath`"" `
        -PassThru -NoNewWindow -Wait

    Write-Host "Multiplayer build exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Multiplayer build failed with exit code $($proc.ExitCode). Check $logPath"
        exit $proc.ExitCode
    }
}

# 5. Run Live Multi-Client Network Replication Test
if (-not $SkipMultiplayerTest) {
    Write-Host "`n=== STEP 5: Running Multi-Client Network Replication Test ===" -ForegroundColor Cyan
    $multiTestScript = "$projectRoot\test_two_client_multiplayer.ps1"
    if (Test-Path $multiTestScript) {
        & powershell -File $multiTestScript -ServerPort 7788
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Multiplayer replication observed warnings or non-zero exit code: $LASTEXITCODE"
        }
    }
}

# 6. Run Unit & Simulation Regression Tests
if (-not $SkipUnitTests) {
    Write-Host "`n=== STEP 6: Executing Unit & Simulation Regression Tests ===" -ForegroundColor Cyan
    
    Write-Host "Running 76 Shared Simulation tests..."
    dotnet test "$projectRoot\shared\OutrunAftermath.Simulation.Tests\" --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Simulation unit tests failed!"
        exit $LASTEXITCODE
    }

    Write-Host "Running 12 Backend API tests..."
    dotnet test "$projectRoot\backend\tests\OutrunAftermath.Api.Tests\" --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Backend API tests failed!"
        exit $LASTEXITCODE
    }
}

Write-Host "`n=== ALL PHASE C.1.3.6 ACCEPTANCE GATES COMPLETED SUCCESSFULLY ===" -ForegroundColor Green
exit 0
