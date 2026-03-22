# Post-Publish Asset Sync Script
# Bu script csproj PostPublish tarafindan ARTIK CAGIRILMIYOR.
# csproj kendi PostPublish hedefinde dogrudan robocopy kullanir:
#   Scripts  -> $(PublishDir)Scripts
#   Config   -> $(PublishDir)Config
#   $(PublishDir) -> Dist\publish
#
# Bu scripti elle calistirmak istersen:
#   Set-Location D:\MEGA\XiDeAI_Pro
#   .\copy-publish-assets.ps1
#
# Tam publish + setup sureci:
#   dotnet publish -c Release -r win-x64 --self-contained false
#   & 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' setup.iss
#   # Uretilen setup.exe'yi calistir

param(
    [string]$PublishDir  = "bin\Release\net8.0-windows\win-x64\publish",
    [string]$DistDir     = "Dist\publish"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$src    = Join-Path $root $PublishDir
$dist   = Join-Path $root $DistDir
$scripts = Join-Path $root "Scripts"
$config  = Join-Path $root "Config"

if (-not (Test-Path $src)) {
    Write-Host "HATA: $src bulunamadi. Once 'dotnet publish' calistirin." -ForegroundColor Red
    exit 1
}

# Scripts -> PublishDir\Scripts
robocopy $scripts "$src\Scripts" /MIR /IS /IT /NFL /NDL /NJH /NJS /NC /NS /NP
Write-Host "  ✅ Scripts -> $src\Scripts" -ForegroundColor Green

# Config -> PublishDir\Config
if (Test-Path $config) {
    robocopy $config "$src\Config" /MIR /IS /IT /NFL /NDL /NJH /NJS /NC /NS /NP
    Write-Host "  ✅ Config  -> $src\Config" -ForegroundColor Green
}

# PublishDir -> Dist\publish
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist -Force | Out-Null }
robocopy $src $dist /MIR /IS /IT /NFL /NDL /NJH /NJS /NC /NS /NP
Write-Host "  ✅ $src -> $dist" -ForegroundColor Green

Write-Host ""
Write-Host "Sonraki adim:" -ForegroundColor Cyan
Write-Host "  & 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' setup.iss" -ForegroundColor DarkCyan
