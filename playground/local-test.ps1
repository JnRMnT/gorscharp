Param(
    [string]$Sample = "01-hello.gör"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$samplePath = Join-Path $PSScriptRoot $Sample

if (!(Test-Path $samplePath)) {
    throw "Sample not found: $samplePath"
}

Write-Host "[1/4] Build..."
dotnet build (Join-Path $repo "GorSharp.slnx")

Write-Host "[2/4] Test..."
dotnet test (Join-Path $repo "GorSharp.slnx") --no-build

Write-Host "[3/4] Transpile sample..."
dotnet run --project (Join-Path $repo "src/GorSharp.CLI/GorSharp.CLI.csproj") -- transpile $samplePath

Write-Host "[4/4] Run sample..."
dotnet run --project (Join-Path $repo "src/GorSharp.CLI/GorSharp.CLI.csproj") -- run $samplePath

Write-Host "Local test complete."
