# XiDeAI Release Automation Script
# Usage: .\release.ps1 -Version 2.2.0 -Changelog "Fixes X, Y, Z"

param (
    [string]$Version = "3.0.0",
    [string]$Changelog = "System-wide fixes, Expert AI Analysis, Thread robustness, and Link automation."
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$InstallerScript = "$ProjectDir\setup.iss"
$CsprojFile = "$ProjectDir\XiDeAI_Pro.csproj"
$ProgramFile = "$ProjectDir\Program.cs"
$MainFormFile = "$ProjectDir\MainForm.cs"
$DistDir = "$ProjectDir\Dist"
$PublishDir = "$DistDir\publish"
$SetupOutputDir = "$DistDir"

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

# Check 2: Manual Script Path in MainForm.cs
$mainFormContent = Get-Content $MainFormFile -Raw
if ($mainFormContent -match 'Path\.Combine\(.*scriptsDir.*screenshot\.py') {
    Write-Error "CRITICAL: Manual script path construction detected in MainForm.cs! Use DependencyManager."
}

# Check 3: REMOVED (Legacy check for CRYPTO string)

Write-Host "Pre-Flight Checks Passed!" -ForegroundColor Green

# ==========================================
# 2. UPDATE VERSIONS
# ==========================================
Write-Host "Updating version numbers..."

# Update .csproj
$csprojXml = [xml](Get-Content $CsprojFile)
$csprojXml.Project.PropertyGroup.Version = $Version
$csprojXml.Project.PropertyGroup.AssemblyVersion = "$Version.0"
$csprojXml.Project.PropertyGroup.FileVersion = "$Version.0"
$csprojXml.Save($CsprojFile)

# Update .iss (Inno Setup)
$issContent = Get-Content $InstallerScript -Raw
$issContent = $issContent -replace '#define MyAppVersion ".*?"', "#define MyAppVersion ""$Version"""
Set-Content -Path $InstallerScript -Value $issContent

Write-Host "Versions updated to $Version" -ForegroundColor Green

# ==========================================
# 3. BUILD PROJECT
# ==========================================
Write-Host "Building project (Release)..."
dotnet publish $CsprojFile -c Release -r win-x64 --self-contained true -o $PublishDir
if ($LASTEXITCODE -ne 0) { Write-Error "Build Failed!" }

Write-Host "Build Success!" -ForegroundColor Green

# COPY SCRIPTS TO PUBLISH FOLDER
Write-Host "Copying Python Scripts..."
Write-Host "Copying Python Scripts..."
if (Test-Path "$ProjectDir\Scripts") {
    Copy-Item -Path "$ProjectDir\Scripts" -Destination "$PublishDir\Scripts" -Recurse -Force
    Write-Host "✅ Scripts folder copied." -ForegroundColor Green
}
else {
    Write-Warning "⚠️ Scripts folder not found!"
}

# ==========================================
# 4. CREATE INSTALLER
# ==========================================
Write-Host "Compiling Installer..."
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /O"$SetupOutputDir" $InstallerScript
if ($LASTEXITCODE -ne 0) { Write-Error "Installer Compilation Failed!" }

Write-Host "Installer Created!" -ForegroundColor Green

# ==========================================
# 5. GENERATE UPDATE INFO (version.json)
# ==========================================
$jsonPath = "$DistDir\version.json"
$date = Get-Date -Format "yyyy-MM-dd"
$jsonContent = @{
    version     = $Version
    releaseDate = $date
    downloadUrl = "https://github.com/marvelariantomarbun-spec/MEGA/releases/download/v$Version/XiDeAI_Setup_v$Version.exe"
    changelog   = $Changelog
} | ConvertTo-Json -Depth 2

Set-Content -Path $jsonPath -Value $jsonContent
Write-Host "version.json generated at $jsonPath" -ForegroundColor Green

Write-Host "RELEASE $Version COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "Installer: $DistDir\XiDeAI_v$Version`_Setup.exe"
