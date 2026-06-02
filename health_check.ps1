# XiDeAI Health Check
$ErrorActionPreference = "Stop"

Write-Host "Running Health Checks..." -ForegroundColor Cyan

# 1. File Presence
$ProjectDir = $PSScriptRoot
$files = @(
    (Join-Path $ProjectDir "xideai_icon.ico"),
    (Join-Path $ProjectDir "Scripts\social_intel.py"),
    (Join-Path $ProjectDir "version.json")
)
foreach ($f in $files) {
    if (-not (Test-Path $f)) { Write-Error "Missing: $f" }
    else { Write-Host "OK: $f" -ForegroundColor Green }
}

# 2. Build Check
Write-Host "Building..." -ForegroundColor Yellow
$res = dotnet build (Join-Path $ProjectDir "XiDeAI_Pro.csproj") -c Debug -v q --nologo
if ($LASTEXITCODE -eq 0) { Write-Host "Build OK" -ForegroundColor Green }
else { Write-Error "Build Failed" }

Write-Host "SYSTEM HEALTHY" -ForegroundColor Cyan
