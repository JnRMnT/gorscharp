$serverExe = "C:\Personal\Genel\Projeler\Gör#\src\GorSharp.LanguageServer\bin\Release\net10.0\gorsharp-lsp.exe"
$logDir = "$env:LOCALAPPDATA\GorSharp\Logs"

if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

Get-ChildItem $logDir -Filter "*.log" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Starting: $serverExe"

$process = Start-Process -FilePath $serverExe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 3

Write-Host "Log files:"
Get-ChildItem $logDir -File | ForEach-Object { Write-Host "  $($_.Name)" }

$lspLog = Join-Path $logDir "lsp-server.log"
if (Test-Path $lspLog) {
    Write-Host "`n=== First 50 lines of lsp-server.log ==="
    Get-Content $lspLog -Head 50
} else {
    Write-Host "`nNo lsp-server.log found"
}

Stop-Process -ImageName "gorsharp-lsp" -Force -ErrorAction SilentlyContinue
Write-Host "`nDone."
