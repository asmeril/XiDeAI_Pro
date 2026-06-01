# 🤖 XiDeAI Pro - Project Manifest v5.2.0

**Release Date:** 2026-06-02
**Version:** 5.2.0
**Build:** Release / win-x64 / PublishSingleFile
**Setup:** `Output/XiDeAI_v5.2.0_Setup.exe` (~64MB)

---

## 🚀 Bu Sürümde Ne Değişti? (v5.2.0)

### 1. Manuel ve Otomatik Thread Stabilizasyonu
**Amaç:** Fenomen tarzı paylaşımlarda yapay zekanın ürettiği mükemmel "Hook" tweetlerinin parçalanmasını ve anlamsız C# başlıklarının arkasına itilmesini engellemek.
- `ThreadService.cs` içerisindeki `PostSignalThread` metodu tamamen revize edildi. Sistemin kendi oluşturduğu sabit 1. tweet (Fiyat/Başlık) ve son tweet (Footer) iptal edildi.
- Fiyat ve TradingView linki, doğrudan yapay zekanın ürettiği ilk "Hook" parçasıyla birleştirildi.
- Grafik (Chart Image), birleştirilmiş gerçek 1. tweete eklenerek "Başlık + Hook + Grafik" bütünlüğü sağlandı. Böylece 4 tweetlik yapı tam olarak korundu.

### 2. Prompt ve Sınır Optimizasyonları
**Amaç:** Yeni birleştirilmiş 1. tweetin Twitter'ın 280 karakter limitini aşmasını engellemek.
- `PromptManager.cs` içerisinde `GetAlphaSignalPrompt`, `GetPreMoveSignalPrompt` ve `GetManualAnalysisPrompt` gibi stratejiler için **1. tweet (Hook) sınırı maksimum 200 karakter** olarak kısıtlandı.
- Kalan parçalar eskisi gibi 240-278 karakter aralığında korunarak organik akış sağlandı.

### 3. X-Hive Daemon Katı Doğrulama (Strict Validation)
**Amaç:** Görsel yükleme hataları veya timeout durumlarında Thread'in yanlışlıkla sistemdeki eski bir tweete "Reply" (Yanıt) olarak gönderilmesini engellemek.
- `playwright_daemon.py` içindeki `_post_single_tweet` fonksiyonuna katı doğrulama (strict validation) eklendi.
- Tweet gönderimi (Post) butonuna basıldıktan sonra, hata veren toast mesajları (Duplicate, error vb.) doğrudan Exception fırlatacak.
- Ekranda Compose (Oluştur) penceresi kapanmadan asılı kalırsa işlem anında sonlandırılacak, sahte bir başarıyla eski tweet URL'si kopyalanmayacak.

---

## 📂 Değişen Dosyalar

1. `XiDeAI_Pro.csproj` (Sürüm Güncellemesi)
2. `setup.iss` (Sürüm Güncellemesi)
3. `Services/ThreadService.cs` (Thread yapısı revizyonu)
4. `Services/PromptManager.cs` (200 karakter limitleri)
5. `Scripts/playwright_daemon.py` (Strict validation)
6. `PROJECT_INDEX.md`
7. `PROJECT_DIARY.md`
8. `PROJECT_MANIFEST_v5.2.0.md`
