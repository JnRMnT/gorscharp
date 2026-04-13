#!/usr/bin/env pwsh
# Test script to check LSP server capabilities by sending Initialize request

$projectRoot = "c:\Personal\Genel\Projeler\Gör#"
$serverExe = Join-Path $projectRoot "src\GorSharp.LanguageServer\bin\Release\net10.0\gorsharp-lsp.exe"

if (-not (Test-Path $serverExe)) {
    Write-Error "Server executable not found: $serverExe"
    exit 1
}

Write-Host "Starting LSP server: $serverExe"
Write-Host ""

# LSP Initialize request (JSON-RPC over stdio)
$initRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        processId = $PID
        clientInfo = @{
            name = "TestClient"
            version = "1.0"
        }
        rootUri = "file:///c:/Personal/Genel/Projeler/Gör%23/"
        capabilities = @{
            workspace = @{
                didChangeConfiguration = @{
                    dynamicRegistration = $true
                }
            }
            textDocument = @{
                hover = @{
                    dynamicRegistration = $true
                }
                completion = @{
                    dynamicRegistration = $true
                }
                definition = @{
                    dynamicRegistration = $true
                }
                references = @{
                    dynamicRegistration = $true
                }
                semanticTokens = @{
                    dynamicRegistration = $true
                }
                inlayHint = @{
                    dynamicRegistration = $true
                }
                publishDiagnostics = @{
                    dynamicRegistration = $true
                }
            }
        }
        trace = "off"
    }
}

$json = $initRequest | ConvertTo-Json -Depth 10
$bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
$contentLength = $bytes.Length

# Remove the trailing newline that ConvertTo-Json adds (LSP expects raw JSON)
$jsonString = $initRequest | ConvertTo-Json -Depth 10 -Compress
$bytes = [System.Text.Encoding]::UTF8.GetBytes($jsonString)
$contentLength = $bytes.Length

# LSP Content-Length header format
$header = "Content-Length: $contentLength`r`n`r`n"
$headerBytes = [System.Text.Encoding]::UTF8.GetBytes($header)

Write-Host "Sending Initialize request..."
Write-Host "Content-Length: $contentLength"
Write-Host ""

# Start server process
$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = $serverExe
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.RedirectStandardInput = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.RedirectStandardError = $true
$process.StartInfo.CreateNoWindow = $true

$process.Start() | Out-Null

# Write request to stdin
$stdin = $process.StandardInput.BaseStream
$stdout = $process.StandardOutput
$stderr = $process.StandardError

$stdin.Write($headerBytes, 0, $headerBytes.Length)
$stdin.Write($bytes, 0, $bytes.Length)
$stdin.Flush()

Write-Host "Request sent. Waiting for response..."
Write-Host ""

# Read response with timeout
$timeout = 5000  # 5 seconds
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$responseBuffer = ""

try {
    while ($sw.ElapsedMilliseconds -lt $timeout) {
        if ($stdout.Peek() -ge 0) {
            $char = $stdout.Read()
            $responseBuffer += [char]$char
            
            # Check if we have a complete response (simple heuristic)
            if ($responseBuffer.Contains("`"result`"") -and $responseBuffer.Contains("}")) {
                break
            }
        }
        Start-Sleep -Milliseconds 10
    }
}
catch {
    Write-Host "Error reading response: $_"
}

Write-Host "=== Server Response ===" 
Write-Host $responseBuffer

# Extract capabilities
if ($responseBuffer -match '"inlayHintProvider"') {
    Write-Host ""
    Write-Host "✓ inlayHintProvider IS PRESENT in server capabilities"
} else {
    Write-Host ""
    Write-Host "✗ inlayHintProvider NOT FOUND in server capabilities"
}

if ($responseBuffer -match '"semanticTokensProvider"') {
    Write-Host "✓ semanticTokensProvider is present"
}

# Cleanup
$process.Kill()
$process.Dispose()

Write-Host ""
Write-Host "Test complete."
