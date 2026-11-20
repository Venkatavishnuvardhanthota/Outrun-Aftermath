$proc = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" `
    -ArgumentList "-quit", "-batchmode", "-projectPath", "`"D:\projects\outrun-aftermath\client`"", `
    "-executeMethod", "OutrunAftermath.Client.Editor.CaptureCharacterVisuals.CaptureBatchMode", `
    "-logFile", "`"D:\projects\outrun-aftermath\client\capture_c134.log`"" `
    -PassThru -NoNewWindow -Wait
Write-Host "Unity capture exit code: $($proc.ExitCode)"
exit $proc.ExitCode
