# PROJECT MANIFEST v4.9.3

**Release Date:** 2026-03-23
**Status:** Stable
**Focus:** Thread Media Attachment Fix, Lock Timeout Fix, Publish Pipeline Fix

## 🚀 Overview
Bu sürüm; thread gönderiminde grafik görselinin ilk tweete eklenmemesi, lock timeout nedeniyle thread'in yarıda kesilmesi ve publish sürecinde dosyaların eksik kopyalanması sorunlarını çözer.

## 🛠️ Changes

### 🐍 `Scripts/x_daemon.py`
- **`_post_single_tweet(driver, text, reply_to_url=None, media_path=None)`:** `media_path` parametresi eklendi.
- Metin yazıldıktan sonra `input[data-testid='fileInput']` selectorı ile medya yükleme bloğu eklendi (non-fatal exception handling ile).
- `cmd_post_thread` içinde 1. tweet çağrısına `media_path=media_path` geçirildi.

### 🐍 `Scripts/social_intel.py`
- **`_post_one(driver, text, reply_to_url=None, media_path=None)`:** `media_path` parametresi eklendi.
- Metin yazıldıktan sonra medya yükleme bloğu eklendi.
- `post_thread_chain` içinde 1. tweet çağrısına `media_path=media_path` geçirildi.

### 🐍 `Scripts/lock_manager.py`
- `acquire_lock(timeout_seconds=360)`: 180s → 360s olarak güncellendi. 4 tweet × ~60s = 240s+ gerektiren thread işlemleri için yeterli süre sağlandı.

### 🏢 `Services/SocialIntelService.cs`
- `RunPythonScript(args, null, null, 360)`: Thread timeout 180s → 360s olarak güncellendi.

### 🏢 `Services/PromptManager.cs`
- `GetShortThreadPromptWithHistory`: Tweet başına “EN AZ 3 cümle, tek cümlelik tweet YASAK” kuralı eklendi.

### ⚙️ `XiDeAI_Pro.csproj`
- `PostPublish` hedefi düzeltildi: `copy-publish-assets.ps1` çağrısı kaldırıldı, `CopyToOutputDirectory: PreserveNewest` mekanizması ile Scripts/Config zaten `bin\publish`'e kopyalanıyor.
- `PostPublish`'te `Dist\publish` sync için doğrudan robocopy `IgnoreExitCode` kullanılıyor.

### ⚙️ `copy-publish-assets.ps1`
- **Kurulu dizine doğrudan kopyalama bloğu tamamen kaldırıldı.** Artık yalnızca `dotnet publish → ISCC setup.exe → kurulum` akışı geçerlidir.

## 📦 Build Information
- **Assembly Version:** 4.9.3.0
- **Target Architecture:** win-x64
- **Runtime:** .NET 8.0 (Self-Contained)

## ✅ Verification
- Thread 1. tweet'e grafik görseli ekleniyor.
- 4 tweetlik thread lock timeout olmadan tamamlanıyor.
- `dotnet publish` sonrası Scripts/Config eksiksiz `Dist\publish` içinde yer alıyor.








