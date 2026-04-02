# PROJECT MANIFEST v4.9.6

**Release Date:** 2026-03-31
**Status:** Stable
**Focus:** X-Hive Playwright Engine & Anti-Hallucination Guardrail

## 🚀 Overview
Bu sürüm, X-Hive projesinin güçlü Playwright motorunu entegre ederek X (Twitter) otomasyonundaki post & thread atma hatalarını tamamen ortadan kaldırır. Ayrıca AI halüsinasyonlarını filtreleyen sert güvenlik duvarları (Guardrail) eklenmiştir.

## 🛠️ Changes

### 🐍 `Scripts/playwright_daemon.py`
- **Tamamen Yeni Engine:** Selenium yerine Playwright tabanlı API kullanan, çok daha hızlı ve Thread işlemlerini natif olarak `1/n` formatıyla (X-Hive stilinde) yöneten arka plan botu.

### 🏢 `Services/SocialIntelService.cs`
- `PostThreadAsync`: C#'taki hatalı karakter bölme mantığı (`ThreadService.SplitText`) kaldırıldı. Tüm thread Playwright daemon'a tek kerede list tipinde gönderilir.
- `PostTweetAsync`: Eski Subprocess ve Daemon fallback mantığı Playwright'a devredildi.

### 🏢 `Services/NewsEngine.cs`
- **Anti-Hallucination Intercept:** Deprem, terör, sel gibi trajik anahtar kelimeler içeren haberlere AI skor=10 (Auto-Post) verirse, skor zorla `8`'e düşürülüp yayın Onay Havuzu'na atılır. AI'ın kontrolsüz kaza/felaket paylaşımları yapması yasaklandı.

## 📦 Build Information
- **Assembly Version:** 4.9.6.0
- **Target Architecture:** win-x64
- **Runtime:** .NET 8.0 (Self-Contained)

## ✅ Verification
- Playwright daemon x_cookies.json dosyasını hatasız okuyabiliyor.
- Uzun haberler Emoji/Link aritmetiğiyle hatasız 275-karakter parçalara bölünüp "🧵 1/n" etiketine sahip oluyor.
- Deprem/felaket haberleri onay bekleme havuzuna sorunsuz düşüyor.











