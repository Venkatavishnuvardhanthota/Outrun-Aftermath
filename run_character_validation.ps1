param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe",
    [string]$ProjectPath = "$PSScriptRoot\client",
    [string]$MethodName = "OutrunAftermath.Client.Editor.CharacterValidationPipeline.RunValidationBatchMode",
    [string]$LogFile = "$PSScriptRoot\client\character_validation.log"
)

Write-Host "Running Character Validation Audit: $MethodName on $ProjectPath..."
$proc = Start-Process -FilePath $UnityPath -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$ProjectPath`"", "-executeMethod", $MethodName, "-logFile", "`"$LogFile`"" -PassThru -NoNewWindow -Wait
Write-Host "Process finished with code $($proc.ExitCode)"
exit $proc.ExitCode
