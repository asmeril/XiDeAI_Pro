# 🤖 XiDeAI Pro - Project Manifest v5.2.2

**Release Date:** 2026-06-02
**Version:** 5.2.2
**Build:** Release / win-x64 / PublishSingleFile
**Setup:** `Output/XiDeAI_v5.2.2_Setup.exe` (~64MB)

---

## 🚀 Bu Sürümde Ne Değişti? (v5.2.2)

### 1. Mention Sistemi Köklü Düzeltmesi
**Amaç:** `@@Selcoin` çift-@ hatası, `X-User` kirliliği ve AI'ın mention uyduranması tamamen giderildi.

- **`SignalEngine.cs`:** `Handle.TrimStart('@')` → tek `@` ekleme — çift `@@` hatası iki ayrı noktada (gerçek post + fallback) düzeltildi.
- **`social_intel.py`:** Genel X aramasında handle parse edilemezse tweet artık **tamamen atlanıyor** (`X-User` fallback kaldırıldı).
- **`SignalEngine.cs` Fallback:** "Zorunlu mention et" yönergesi kaldırıldı → "Doğal uyuyorsa kullanabilirsin, zorunlu değil" olarak değiştirildi.

### 2. Fenomenler Skor Sistemi Aktif Edildi
**Amaç:** Fenomenler listesi artık gerçek engagement verisine göre sıralanıyor.

- **`InfluencerControlService.cs`:** `UpdateScore(handle, delta)` metodu eklendi. `Score = Clamp(Score + delta, 0, 100)`.
- **`SocialIntelService.cs`:** Daemon'dan post alınca `engagement / 10` (max +5/tweet) formülüyle `UpdateScore` otomatik çağrılıyor. Aktif fenomenler üste çıkıyor.

### 3. AI Tarama Bağlamı (SignalEngine)
**Amaç:** AI artık sinyalin neden seçildiğini biliyor — daha somut analiz üretiyor.

- **`SignalEngine.cs` `priceContext`:** Her sinyal için strateji tipine göre tarama kriteri bloğu eklendi:
  - **ALPHA:** `EMA200 üzeri | ADX>20 | 18-bar dar bant (squeeze) | Hacim 1.5x+`
  - **PREMOVE:** `Günlük destek bölgesi | Hacim artışı ile dip test | Öncü birikim`
  - **ROKET:** `⚡ Hacim 3x+ ve mum %1+` — ayrı satır

### 4. Gösterge Rehberi → AI Prompt'a Enjekte Edildi
**Amaç:** AI thread üretirken OB/FVG/MSB/Pivot/RSI/MACD bilgisini kullanıyor.

- **`GeminiService.cs` `GenerateStrategySpecificAnalysis`:** `Config/IndicatorGuide.md` yüklenerek `priceContext`'e ekleniyor. Önceden bu metot rehberi hiç görmüyordu.

### 5. Son Tweet Engagement Kuralı (PromptManager)
**Amaç:** Her thread'in son tweeti tartışma açacak.

- **3 ana prompt** (`GetSignalAnalysisPrompt`, `GetDeepManualAnalysisPrompt`, `GetDeepTechnicalAnalysisPrompt`, `GetAlphaSignalPrompt`, `GetPreMoveSignalPrompt`):
  - **SON TWEET ZORUNLU:** Net karar (AL / İZLE / BEKLE) + takipçiyi görüşe davet eden soru.
  - Yasak sözcükler genişletildi: `piyasa kurdu, borsa kurdu, fısıltı alış, akıllı para, premove sahnesi` vb.
  - Robotik ton kaldırıldı: "Gizemli fısıldayan borsa kurdu" → "Sakin ama kararlı, önce seviye sonra yorum."

---

## 📂 Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/SignalEngine.cs` | Tarama kriteri bağlamı + `@@` fix (2 yer) + fallback optional |
| `Services/InfluencerControlService.cs` | `UpdateScore(handle, delta)` metodu eklendi |
| `Services/SocialIntelService.cs` | Engagement bazlı skor güncelleme bağlantısı |
| `Services/GeminiService.cs` | `GenerateStrategySpecificAnalysis` → IndicatorGuide.md yükleme |
| `Services/PromptManager.cs` | 5 prompt: son tweet CTA + yasak kelimeler + robotik ton fix |
| `Scripts/social_intel.py` | X-User fallback kaldırıldı → handle parse edilemezse tweet atla |

