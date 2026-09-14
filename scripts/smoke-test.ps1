$exe = Join-Path $PSScriptRoot "..\publish\StreamDesk.exe"
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 8
if ($p.HasExited) {
    Write-Host ("EXITED code=" + $p.ExitCode)
    exit 1
} else {
    Write-Host "RUNNING ok (main window did not crash within 8s)"
    $p.Kill()
    exit 0
}
