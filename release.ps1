# XiDeAI Release Automation Script
# Usage: .\release.ps1 -Version 3.8.0 -Changelog "HIVE Protocol Phase 1 & 2"

param (
    [string]$Version = "5.0.0",
    [string]$Changelog = "v5.0.0: Full Local AI Transition (Gemma 4), Vision Analysis Fix, Improved Error Reporting, Build Optimization (~50MB)"
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$InstallerScript = "$ProjectDir\setup.iss"
$CsprojFile = "$ProjectDir\XiDeAI_Pro.csproj"
$ProgramFile = "$ProjectDir\Program.cs"
$MainFormFile = "$ProjectDir\MainForm.cs"
$DistDir = "$ProjectDir\Dist"
$PublishDir = "$DistDir\publish"
$SetupOutputDir = "$ProjectDir\Output"

Write-Host "STARTING XiDeAI Release Process for v$Version..." -ForegroundColor Cyan

# ==========================================
# 0. KILL SWITCH (ENSURE CLEAN STATE)
# ==========================================
Write-Host "Killing existing processes to prevent file locks..." -ForegroundColor Yellow
# Stop processes gracefully, ignore if not found
Get-Process "XiDeAI*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process "ISCC"   -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean Dist folder if exists
if (Test-Path $DistDir) {
    Write-Host "Cleaning Dist folder..." -ForegroundColor Yellow
    Remove-Item -Path $DistDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

# ==========================================
# 1. PRE-FLIGHT CHECKS (FAIL FAST)
# ==========================================
Write-Host "Running Pre-Flight Checks..." -ForegroundColor Yellow

# Check 1: Hardcoded Version in Program.cs
$programContent = Get-Content $ProgramFile -Raw
if ($programContent.Contains("Logger.Sys(`"Uygulama başlatıldı (v2.")) {
    Write-Error "CRITICAL: Hardcoded version string found in Program.cs! Please use Assembly version."
}

# Check 2 updated: Just warn if manual paths exist, don't fail as we might have legacy code
$mainFormContent = Get-Content $MainFormFile -Raw
if ($mainFormContent -match 'Path\.Combine\(.*scriptsDir.*screenshot\.py') {
    Write-Warning "WARNING: Manual script path construction detected in MainForm.cs. Ensure paths are correct."
}

Write-Host "Pre-Flight Checks Passed!" -ForegroundColor Green

# ==========================================
# 2. UPDATE VERSIONS
# ==========================================
Write-Host "Updating version numbers..."

# Update .csproj — string replace yerine XML parser kullanmıyoruz.
# [xml].Save() bazı PropertyGroup öğelerini (PublishSingleFile vb.) silebiliyordu.
$csprojContent = [System.IO.File]::ReadAllText($CsprojFile)
$csprojContent = $csprojContent -replace '<Version>[\d\.]+</Version>',         "<Version>$Version</Version>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>[\d\.]+</FileVersion>',  "<FileVersion>$Version.0</FileVersion>"
[System.IO.File]::WriteAllText($CsprojFile, $csprojContent)

# Update .iss (Inno Setup)
$issContent = Get-Content $InstallerScript -Raw
$issContent = $issContent -replace '#define MyAppVersion "[\d\.]+"', "#define MyAppVersion `"$Version`""
Set-Content -Path $InstallerScript -Value $issContent

Write-Host "Versions updated to $Version" -ForegroundColor Green

# ==========================================
# 3. BUILD PROJECT
# ==========================================
Write-Host "Cleaning project..."
dotnet clean $CsprojFile -c Release
if (Test-Path "$ProjectDir\obj") { Remove-Item "$ProjectDir\obj" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$ProjectDir\bin") { Remove-Item "$ProjectDir\bin" -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "Building project (Release - Self-Contained, No Trimming)..."
dotnet publish $CsprojFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true -o $PublishDir
if ($LASTEXITCODE -ne 0) { Write-Error "Build Failed!" }

# CLEANUP BLOAT (v5.0.0 Optimization)
Write-Host "Cleaning up build bloat (locales, pdb, xml)..." -ForegroundColor Yellow
# Remove satellite assemblies (localizations we don't use, saving ~30MB)
Get-ChildItem -Path $PublishDir -Directory | Where-Object { $_.Name.Length -eq 2 -or $_.Name -match "^[a-z]{2}-" } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
# Remove debug and doc files
Get-ChildItem -Path $PublishDir -Include *.pdb, *.xml, *.devlog -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Build Success!" -ForegroundColor Green

# COPY SCRIPTS TO PUBLISH FOLDER
Write-Host "Copying Python Scripts..."
$ScriptsDest = "$PublishDir\Scripts"
New-Item -ItemType Directory -Path $ScriptsDest -Force | Out-Null
if (Test-Path "$ProjectDir\Scripts") {
    # Copy contents only (to avoid Scripts\Scripts)
    Copy-Item -Path "$ProjectDir\Scripts\*" -Destination $ScriptsDest -Recurse -Force
    
    # CLEANUP: Remove screenshots and cache
    Write-Host "Cleaning up unnecessary files (screenshots, cache)..." -ForegroundColor Yellow
    Remove-Item -Path "$ScriptsDest\screenshots" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$ScriptsDest\__pycache__" -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $ScriptsDest -Include "__pycache__", "*.pyc", "*.png", "*.log" -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "✅ Scripts folder copied and cleaned." -ForegroundColor Green
}
else {
    Write-Warning "⚠️ Scripts folder not found!"
}

# COPY CONFIG TO PUBLISH FOLDER
Write-Host "Copying Config Folder..."
if (Test-Path "$ProjectDir\Config") {
    # Copy contents including subfolders
    New-Item -ItemType Directory -Path "$PublishDir\Config" -Force | Out-Null
    Copy-Item -Path "$ProjectDir\Config\*" -Destination "$PublishDir\Config" -Recurse -Force
    Write-Host "✅ Config folder copied." -ForegroundColor Green
}
else {
    Write-Warning "⚠️ Config folder not found!"
}

# ==========================================
# 4. CREATE INSTALLER
# ==========================================
Write-Host "Compiling Installer..."
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" $InstallerScript
if ($LASTEXITCODE -ne 0) { Write-Error "Installer Compilation Failed!" }

Write-Host "Installer Created!" -ForegroundColor Green

# ==========================================
# 5. GENERATE UPDATE INFO (version.json)
# ==========================================
$jsonPath = "$SetupOutputDir\version.json"
$date = Get-Date -Format "yyyy-MM-dd"
$jsonContent = @{
    version     = $Version
    releaseDate = $date
    changelog   = $Changelog
} | ConvertTo-Json -Depth 2

Set-Content -Path $jsonPath -Value $jsonContent
Write-Host "version.json generated at $jsonPath" -ForegroundColor Green

Write-Host ""
Write-Host "RELEASE $Version COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "Installer Location: $SetupOutputDir"
Write-Host ""
