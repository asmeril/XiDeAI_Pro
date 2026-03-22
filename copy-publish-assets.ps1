# Post-Publish Asset Copy Script
# Automatically copy Python scripts and Config files after dotnet publish

param(
    [string]$PublishDir = "Dist\publish",
    [string]$BuildOutputDir = "bin\Release\net8.0-windows\win-x64\publish"
)

Write-Host "Post-publish assets kopyalaniyor..." -ForegroundColor Cyan

# --- Adim 0: dotnet publish ciktisindan Dist\publish\ klasorunu guncelle ---
if (Test-Path $BuildOutputDir) {
    if (-not (Test-Path $PublishDir)) {
        New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
    }
    Write-Host "Build output Dist\publish\ ile senkronize ediliyor: $BuildOutputDir -> $PublishDir" -ForegroundColor Cyan
    robocopy $BuildOutputDir $PublishDir /MIR /NFL /NDL /NJH /NJS /NC /NS /NP
    if ($LASTEXITCODE -le 3) {
        Write-Host "Build output senkronizasyonu tamamlandi" -ForegroundColor Green
    } else {
        Write-Host "UYARI: robocopy cikis kodu $LASTEXITCODE" -ForegroundColor Yellow
    }
} else {
    Write-Host "UYARI: Build output dizini bulunamadi: $BuildOutputDir" -ForegroundColor Yellow
    Write-Host "Once 'dotnet publish' calistirin!" -ForegroundColor Yellow
}

# Copy Scripts
$scriptsSource = "Scripts"
$scriptsDest = "$PublishDir\Scripts"

if (Test-Path $scriptsSource) {
    if (-not (Test-Path $scriptsDest)) {
        New-Item -ItemType Directory -Path $scriptsDest -Force | Out-Null
    }
    Copy-Item "$scriptsSource\*.py" -Destination $scriptsDest -Force
    Write-Host "✅ Python scripts kopyalandı" -ForegroundColor Green
}
else {
    Write-Host "❌ Scripts klasörü bulunamadı" -ForegroundColor Red
}

# Copy Config
$configSource = "Config"
$configDest = "$PublishDir\Config"

if (Test-Path $configSource) {
    if (-not (Test-Path $configDest)) {
        New-Item -ItemType Directory -Path $configDest -Force | Out-Null
    }
    Copy-Item "$configSource\*" -Destination $configDest -Force -Recurse
    Write-Host "✅ Config files kopyalandı" -ForegroundColor Green
}
else {
    Write-Host "❌ Config klasörü bulunamadı" -ForegroundColor Red
}

# Copy Drivers (Optional but recommended)
$driversSource = "drivers"
$driversDest = "$PublishDir\drivers"

if (Test-Path $driversSource) {
    if (-not (Test-Path $driversDest)) {
        New-Item -ItemType Directory -Path $driversDest -Force | Out-Null
    }
    Copy-Item "$driversSource\*" -Destination $driversDest -Force -Recurse
    Write-Host "✅ Drivers kopyalandı" -ForegroundColor Green
}

Write-Host "✅ Post-publish tamamlandı" -ForegroundColor Green
