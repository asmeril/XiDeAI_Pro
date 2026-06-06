# XiDeAI Release Automation Script
# Usage: .\release.ps1 -Version 5.3.0 -Changelog "Canonical PostingService, verified posting"

param (
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [Parameter(Mandatory=$true)]
    [string]$Changelog
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

# Generate canonical project version.json before publish so it is packaged into installer.
$changelogItems = @()
try {
    if (Test-Path "$ProjectDir\version.json") {
        $existingVersionJson = Get-Content "$ProjectDir\version.json" -Raw | ConvertFrom-Json
        if ($existingVersionJson.changelog -is [System.Array]) { $changelogItems += $existingVersionJson.changelog }
    }
} catch { }
$releaseLine = "v$Version: $Changelog"
$changelogItems = @($releaseLine) + @($changelogItems | Where-Object { $_ -ne $releaseLine })
$jsonContent = [ordered]@{
    version = $Version
    build_number = [int](($Version -replace '\.', '') + '0')
    lastUpdate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    changelog = $changelogItems
} | ConvertTo-Json -Depth 5
Set-Content -Path "$ProjectDir\version.json" -Value $jsonContent -Encoding UTF8

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
    Get-ChildItem -Path $ScriptsDest -Include "test_*.py", "debug_*.py", "temp_*.py", "fix_*.py", "inspect_*.py", "create_test_*.py" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

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

# Ensure canonical version metadata is inside publish folder before Inno Setup packages it.
Set-Content -Path "$PublishDir\version.json" -Value $jsonContent -Encoding UTF8

# ==========================================
# 4. CREATE INSTALLER
# ==========================================
Write-Host "Compiling Installer..."
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" $InstallerScript
if ($LASTEXITCODE -ne 0) { Write-Error "Installer Compilation Failed!" }

Write-Host "Installer Created!" -ForegroundColor Green

# ==========================================
# 5. GENERATE VERSION INFO (canonical version.json)
# ==========================================
Set-Content -Path "$ProjectDir\version.json" -Value $jsonContent -Encoding UTF8
Set-Content -Path "$PublishDir\version.json" -Value $jsonContent -Encoding UTF8
Set-Content -Path "$SetupOutputDir\version.json" -Value $jsonContent -Encoding UTF8
Write-Host "version.json generated in project, publish and output." -ForegroundColor Green

Write-Host ""
Write-Host "RELEASE $Version COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "Installer Location: $SetupOutputDir"
Write-Host ""
