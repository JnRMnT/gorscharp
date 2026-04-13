$projectRoot = "C:\Personal\Genel\Projeler\Gör#"
$serverExe = Join-Path $projectRoot "src\GorSharp.LanguageServer\bin\Release\net10.0\gorsharp-lsp.exe"
$logDir = "$env:LOCALAPPDATA\GorSharp\Logs"

Write-Host "Server: $serverExe"
Write-Host "Exists: $(Test-Path $serverExe)"

if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

Get-ChildItem $logDir -Filter "*.log" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Starting server..."

$process = Start-Process -FilePath $serverExe -PassThru -WindowStyle Hidden -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "PID: $($process.Id)"
    Start-Sleep -Seconds 3
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "`nLog files:"
Get-ChildItem $logDir -File | ForEach-Object { Write-Host "  $($_.Name) - $((Get-Item $_.FullName).Length) bytes" }

$lspLog = Join-Path $logDir "lsp-server.log"
if (Test-Path $lspLog) {
    Write-Host "`n=== First 50 lines ==="
    Get-Content $lspLog -Head 50
} else {
    Write-Host "`nNo lsp-server.log"
}
