# XiDeAI Health Check
$ErrorActionPreference = "Stop"

Write-Host "Running Health Checks..." -ForegroundColor Cyan

# 1. File Presence
$files = @("d:\MEGA\IdealSmartNotifier\xideai_icon.ico", "d:\MEGA\IdealSmartNotifier\scripts\social_intel.py", "d:\MEGA\IdealSmartNotifier\version.json")
foreach ($f in $files) {
    if (-not (Test-Path $f)) { Write-Error "Missing: $f" }
    else { Write-Host "OK: $f" -ForegroundColor Green }
}

# 2. Build Check
Write-Host "Building..." -ForegroundColor Yellow
$res = dotnet build "d:\MEGA\IdealSmartNotifier\IdealSmartNotifier.csproj" -c Debug -v q --nologo
if ($LASTEXITCODE -eq 0) { Write-Host "Build OK" -ForegroundColor Green }
else { Write-Error "Build Failed" }

Write-Host "SYSTEM HEALTHY" -ForegroundColor Cyan
