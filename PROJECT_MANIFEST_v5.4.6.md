# XiDeAI Pro - Project Manifest v5.4.6

**Release Date:** 2026-06-11
**Version:** 5.4.6
**Build:** Config-Tabanlı Dinamik Guru Profili Sistemi
**Setup:** `Output/XiDeAI_v5.4.6_Setup.exe` after Windows publish

---

## Bu Sürümde Ne Değişti? (v5.4.6)

### 1. Config-Tabanlı Dinamik Guru Profili Sistemi

Her üstat için artık `Config/GuruProfiles.json` dosyasından dinamik profil yüklenir. Yeni üstat eklemek JSON'a satır eklemek kadar kolaydır.

**GuruProfile Alanları:**
- `Name`: Üstatın görünen adı
- `Identity`: Kimlik tanımı (örn: "Piyasa analisti")
- `ScanType`: Tarama türü (TEKNİK, TAKAS, vb.)
- `Style`: Yazım tarzı
- `AnalysisFocus`: Analiz odak noktası
- `InteractionStyle`: Etkileşim tarzı
- `ForbiddenWords`: Üstate özgü yasak kelime listesi
- `SignaturePhrases`: İmza ifadeler

### 2. Config/GuruProfiles.json (YENİ DOSYA)

- `@EFELERiiNEFESi3`: Teknik analiz odaklı profil (EMA, RSI, MACD, OB/FVG, Pivot)
- `@matisay67`: Takas/Veri odaklı profil (Yabancı payı, AKD/BOFA, fiili dolaşım)
- Yeni üstat eklemek için JSON'a yeni bir handle-key eklemek yeterli

### 3. ConfigManager.cs Güncellemesi

- `GuruProfile` sınıfı eklendi
- `GetGuruProfile(string guruHandle)` metodu eklendi — JSON'dan dinamik profil yükler
- `DefaultProfile()` fallback metodu eklendi
- AppDomain.CurrentDomain.BaseDirectory ve AppContext.BaseDirectory'den Config/GuruProfiles.json aranır

### 4. PromptManager.cs — GetGuruHonoringThreadPrompt() Yeniden Yazıldı

- Artık `ConfigManager.GetGuruProfile()` çağırarak üstate özgü profil yükler
- Profilin Style, AnalysisFocus, InteractionStyle, ForbiddenWords alanlarını prompt'a yedirir
- `tweetContent` parametresi eklendi — üstadın orijinal tweet içeriği yönlendirici olarak kullanılır
- Yasak kelime listesi profil + genel yasaklar birleştirilerek dinamik oluşturulur

### 5. GeminiService.cs — GenerateGuruHonoringThread() Güncellendi

- `tweetContent` parametresi eklendi ve prompt'a aktarılıyor

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Config/GuruProfiles.json` | YENİ — Dinamik üstat profil tanımları |
| `ConfigManager.cs` | `GuruProfile` sınıfı, `GetGuruProfile()`, `DefaultProfile()` eklendi |
| `Services/PromptManager.cs` | `GetGuruHonoringThreadPrompt()` profil-tabanlı dinamik prompt'a dönüştürüldü |
| `Services/GeminiService.cs` | `GenerateGuruHonoringThread()` tweetContent parametresi eklendi |
| `XiDeAI_Pro.csproj` | Versiyon 5.4.6 |
| `PROJECT_MANIFEST_v5.4.6.md` | Bu manifest |

---

## Doğrulama

- `dotnet build XiDeAI_Pro.csproj` → Build succeeded, 0 Error(s), 2 Warning(s)
- Referans: v5.4.5 (Çoklu Üstat Paneli + Mehmet Atışay Takas Analizi)