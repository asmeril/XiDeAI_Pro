# XiDeAI Pro - Version Update Script (Full Automation)
# Usage: .\update-version.ps1 -Version "3.8.0"

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$today = Get-Date -Format "yyyy-MM-dd"
$todayTurkish = Get-Date -Format "dd MMMM yyyy"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  XiDeAI Pro v$Version - Version Update" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Update .csproj
$csprojPath = "XiDeAI_Pro.csproj"
if (Test-Path $csprojPath) {
    $content = Get-Content $csprojPath -Raw -Encoding UTF8
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
    $content = $content -replace '<AssemblyVersion>[\d\.]+\.0</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[\d\.]+\.0</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
    Set-Content $csprojPath $content -Encoding UTF8
    Write-Host "OK Updated $csprojPath" -ForegroundColor Green
}
else {
    Write-Host "ERR $csprojPath not found" -ForegroundColor Red
}

# 2. Update setup.iss
$issPath = "setup.iss"
if (Test-Path $issPath) {
    $content = Get-Content $issPath -Raw -Encoding UTF8
    $content = $content -replace '#define MyAppVersion "[\d\.]+"', "#define MyAppVersion `"$Version`""
    Set-Content $issPath $content -Encoding UTF8
    Write-Host "OK Updated $issPath" -ForegroundColor Green
}
else {
    Write-Host "ERR $issPath not found" -ForegroundColor Red
}

# 3. Update PROJECT_INDEX.md
$indexPath = "PROJECT_INDEX.md"
if (Test-Path $indexPath) {
    $content = Get-Content $indexPath -Raw -Encoding UTF8
    $content = $content -replace '\*\*Version:\*\* [\d\.]+ \(Live\)', "**Version:** $Version (Live)"
    $content = $content -replace '\*\*Last Updated:\*\* [\d-]+', "**Last Updated:** $today"
    Set-Content $indexPath $content -Encoding UTF8
    Write-Host "OK Updated $indexPath" -ForegroundColor Green
}

# 4. Rename and update PROJECT_MANIFEST
$manifestFiles = Get-ChildItem -Filter "PROJECT_MANIFEST_v*.md" | Sort-Object Name -Descending
if ($manifestFiles.Count -gt 0) {
    $oldManifest = $manifestFiles[0].Name
    $newManifest = "PROJECT_MANIFEST_v$Version.md"
    
    if ($oldManifest -ne $newManifest) {
        Rename-Item $oldManifest $newManifest -Force
        Write-Host "OK Renamed $oldManifest -> $newManifest" -ForegroundColor Green
    }
    
    # Update content inside manifest
    $content = Get-Content $newManifest -Raw -Encoding UTF8
    $content = $content -replace 'Project Manifest \(v[\d\.]+\)', "Project Manifest (v$Version)"
    $content = $content -replace '\*\*Last Updated:\*\* [\d-]+', "**Last Updated:** $today"
    Set-Content $newManifest $content -Encoding UTF8
    Write-Host "OK Updated $newManifest content" -ForegroundColor Green
}

# 5. Update PROJECT_DIARY.md - Add version section header if not exists
$diaryPath = "PROJECT_DIARY.md"
if (Test-Path $diaryPath) {
    $content = Get-Content $diaryPath -Raw -Encoding UTF8
    
    # Check if this version section already exists
    if ($content -notmatch "v$Version") {
        # Simplified string to avoid encoding issues
        $newSection = "`r`n`r`n---`r`n`r`n## $todayTurkish`r`n`r`n### v$Version Release`r`n`r`n> TODO: Release notes eklenecek.`r`n`r`n"
        
        # Append to end of file
        $content = $content.TrimEnd() + $newSection
        Set-Content $diaryPath $content -Encoding UTF8
        Write-Host "OK Added v$Version section to $diaryPath" -ForegroundColor Green
    }
    else {
        Write-Host "INFO v$Version section already exists in $diaryPath" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  OK Version update complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
