#!/usr/bin/env pwsh
# Minimal LSP test - directly launch server and check if it logs

$serverExe = "C:\Personal\Genel\Projeler\Gör#\src\GorSharp.LanguageServer\bin\Release\net10.0\gorsharp-lsp.exe"
$logDir = "$env:LOCALAPPDATA\GorSharp\Logs"

# Ensure log directory exists
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Remove old logs
Get-ChildItem $logDir -Filter "*.log" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Starting language server: $serverExe"
Write-Host "Log directory: $logDir"
Write-Host ""

# Start server in background
$process = Start-Process -FilePath $serverExe -PassThru -WindowStyle Hidden
$pid = $process.Id

Write-Host "✓ Server started (PID=$pid)"
Write-Host "Waiting 3 seconds for initialization..."

Start-Sleep -Seconds 3

# Check if process is still running
$isRunning = Get-Process -Id $pid -ErrorAction SilentlyContinue
if ($isRunning) {
    Write-Host "✓ Server still running"
} else {
    Write-Host "✗ Server crashed immediately"
}

# Check logs
Write-Host ""
Write-Host "=== Log Files ===" 
Get-ChildItem $logDir -File | ForEach-Object { Write-Host "$($_.Name) - $((Get-Item $_.FullName).Length) bytes" }

$lspLog = Join-Path $logDir "lsp-server.log"
if (Test-Path $lspLog) {
    Write-Host ""
    Write-Host "=== LSP Server Log (first 40 lines) ==="
    Get-Content $lspLog | Select-Object -First 40
}

Write-Host ""
Write-Host "Killing server..."
Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue

Write-Host "Done."

