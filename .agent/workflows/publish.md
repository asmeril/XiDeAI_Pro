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
> - Normal güncelleme: sadece `PATCH` artar → `4.7.8` → `4.7.9`
> - **PATCH 9'a ulaştığında:** bir sonraki sürümde `MINOR` artar, `PATCH` 0'a sıfırlanır → `4.7.9` → `4.8.0` (4.7.10 OLMAZ)
> - Büyük mimari değişikliklerde: `MAJOR` artar → `5.0.0`

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

## 2. Clean & Build
```powershell
rm -r Dist -Force -ErrorAction SilentlyContinue
dotnet publish XiDeAI_Pro.csproj -c Release -r win-x64 --self-contained true
```
> **⚠️ KRİTİK:** `-o Dist\publish` argümanını veya `copy-publish-assets.ps1` kullanma! Bunlar birbiriyle çakışarak eski dosyaları yeni build'in üstüne yazar. PostPublish eventi (csproj içindeki) her şeyi `Dist\publish` klasörüne otomatik ve güvenli bir biçimde taşır.

## 4. Generate Setup
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

## 5. Verify
`Output\XiDeAI_v{VERSION}_Setup.exe` dosyasını kontrol et.
