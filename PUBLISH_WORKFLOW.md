# XiDeAI Pro - Build & Publish Workflow

## Build ve Publish

### Seçenek 1: Manual Publish (Önerilen - Her Zaman Çalışır)
```powershell
cd D:\Projects\XiDeAI_Pro
dotnet publish -c Release -o Dist\publish
powershell.exe -ExecutionPolicy Bypass -File copy-publish-assets.ps1 -PublishDir Dist\publish
```

### Seçenek 2: Otomatik Publish (Post-Build Script)
```powershell
cd D:\Projects\XiDeAI_Pro
dotnet publish -c Release -o Dist\publish
```
✅ Post-build event otomatik olarak Python scriptlerini ve Config dosyalarını kopyalar

## Publish İçeriği

| Klasör | İçerik | Otomatik Kopyala |
|--------|--------|------------------|
| `Dist/publish/` | XiDeAI_Pro.exe | ✅ dotnet publish |
| `Dist/publish/Scripts/` | Python scripts (screenshot.py, social_intel.py) | ✅ copy-publish-assets.ps1 |
| `Dist/publish/Config/` | Teknik rehberler (IndicatorGuide.md) | ✅ copy-publish-assets.ps1 |

## Güvenilirlik Kontrolleri

Her publish sonrası aşağıdaki dosyaların varlığını kontrol et:
```powershell
# Python scripts var mı?
Get-ChildItem D:\Projects\XiDeAI_Pro\Dist\publish\Scripts -Filter *.py

# Config dosyaları var mı?
Get-ChildItem D:\Projects\XiDeAI_Pro\Dist\publish\Config -Filter *.md

# Exe var mı?
Get-Item D:\Projects\XiDeAI_Pro\Dist\publish\XiDeAI_Pro.exe
```

## Hata Giderme

### Problem: Python scripts not found
**Çözüm:**
```powershell
cd D:\Projects\XiDeAI_Pro
powershell.exe -ExecutionPolicy Bypass -File copy-publish-assets.ps1 -PublishDir Dist\publish
```

### Problem: Post-build script çalışmadı
**Çözüm:** Manual olarak çalıştır:
```powershell
powershell.exe -ExecutionPolicy Bypass -File D:\Projects\XiDeAI_Pro\copy-publish-assets.ps1 -PublishDir D:\Projects\XiDeAI_Pro\Dist\publish
```

---

**Son Güncelleme:** 5 Ocak 2026 - Post-publish asset copy otomasyonu eklendi
