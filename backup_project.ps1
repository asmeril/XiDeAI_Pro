# XiDeAI Pro Backup Script
$ProjectDir = "d:\Projects\XiDeAI_Pro"
$BackupDir = "d:\Projects\XiDeAI_Pro_Backups"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupName = "XiDeAI_Pro_Backup_$Timestamp.zip"

if (!(Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir
}

Write-Host "Creating backup: $BackupName ..." -ForegroundColor Cyan

# Use Git's archive command to only backup tracked source files (ignoring bin, obj, logs etc.)
Set-Location $ProjectDir
git archive --format=zip HEAD -o "$BackupDir\$BackupName"

if (Test-Path "$BackupDir\$BackupName") {
    Write-Host "✅ Backup created successfully at $BackupDir" -ForegroundColor Green
}
else {
    Write-Host "❌ Backup failed!" -ForegroundColor Red
}
