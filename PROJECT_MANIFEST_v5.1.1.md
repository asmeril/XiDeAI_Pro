# 🚀 XiDeAI Pro — Project Manifest v5.1.1

**Release Date:** 2026-05-31
**Version:** 5.1.1
**Build:** Release / win-x64 / PublishSingleFile
**Setup:** `Output/XiDeAI_v5.1.1_Setup.exe` (~64MB)

---

## 🔄 Bu Sürümde Ne Değişti?

### 1. iDeal Robot → XiDeAI Canlı Veri Entegrasyonu

**Amaç:** iDeal tarama robotlarının anlık piyasa verisini XiDeAI uygulamasına file-based IPC ile beslemek.

**Yeni Robot: `Robot_XU100_Nabiz_Monitor.txt`** (d:\MEGA\Robots)
- Her 5 dakikada XU100, XU030, XU050 verisi çeker.
- `Market_Status.txt` → XiDeAI'nin `RefreshTrendsAsync` tarafından okunur.
- `Market_Pulse_Alarm.txt` → Gün içi hacimli/sert hareketleri birikimli yazar; EOD thread'ini besler.
- Alarm eşikleri: `ESIK_SERT=%0.50`, `ESIK_BUYUK=%1.00`, `HACIM_KAT=2.0x`.
- 18:00–18:15 arası EOD_SNAPSHOT satırı yazar (günün yüksek/düşük/range/kapanış).

**Değişen: `MainForm.cs`**
- `RefreshTrendsAsync()`: `Market_Status.txt` okunur → `[XU100_CANLI_VERI: MOD=X, TREND=Y(Z%)]` hard data ile Twitter trendleri birleştirilerek `DailyTrends` oluşturulur.
- `PostMarketCloseSummary()`: `Market_Pulse_Alarm.txt` okunur → bugünün alarm satırları `pulseAnomalies` olarak `GenerateMarketCloseTableTweet` zincirine geçirilir.

---

### 2. Fenomen Thread Formatı — Gün Sonu EOD Tweeti

**Değişen: `Services/GeminiService.cs`**
- `GenerateMarketCloseTableTweet` imzasına `pulseAnomalies = ""` parametresi eklendi.

**Değişen: `Services/PromptManager.cs` — `GetMarketClosePrompt`**
- Eski: Tek tweet, 280 karakter sıkışık tablo formatı.
- **Yeni: 6-7 tweet fenomen thread yapısı:**
  - Tweet 1: 🔥 HOOK — pulse varsa o olay kanca
  - Tweet 2: 📊 XU100/XU030/XU050 kapanış yorumu + hacim
  - Tweet 3: 🚀 Günün yıldızları (Top Gainers)
  - Tweet 4: 💀 Günün kazazedeleri (Top Losers)
  - Tweet 5: 🚨 PULSE ANLARI — Smart Money diliyle gün içi alarmlar (pulseAnomalies varsa)
  - Tweet 6: 🔮 Yarına bakış
  - Tweet 7: 📌 CTA (takip / RT / bildir aç)

---

### 3. X Algoritma Fenomen Kuralları — Tüm Promptlara Enjeksiyon

**Değişen: `Services/PromptManager.cs`**

Tüm `### GÖREV` bloklarının önüne 4 kural eklendi:
1. **HOOK:** Çarpıcı ilk cümle (soru, rakam veya şok stat)
2. **FORMAT:** Kısa cümleler, boşluklu satırlar (dwell time artırır)
3. **HİKAYELEŞTİR:** FVG/OB gibi teknik terimleri halk diliyle anlat (ELI5)
4. **CALL TO ACTION:** Son tweette RT / takip / bildirim aç çağrısı

**Contrarian Filter:**
- `DailyTrends` = `[XU100_CANLI_VERI: MOD=X, TREND=Y%] YATIRIMCI_SOSYAL_ALGI: #...`
- AI hard data ile sosyal algı zıtlığını Smart Money tuzağı olarak yorumlar.

---

### 4. iDeal Robot Derleme Hataları Giderildi

**`Robot_Alpha_Scanner_v3.0.txt`**
- `$"..."` string interpolation → `+` birleştirme (CS1056: `$` hatası)

**`Robot_PreMove_Scanner_v3.0.txt`**
- `bugunYukarı` identifier → `bugunYukari` (CS1056: `±` hatası)
- `new string('─', 90)` → `new string('-', 90)` ×3 (CS1012: char literal hatası)

> **Kök neden:** iDeal `CSharpCodeProvider` C# 5.0 öncesi davranır. String interpolation (`$`) ve multi-byte char literal desteklenmez.

---

## 📂 Değişen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `Services/GeminiService.cs` | `GenerateMarketCloseTableTweet` — `pulseAnomalies` parametresi |
| `Services/PromptManager.cs` | `GetMarketClosePrompt` 6-7 tweet fenomen thread; X Algoritma kuralları tüm prompt'lara |
| `MainForm.cs` | `RefreshTrendsAsync` Market_Status.txt okuma; `PostMarketCloseSummary` pulse feed |
| `XiDeAI_Pro.csproj` | Version: 5.1.1 |
| `setup.iss` | MyAppVersion: 5.1.1 |
| `d:\MEGA\Robots\Robot_XU100_Nabiz_Monitor.txt` | **YENİ** — Nabız monitor robotu (276 satır) |
| `d:\MEGA\Robots\Robot_Alpha_Scanner_v3.0.txt` | CS1056 `$` interpolation düzeltmesi; Market_Status.txt write |
| `d:\MEGA\Robots\Robot_PreMove_Scanner_v3.0.txt` | CS1056 `±` identifier + CS1012 char literal düzeltmeleri |

---

## ✅ v5.1.0'dan Devralınan Özellikler

- Thread reply XHive-style rewrite (`playwright_daemon.py`)
- `/no_think` prefix (Qwen3 reasoning bastırma)
- Vision timeout 600s, metin timeout 300s
- PublishSingleFile ~64MB setup

---

## 🔬 Test Kriterleri

- `Market_Status.txt` yazıldı mı: `C:\iDeal\TARAMA_LOG\Market_Status.txt` kontrol et
- `DailyTrends` string'i `[XU100_CANLI_VERI: MOD=BULL, TREND=+0.45%]` formatında mı?
- EOD thread: 6-7 parça çıkıyor mu, son tweette CTA var mı?
- Pulse alarm: `Market_Pulse_Alarm.txt` doluysa Tweet 5 (🚨) üretiliyor mu?
- Robot derleme: iDeal'de Alpha ve PreMove robotları hatasız çalışıyor mu?
