---
description: Build, Publish and Generate Setup for XiDeAI Pro
---
// turbo-all

## 0. Pre-Flight Check (CRITICAL)
**Yayınlamadan önce KESİNLİKLE kontrol et:**
1.  **Yeni Dosya Var mı?**
    - Eğer yeni bir `.cs` (Service/Form) veya `.py` script oluşturduysan, bunu `PROJECT_INDEX.md` -> **Services Map** tablosuna ekle.
2.  **Yeni Metot/Özellik Var mı?**
    - `PROJECT_INDEX.md` -> **Key Classes & Methods** bölümünü güncelle.
3.  **Manifest Güncellemesi:**
    - `PROJECT_MANIFEST_vX.X.X.md` içinde sürüm notları detaylı mı? Sadece başlık yetmez.

## 1. Update Version & Documentation (Single Command)

> **📌 Sürüm Numarası Kuralı:** Format `MAJOR.MINOR.PATCH`'tir.
> - Normal güncelleme: sadece `PATCH` artar → `5.0.1` → `5.0.2`
> - **PATCH 9'a ulaştığında:** `MINOR` artar, `PATCH` 0'a sıfırlanır → `5.0.9` → `5.1.0` (`5.0.10` OLMAZ)
> - **MINOR 9'a ulaştığında:** `MAJOR` artar, `MINOR` ve `PATCH` sıfırlanır → `5.9.9` → `6.0.0` (`5.10.x` OLMAZ)
> - Büyük mimari değişikliklerde de: `MAJOR` artar.
> - ⚠️ **MINOR ve PATCH hiçbir zaman çift haneye (10+) çıkmaz.**
> - 📌 **Mevcut seri:** `4.10.9` son `4.x` sürümüdür. Bir sonraki sürüm `5.0.0`'dan başlar.

```powershell
.\update-version.ps1 -Version "4.8.0"
```
**Otomatik güncellenenler:**
- `.csproj` (Version, AssemblyVersion, FileVersion)
- `setup.iss` (MyAppVersion)
- `PROJECT_INDEX.md` (Version + Last Updated)
- `PROJECT_MANIFEST` (Dosya adı + içerik)
- `PROJECT_DIARY.md` (Yeni versiyon başlığı eklenir)

⚠️ **Manuel:** `PROJECT_DIARY.md`'deki TODO kısmını gerçek release notes ile değiştir.

## 2. Clean & Build (Tek Komut — Önerilen)
```powershell
.\release.ps1 -Version "4.X.Y" -Changelog "Değişiklik açıklaması"
```
`release.ps1` şunları otomatik yapar: temizlik → versiyon güncelleme → publish → Scripts/Config kopyalama → ISCC setup oluşturma → version.json.

> **⚠️ KRİTİK — Bilinen Sorun (Giderildi v4.10.9):**
> `release.ps1` eski versiyonlarda csproj versiyonunu güncellemek için `[xml]` parser kullanıyordu.
> Bu, `PublishSingleFile=true` gibi ayarları **sessizce siliyordu** → setup boyutu 64MB yerine 49MB çıkıyordu.
> **Fix:** `release.ps1` artık `[System.IO.File]::ReadAllText` + string-replace kullanıyor. Bu sorunu bir daha yaşamamalısınız.

> **⚠️ KRİTİK — `PublishSingleFile` Korunmalı:**
> `XiDeAI_Pro.csproj` içinde `<PublishSingleFile>true</PublishSingleFile>` her zaman `true` olmalı.
> Doğru build: `XiDeAI_Pro.exe = ~65.6 MB` (single-file, self-contained)
> Doğru setup: `XiDeAI_v{VERSION}_Setup.exe = ~64 MB`
> Eğer setup **49 MB** çıkıyorsa `PublishSingleFile` silinmiş demektir — csproj kontrol et.

## 3. Manuel Build (Adım Adım)
```powershell
# 1. Publish
dotnet publish XiDeAI_Pro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true -o "Dist\publish"

# 2. Scripts & Config kopyala
Copy-Item -Path "Scripts\*" -Destination "Dist\publish\Scripts" -Recurse -Force
Copy-Item -Path "Config\*"  -Destination "Dist\publish\Config"  -Recurse -Force

# 3. Setup oluştur
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

## 4. Verify
```powershell
# EXE boyutu ~65.6 MB olmalı
Get-Item "Dist\publish\XiDeAI_Pro.exe" | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}

# Setup boyutu ~64 MB olmalı
Get-Item "Output\XiDeAI_v{VERSION}_Setup.exe" | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}
```
