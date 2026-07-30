# XiDeAI Pro - Kod Export Script'i
# Bu script tüm kaynak kodlarını AI eğitimi için tek bir klasöre toplar

param(
    [string]$OutputDir = ".\AI_Knowledge_Base"
)

Write-Host "XiDeAI Pro Kod Export Basliyor..." -ForegroundColor Cyan

# Klasör yapısını oluştur
$codebaseDir = Join-Path $OutputDir "codebase"
$docsDir = Join-Path $OutputDir "docs"

New-Item -ItemType Directory -Force -Path $codebaseDir | Out-Null
New-Item -ItemType Directory -Force -Path $docsDir | Out-Null

# C# dosyalarını kopyala
Write-Host "C# dosyalari kopyalaniyor..." -ForegroundColor Yellow
$csFiles = Get-ChildItem -Path "." -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | Where-Object {
    ($_.FullName -notlike "*\bin\*") -and ($_.FullName -notlike "*\obj\*") -and ($_.FullName -notlike "*\packages\*")
}
foreach ($file in $csFiles) {
    $relativePath = $file.FullName.Substring((Get-Location).Path.Length + 1).Replace("\", "_")
    Copy-Item $file.FullName -Destination (Join-Path $codebaseDir $relativePath) -Force
}

# Python script'lerini kopyala
Write-Host "Python dosyalari kopyalaniyor..." -ForegroundColor Yellow
$pyFiles = Get-ChildItem -Path ".\Scripts" -Recurse -Filter "*.py" -ErrorAction SilentlyContinue
foreach ($file in $pyFiles) {
    $relativePath = "Scripts_" + $file.Name
    Copy-Item $file.FullName -Destination (Join-Path $codebaseDir $relativePath) -Force
}

# Markdown dokümantasyonlarını kopyala
Write-Host "Dokumantasyon kopyalaniyor..." -ForegroundColor Yellow
$mdPatterns = @("PROJECT_INDEX.md", "PROJECT_DIARY.md", "PROJECT_MANIFEST*.md", "README.md")
foreach ($pattern in $mdPatterns) {
    Get-ChildItem -Path "." -Filter $pattern -ErrorAction SilentlyContinue | Copy-Item -Destination $docsDir -Force
}

# Config dosyalarını kopyala
Write-Host "Config dosyalari kopyalaniyor..." -ForegroundColor Yellow
Get-ChildItem -Path ".\Config" -Recurse -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -in @(".md", ".json", ".txt")
} | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $docsDir ("Config_" + $_.Name)) -Force
}

# İstatistikler
$csCount = (Get-ChildItem -Path $codebaseDir -Filter "*.cs" -ErrorAction SilentlyContinue | Measure-Object).Count
$pyCount = (Get-ChildItem -Path $codebaseDir -Filter "*.py" -ErrorAction SilentlyContinue | Measure-Object).Count
$docCount = (Get-ChildItem -Path $docsDir -ErrorAction SilentlyContinue | Measure-Object).Count

Write-Host ""
Write-Host "Export Tamamlandi!" -ForegroundColor Green
Write-Host "   Konum: $OutputDir"
Write-Host "   C# Dosyalari: $csCount"
Write-Host "   Python Dosyalari: $pyCount"
Write-Host "   Dokumantasyon: $docCount"
Write-Host ""
Write-Host "Sonraki adim: python setup_rag.py" -ForegroundColor Cyan
