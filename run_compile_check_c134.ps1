$proc = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" `
    -ArgumentList "-quit", "-batchmode", "-projectPath", "`"D:\projects\outrun-aftermath\client`"", `
    "-logFile", "`"D:\projects\outrun-aftermath\client\compile_check_c134.log`"" `
    -PassThru -NoNewWindow -Wait
Write-Host "Unity compile check exit code: $($proc.ExitCode)"
exit $proc.ExitCode
