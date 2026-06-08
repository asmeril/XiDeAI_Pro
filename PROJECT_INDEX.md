> **Version:** 5.4.3 (Guru Panel Guardrails)
> **Architecture:** Hybrid (C# WinForms + Canonical PostingService + Python Playwright Posting Engine + Selenium Research Fallback + WebView2 Session Bridge)
> **Last Updated:** 2026-06-06

Bu indeks, proje üzerinde çalışacak yapay zeka ve geliştiriciler için **kod tabanının haritasını** sunar. Yeni özellik eklerken veya hata düzeltirken burayı referans alınız.

---

## 🏗️ Core Architecture (Hibrit Yapı)

Proje 4 ana katmandan oluşur:
1.  **Orchestrator (C#):** Tüm mantığı yöneten, servisleri başlatan ve kararları veren katman. (`OperationManager`)
2.  **Publishing Engine (Python Playwright):** Thread gönderiminde ana motor. (`playwright_daemon.py`)
3.  **Interaction Layer (Hybrid):**
    *   **Playwright-First (Thread/Tweet):** Gönderim işlemleri Playwright motoru ile yürür.
    *   **WebView2 Fallback:** Kullanıcı görünürlüğü gerektiren işlemler için yedek.
    *   **Python/Selenium Fallback:** Fenomen tarama/araştırma için halen aktif.
4.  **Intelligence Layer (AI):** LM Studio (Yerel Model, Birincil) + Gemini/Perplexity (Yedek/Bulut) entegrasyonu.

### ✅ Canonical Publishing Flow (Tek Gerçek Hat)
1. Modüller sadece içerik üretir; gönderimi `PostingService` yapar.
2. `PostingService.PostTweetAsync` / `PostThreadAsync` tüm tweet/thread payloadlarını normalize eder ve `SocialIntelService` alt motoruna iletir.
3. `SocialIntelService` canonical olarak `Scripts/playwright_daemon.py` kullanır; WebView2 internal bridge debug-only bırakılmıştır.
4. `playwright_daemon.py` gerçek `/status/<id>` URL yakalamadan success dönmez; thread için `posted_count == total_chunks` beklenir.
5. C# tarafında success sadece `PostingService.IsVerifiedTweet/IsVerifiedThread` doğrulamasından sonra kabul edilir.

### ❌ Kaçınılacak Eski Davranışlar
- Aynı işlemde hem fail hem success logu yazmak.
- C# tarafından hazırlanmış thread parçalarını Python'da gereksiz yeniden parçalamak.
- Thread sayaçlarını parça sayısı yerine sabit +1 artırmak.
- WebView2 modal kapandı veya butona tıklandı diye post'u başarı saymak.

---

## 📂 Services Map (C#)

Tüm servisler `Services/` klasörü altındadır ve `OperationManager.cs` tarafından yönetilir.

| Servis Dosyası | Temel Görevleri | Bağlı Olduğu Python Script |
| :--- | :--- | :--- |
| **`SocialIntelService.cs`** | **Düşük seviye X (Twitter) köprüsü.** PostingService tarafından çağrılır; Playwright subprocess ile doğrulanmış gönderim yapar. Araştırma/interaction için daemon ve Selenium fallback içerir. | `playwright_daemon.py`, `x_daemon.py`, `social_intel.py` |
| **`PostingService.cs`** | **v5.3.0 Canonical gönderim servisi.** Tüm modüller için tek tweet/thread doğrulama kapısı. `/status/` URL ve thread parça sayısı doğrulanmadan success dönmez. | `playwright_daemon.py` |
| **`OperationManager.cs`** | **Orkestra Şefi.** Servisleri başlatır, daemon'ı başlatır, durdurur ve birbirine bağlar. | - |
| `GeminiService.cs` | **AI Motoru.** Promptları işler, görsel analiz yapar (Vision) ve thread üretir. **v4.2.2:** Two-Step News metodları eklendi. | - |
| `ModelBenchmarkService.cs` | **v4.9.9** Gemini modellerini test eder, API'den canlı model listesi çeker, benchmark yapar. **v4.9.9:** `UpdateTaskPreferencesFromResults()` eklendi — benchmark sonucu ModelManager TaskType tercihlerini dinamik olarak günceller. | - |
| `NewsEngine.cs` | **v4.2.2:** Two-Step Logic (1-10 skor, 7 kategori, SON DAKİKA boost). Haber akışını kategorize eder ve işler. **v5.1.3:** Flash haber garantili 2-tweet format (`GetFlashNewsAnalysisPrompt`); `BuildMinimalNewsTweet` — AI null dönse bile başlık+link tweet'i gönderilir; maxTokens 800/900'a yükseldi; `BuildNewsLeadTweet` link+flash tag eklendi. **v5.1.4:** CS8602 null guard — `threadContent != null &&` eklendi. |
| `NewsTrackerService.cs` | RSS ve Twitter'dan haber tarar. **v3.8.3:** `OnNewsDetected` eventini tetikler. | `x_daemon.py` |
| `ThreadService.cs` | Zincir (Thread) oluşturma mantığını kurar. | - |
| `ThreadPipeline.cs` | **Merkezi Thread Hazırlayıcı.** Lead tweet, parça normalizasyonu ve ortak split kurallarını tek yerde toplar. | - |
| `SymbolNormalizer.cs` | **v5.2.3** Merkezi sembol normalizasyonu ve BIST sembol doğrulaması. `VIP'VIP-VAKBN`, `VIP-VAKBN`, `BIST:VAKBN` gibi varyantları canonical sembole indirger; kırpılmış/bozuk sembolleri engeller. | - |
| `InfluencerControlService.cs` | Takip edilecek fenomenlerin veritabanını yönetir. **(v5.2.2)** `UpdateScore(handle, delta)` eklendi — engagement bazlı otomatik skor güncelleme (0-100 aralığı). | - |
| `PriceFetchService.cs` | **Fiyat Motoru.** BIST ve Kripto paraların anlık fiyatını çeker. (Parallel Async). | - |
| `SignalEngine.cs` | Sinyal işleme motoru. Sinyalleri filtreler, formatlar ve yayınlar. | - |
| `LogFileWatcher.cs` | **v5.2.3** `Sinyal_Log_Database.txt` için snapshot tabanlı izleme. Byte-tail kaynaklı satır ortası okuma (`ACSEL→SEL`, `DEVA→VA`) engellendi. | - |
| `SignalParser.cs` | **v5.2.3** Strict Alpha/PreMove parse. Header, `KAPALI`, bilinmeyen BIST sembolü, fiyatı sıfır ve geçersiz durumlar atlanır. | - |
| `ModelManager.cs` | **v4.10.0** AI provider yöneticisi. Aktif provider'ı seçer, fallback/routing yapar. `SyncGeminiProviders()` ile LMStudio dahil tüm provider'ları senkronize eder. | - |
| `LMStudioProvider.cs` | **v4.10.0** LM Studio / LM Link local model provider'ı (OpenAI uyumlu). `SendRequest()` + `SendRequestWithImage()` destekler. **v4.10.2:** `PrepareImageForVision()` — 4K DPI ekran görüntülerini 1024px JPEG'e dönüştürür. **v5.0.0:** `/no_think` prefix (Qwen3 reasoning bastirma), vision timeout 600s. **v5.1.3:** `reasoning_content` fallback KALDIRILDI — `content=boş`+`finish_reason=length` durumunda `null` döndürülüyor; `finish_reason=length` logu eklendi. **v5.1.4:** `AnalyzeNewsUnified` maxTokens 450→1500 (Qwen3.6-27b finish_reason=length sorunu çözüldü); prompt'a "düşünme adımı YOK" hint eklendi. |
| `ManualAnalysisService.cs` | **v4.10.8** Manuel analiz servisi. **Yerel model aktifken:** `IndicatorExtractor` atlanır, kısa thread için ekran görüntüsü tekrar gönderilmez, ana analiz metni indicator context olarak kullanılır. | - |

> **Not (v4.0.0):** HIVE servisleri (Sentinel, Apex, Omni, Oracle, Wisdom, Cortex) kaldırılmıştır. Yedek: `d:\Projects\HiveProjesi`

### 🔑 Key Classes & Methods

#### `SocialIntelService.cs`
- `FindInfluencerAnalyses(symbol, market)`: Fenomenlerin analizlerini arar. (Önce VIP timeline, sonra genel arama).
- **(v5.2.2)** Daemon'dan post alınınca `engagement/10` formülüyle `InfluencerControlService.UpdateScore()` çağrılır — etkin fenomenler üste çıkar.
- **(v5.2.2)** Genel arama parse hatasında handle boş kalırsa tweet atlanır (eski: `X-User` fallback kaldırıldı).
- **(v5.2.3)** `IsBadSocialResult(author, content, url)`: kendi hesap, bot çıktısı (`Piyasa Görüşleri`, `Teknik Analizim`, `XiDeAI`), `ERROR_404` ve ana sayfa URL sonuçlarını filtreler.
- `PostTweet(text)` / `PostThreadAsync(tweets)`: Düşük seviye Playwright posting köprüsü. **v5.3.0:** WebView2 internal bridge canonical yoldan çıkarıldı; `/status/` URL ve `posted_count/total_chunks` doğrulaması zorunlu.
- `CheckSafety(actionType)`: **(v4.6.0)** Güvenlik kontrolü yapar (Hız limiti ve günlük kotalar).
- `PerformDeepScanAsync()`: Rastgele seçilen fenomenleri tarayarak bilgi tabanını günceller.
- **(v5.3.0)** `PostTweet`/`PostThreadAsync` yalnızca `PostingService` tarafından production gönderim için kullanılmalıdır; internal WebView2 bridge canonical yoldan çıkarıldı.

#### `PostingService.cs`
- **(v5.3.0)** `PostTweetAsync(text, mediaPath, module)`: Tekil tweetleri canonical Playwright hattına gönderir, gerçek `/status/` URL yoksa hata döndürür.
- **(v5.3.0)** `PostThreadAsync(parts, mediaPath, module)`: Thread parçalarını `ThreadPipeline.EnsureWithinLimit` ile normalize eder; tüm parçalar gönderilmeden success dönmez.
- **(v5.3.0)** `IsVerifiedTweet` / `IsVerifiedThread`: Uygulama genelinde tek başarı standardı.

#### `FanZoneService.cs`
- **(v5.3.0)** Kritik hesap taramasında tweet URL'si işlem öncesi değil `ProcessTweet` içinde dedupe edilir; like/RT başarı ikonları yalnızca gerçek `status=success` dönerse işaretlenir.

#### `InteractionEngine.cs`
- **(v5.3.0)** `RunTargetedCheck(category)` artık `Influencer.Handle` değerlerini gönderir; önceki `string.Join(targets)` class-name üretme hatası giderildi.
- **(v5.3.2)** Viral reply adayları öneri aşamasında interaction memory'ye yazılmaz; sadece onaylı yanıt gerçek başarıyla gönderildikten sonra işaretlenir.
- **(v5.3.3)** Otomatik bot döngüsünden direkt Like/RT kaldırıldı. Hedef fenomen etkileşimi sadece manuel tetiklenir; varsayılan aksiyon yavaş modda Like-only, Python tarafında son 6 saat + gerçek tweet sahibi filtresi zorunludur.

#### `ThreadService.cs`
- **(v4.10.8)** Tweet parçaları `.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5)` filtresiyle kısa/boş parçalar temizlenir.
- **(v5.3.0)** Sinyal, batch, günlük/haftalık rapor threadleri `PostingService` üzerinden gönderilir.
- **(v5.3.4)** Sinyal threadleri en fazla 4 parça ile sınırlandı; beğeni/RT çağrısı kaldırıldı, sonuç tweeti seviye/teyit/risk diline çekildi.
- **(v5.4.1)** Sinyal lead tweetlerinde ham `AKTIF/PULLBACK_ADAY` yerine takipçi dostu `Sinyal canlı, teyit aranıyor` / `Geri çekilme takibi, acele yok` etiketleri kullanılır.

#### `LogFileWatcher.cs`
- **(v5.2.3)** `LoadSeenKeys(path)`: servis başlarken mevcut açık sinyalleri hafızaya alır, geçmiş satırları tekrar tetiklemez.
- **(v5.2.3)** `ReadStableLines(path)`: iDeal dosyasını snapshot olarak okur; rewrite edilen dosyada byte offset kullanılmaz.
- **(v5.2.3)** `TryBuildSignalKey(line, out key, out strategy)`: `Symbol|Strategy|Period|Tarih|Durum` anahtarı üretir, `KAPALI` ve geçersiz satırları atlar.

#### `SignalParser.cs`
- **(v5.2.3)** `ParseDbLine(line, strategyOverride)`: 12 kolonlu iDeal DB satırlarını strict parse eder; `D` periyodu `G` olarak normalize edilir; `SymbolNormalizer` ile canonical sembol kullanılır.

#### `SignalPersistenceService.cs`
- **(v5.3.0)** Processed key artık `Symbol|Strategy|Period|Durum|DetectedAt` tabanlıdır; aynı sembol/periyotta farklı strateji veya yeni tarihli sinyal yanlışlıkla bastırılmaz.

#### `SymbolNormalizer.cs`
- **(v5.2.3)** `NormalizeSignalSymbol(rawSymbol)`: `VIP'VIP-VAKBN` ve benzeri bozuk prefixleri temizleyerek canonical BIST sembolü üretir.
- **(v5.2.3)** `IsKnownBistSymbol(symbol)`: `Config/symbols_bist.txt` üzerinden sembol doğrular; config yoksa makul BIST formatına fallback yapar.

#### `PromptManager.cs`
- **(v4.10.8)** Derin analiz prompt'una `### GÖRSEL OKUMA (GRAFİK)` bölümü eklendi — yerel modelin grafik okuma kalitesini artırır.
- **(v5.1.1)** `GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: Yeniden yazıldı. Eski tek-tweet şablon → 6-7 tweet fenomen thread yapısı (Hook → XU100 yorum → Yıldızlar → Kazazedeler → Pulse anları → Yarına bakış → CTA).
- **(v5.1.1)** Tüm `### GÖREV` bloklarına X Algoritma Fenomen Kuralları enjekte edildi: Hook (kanca ilk cümle), kısa/boşluklu format (dwell time), ELI5 hikayeleştirme, CTA (son tweette RT/takip).
- **(v5.1.1)** Contrarian Filter: `DailyTrends` = `[XU100_CANLI_VERI: MOD=X, TREND=Y%] YATIRIMCI_SOSYAL_ALGI: #...` — AI hard data ile sosyal algı zıtlığını Smart Money tuzagı olarak yorumlar.
- **(v5.2.2)** `GetSignalAnalysisPrompt`, `GetDeepManualAnalysisPrompt`, `GetDeepTechnicalAnalysisPrompt`: YASAK SÖZCÜKLER listesi eklendi (fısıltı alış, akıllı para, piyasa kurdu vb.). Son tweet ZORUNLU: AL/İZLE/BEKLE karar + soru formatı.
- **(v5.2.2)** `GetAlphaSignalPrompt` / `GetPreMoveSignalPrompt`: Robotik ton kaldırıldı (borsa kurdu, fısıldayan vb.). Fenomen mention: varsa doğal, yoksa ekleme (zorunlu değil).
- **(v5.2.9)** `GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash, sectorMap)`: `sectorMap` parametresi eklendi. `GetEkonomiNewsAnalysisPrompt`, `GetTeknolojiNewsAnalysisPrompt`, `GetYasamNewsAnalysisPrompt` BIST Sektör Haritası'nı prompt'a enjekte eder; halusinatör sembol üretimi engellendi. Her kategori prompt'u "TAM OLARAK 3 TWEET" ve `|||` zorunluluğu ile güncellendi.

#### `MainForm.cs`
- **(v5.1.1)** `RefreshTrendsAsync()`: `Market_Status.txt` okunur → `[XU100_CANLI_VERI: MOD, TREND%]` hard data + Twitter trendleri birleşik `DailyTrends` string'i oluşturur.
- **(v5.1.1)** `PostMarketCloseSummary()`: `Market_Pulse_Alarm.txt` okunarak bugünün nabız alarmları `pulseAnomalies` string'ine toplanır ve `GenerateMarketCloseTableTweet` zincirine iletilir.
- **(v5.3.0)** Sabah motivasyon ve gün sonu raporu `_tweetedToday` içine sadece doğrulanmış başarıdan sonra işlenir; `*_PENDING` guard eklendi.
- **(v5.3.0)** Fenomen silme UI'ı `InfluencerControlService.DeleteInfluencer()` kullanır; kopya liste üzerinden silme hatası giderildi.
- **(v5.3.0)** Telegram `/ONAY` etkileşim onayı reply sonucunu kontrol eder; başarısızsa pending kayıt silinmez.
- **(v5.3.0)** Manuel analiz tweet butonu sadece başarılı analiz sonucunda aktif olur.
- **(v5.3.2)** Bot Etkileşim tabına manuel `Şimdi Tara`, `BIST Fenomen`, `Kripto Fenomen`, `Durum` kontrolleri eklendi; checkbox timer'ı başlatır/durdurur.
- **(v5.3.2)** `/BOTDURUM`, `/ETKILESIMTARA`, `/ETKILESIMTEST @handle` Telegram komutları eklendi.
- **(v5.3.3)** `CheckForInteractions()` artık otomatik Like/RT yapmaz; yalnızca taze, geçerli handle'lı, spam olmayan tweetler için onaylı reply adayı üretir.
- **(v5.3.4)** Manuel analiz paylaşımı sadece doğrulanmış 4 parçalık `ShortThread` ile yapılır; detay rapor artık X thread'e fallback edilmez.
- **(v5.3.4)** Gün sonu özeti paylaşımı en fazla 4 tweet ile sınırlandı; factual kapanış formatı ve YTD güvenliği zorunlu.
- **(v5.3.5)** Telegram `/ANALIZ` UI ile aynı `TradingViewChartId` akışını kullanır; opsiyonel üçüncü argüman baz (`TL/USD/EUR/XU100`) olarak alınır.
- **(v5.3.5)** Analiz kimliği sade, seviye/teyit/risk odaklı tona çekildi; fenomen persona/clickbait/FOMO dili azaltıldı.
- **(v5.3.6)** Etkileşim reply üretimi kategori personası yerine nötr kısa editör tonu kullanır; hassas/alakasız içerikte `SKIP` cevabı aksiyonu iptal eder.
- **(v5.3.7)** Telegram haber onay bildirimleri kısa, düz metin ve karar odaklı formata alındı; uzun reasoning/summary kaynaklı Markdown riski azaltıldı.
- **(v5.3.9)** Tek tweet manuel paylaşımı 280+ karakteri otomatik thread'e çevirmez; kullanıcı açıkça thread modunu seçmek zorundadır.
- **(v5.4.0)** Manuel analiz short-thread formatı 4-8 parçaya çıkarıldı; ilk 2 tweet kısa özet/devam rehberi, sonraki tweetler seviye/teyit/risk detaylarıdır. 120 karakter altı parçalar geçersiz sayılır.
- **(v5.4.1)** Aynı sembol için 7 gün içinde tekrar sinyal gelirse detaylı analiz yerine önceki analize atıf yapan 1-2 tweetlik pekiştirme thread'i paylaşılır.
- **(v5.4.2)** Etkileşim adayları otomatikte yalnız finans niyeti taşıyan tweetlerden seçilir; promo/giveaway/RT çağrısı hard-block edilir ve Telegram komutları `/ONAY_ID` formatına alınır.
- **(v5.4.3)** Üstat paneli yalnız `GuruHandle` mention'ına izin verir; kaynak tarama tweet URL'si zorunludur ve hoca saygısı ölçülü teknik analiz diline çekildi.

#### `NewsEngine.cs`
- **(v5.3.6)** Haber threadleri en fazla 3 parçaya sınırlandı; son parçada haber özeti/YTD güvenliği zorunlu hale getirildi.
- **(v5.3.8)** Normal skor 9 haberler Telegram onayına düşmez; `SKIPPED_REVIEW` history'ye yazılır. Yalnız skor 10 veya breaking 9+ auto-post olur.

#### `PerformanceTracker.cs`
- `RecordSignal(signal)`: Bot, Manuel veya Guru kaynaktan gelen sinyali veritabanına işler.

#### `GeminiService.cs`
- `AnalyzeChartImage(symbol, path)`: **(v3.9.0)** Grafik görsellerini teknik analize (RSI, Trend, Formasyon) dönüştürür.
- `GenerateGuruHonoringThread(...)`: Görsel analiz ve fiyat verisini kullanarak guru threadi üretir.
- `DetectNewsCategory(title, source)`: **(v4.2.2)** Haber kategorisini tespit eder (7 kategori).
- `AnalyzeNewsImpactTwoStep(title, source)`: **(v4.2.2)** Önce kategori, sonra 1-10 skor üretir.
- `GenerateNewsCategoryAnalysis(category, title, source, link)`: **(v4.2.2)** Kategoriye özel analiz thread'i üretir.
- **(v5.2.9)** `LoadSectorMapContext()`: `Config/BistSectorMap.md` dosyasını okur. `GenerateNewsCategoryAnalysis` ve `AnalyzeNewsForThread` çağrılarında `sectorMap` parametresi olarak prompt'a enjekte edilir.
- `SendRequest(prompt)`: AI modeline metin tabanlı istek gönderir.
- `GenerateMarketCloseTableTweet(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: **(v5.1.1)** Gün sonu kapanış tweet thread'i üretir.

#### `LMStudioProvider.cs`
- `SendRequest(prompt)`: LM Studio'ya metin isteği gönderir (OpenAI compat). **(v5.0.0)** Prompt başına `/no_think\n` prefix eklenir, timeout 300s.
- `SendRequestWithImage(prompt, imagePath)`: Görsel + metin isteği gönderir. **(v5.0.0)** Timeout 600s, `max_tokens` minimum 8192.
- `PrepareImageForVision(imagePath, maxDimension)`: **(v4.10.2)** Görseli max 1024px'e küçültür ve JPEG 85% kalitesinde kodlar.
- `BuildRequestBody(prompt, maxTokens)` / `BuildVisionRequestBody(prompt, imageUrl, maxTokens)`: **(v5.2.3)** LM Studio OpenAI-compatible isteklerine `enable_thinking=false`, `reasoning_effort=none`, `chat_template_kwargs.enable_thinking=false` ekler.
- `ExtractContentFromChoice(choice)`: **(v5.2.3)** `reasoning_content` artık publishable kabul edilmez. `content` boşsa veya `finish_reason=length` ise provider `null` döndürür ve fallback tetikler.

#### `ManualAnalysisService.cs`
- **(v4.10.8)** Yerel model aktifken `IndicatorExtractor` çağrısı atlanır (token tasarrufu).
- **(v4.10.8)** Kısa thread üretiminde yerel model için ekran görüntüsü tekrar gönderilmez.
- **(v4.10.8)** Yerel modele indicator context yerine ana analiz metni iletilir.

---

## 🐍 Python Scripts Map (Scripts/)

Python scriptleri "Worker" (İşçi) olarak çalışır. C# tarafından komut satırı argümanları ile çağrılır ve JSON çıktısı üretirler.

| Script Dosyası | Görev Tanımı | Kütüphaneler |
| :--- | :--- | :--- |
| **`playwright_daemon.py`** | **(v4.9.6 Yeni) Thread & Yayın Motoru.** X-Hive bazlı yeni süper hızlı bot. **v5.2.3:** `_robust_click_publish()` ile X overlay/click intercept durumlarında Escape → normal click → force click → JS click fallback zinciri ve hata screenshot'ı. | `playwright.async_api` |
| **`x_daemon.py`** | **HTTP Daemon (localhost:5580).** Tek Chrome instance ile sürekli çalışır. **(v4.9.4)** `_post_single_tweet` URL yakalama - home fallback kaldırıldı, toast/profile retry eklendi. | `selenium`, `undetected_chromedriver` |
| **`social_intel.py`** | **Dev X Otomasyonu.** Selenium ile giriş yapar, arama yapar, veri çeker, etkileşim kurar. **v5.2.3:** Türkçe engagement parse (`B=bin`, `Mn=milyon`), own-account/bot-output filtreleri, 404 sentinel kaldırma ve status URL dedupe eklendi. | `selenium`, `pickle` |
| `omni_scout.py` | Reddit ve diğer kaynaklardan viral veri çeker. | `praw` (Reddit API) |
| `oracle.py` | Tahmin piyasaları verisi (Polymarket vs.) | `requests` |
| `screenshot.py` | BIST/Crypto grafiklerinin ekran görüntüsünü alır. **v5.2.3:** Python tarafında da `VIP'VIP-*` ve prefix sembol normalizasyonu yapar. | `selenium` |
| **`lock_manager.py`** | **Atomic File Lock.** X (Twitter) oturumlarının çakışmasını önler. **(v4.9.3)** `acquire_lock` timeout 180s → 360s. | `msvcrt`(Win) / `fcntl`(Linux) |

### 🐍 `social_intel.py` Capabilities
Bu script "Standalone" (Tek başına) çalışabilen güçlü bir bottur.
- **Driver Pool:** `ChromeDriverPool` sınıfı ile tarayıcıları önbelleğe alır (Performans artışı).
- **Smart Search:** `find_influencer_posts` fonksiyonu ile hem timeline hem de genel arama yapar.
- **Human-Like Behavior:** **(v4.6.0)** `human_delay` fonksiyonu ile insansı beklemeler yapar ve yakalanmayı önler.
- **Robust Typing:** **(v4.6.6)** Metni `document.execCommand('insertText', ...)` kullanarak JS enjeksiyonu ile yazar. React senkronizasyonu için "WAKE UP" mekanizması içerir ve Türk karakterleri için ultra-stabilitedir.
- **Commands:** `search_influencer`, `post_tweet`, `fetch_replies`, `discover_influencers` vb.

---

## 🖥️ UI Map (Arayüz Haritası)

### 🏠 MainForm (Ana Ekran)
*   **Sidebar (Navigasyon):**
    *   `Ana Ekran`, `Sinyal Merkezi`, `Manuel Analiz`, `Bot Etkileşim`, `Ayarlar`
    *   `Geçmiş`, `Fenomenler`, `Haberler` (Restore Edildi), `Üstat Paneli`, `Fenerbahçe`
    *   `HIVE Intel`, `Etkileşim Merkezi`
*   **Dashboard (`pnlDashboard`):**
    *   **Header:** API/Web Sayaçları, Ticker, Start/Stop Butonları.
    *   **Tabs:** `Piyasa Analiz (Grafik)`, `Sosyal Medya Akışı (X)`.
*   **Sinyal Merkezi (`pnlSignals`):**
    *   **Filtreler:** Strateji (King, Bomba...), Periyot, Eşik Değerler.
    *   **Grid:** `dgvSignals` (Canlı sinyaller).
*   **Manuel Analiz (`pnlAnalysis`):**
    *   **Kontroller:** Pazar, Periyot, Sembol seçimi.
    *   **Aksiyon:** Analiz Et -> Sonuç (Text) + Grafik (Resim) -> Tweetle.
*   **HIVE Intel (`pnlHive`):**
    *   **Apex Ar-Ge:** Makaleler (Papers) ve GitHub Repoları.
    *   **Meta-Teacher:** Konsey (Guru) içgörüleri tablosu.
    *   **Wisdom:** Bilgelik kütüphanesi (`WisdomLibControl`).

### ⚙️ Ayarlar Paneli Detayları (`pnlSettings`)
> **Konum:** `MainForm.cs` satır ~908-1165

**Yapı:** `SplitContainer` (Sol: Kategori ListBox, Sağ: İçerik Panel)

| Kategori | Panel | Kontroller |
|----------|-------|------------|
| 🔑 API & Bağlantılar | `pnlSetApi` | `txtApiKey`, `txtApiSecret`, `txtAccessToken`, `txtTokenSecret` (Twitter) |
|  |  | `txtGeminiKey`, `txtPerplexityKey`, `cmbGeminiModel` (AI) |
|  |  | `btnTestApi` (🧪 Test), `btnListModels` (📋 Modeller) |
|  |  | `dgvBenchmark` (Benchmark Grid), `btnRunBenchmark`, `btnAutoSelect` |
|  |  | `txtTelToken`, `txtTelChatId` (Telegram) |
|  |  | `txtTvSymbol`, `txtTvChartId` (TradingView) |
| 🛡️ Spam & Güvenlik | `pnlSetSpam` | `chkSpamSignals`, `chkSpamBatches`, `chkSpamManual`, `chkSpamNews` |
| 🎯 Hedef & Otomasyon | `pnlSetTarget` | `txtTargetAccounts`, `chkAuto` |

**Key UI Elements:**
- **Benchmark Panel:** `pnlBenchmark` (satır ~1040-1080)
- **Kaydet Butonu:** `btnSave` (satır ~1155) → `BtnSave_Click`

### 🤖 OperatorForm (İcra Paneli)
*   **Intelligence:** Cortex Zeka Raporu (Sol Panel).
*   **Execution:** Tweet Zinciri (Sağ Panel), Başlat Butonu.
*   **Sentinel:** Canlı etkileşim akışı.

---

## 📍 Key Line References (Satır Haritası)

> **Not:** Bu satırlar değişebilir. Ancak arama yapmadan önce burayı kontrol edin.

### MainForm.cs - Panel Initialize Fonksiyonları
| Fonksiyon | Satır | Açıklama |
|-----------|-------|----------|
| `InitializeComponent` | 197-1208 | **ANA UI KURULUMU** - Tüm paneller, kontroller |
| `ShowPanel` | 1210-1238 | Panel görünürlük yönetimi |
| `InitializeInfluencerPanel` | 1247-1397 | Fenomenler sekmesi |
| `InitializeHistoryPanel` | 1468-1516 | Geçmiş sekmesi |
| `InitializeNewsPanel` | 1518-1598 | Haberler sekmesi |
| `InitializeChart` | 1806-1854 | TradingView grafik |
| `InitializeTwitterWebView` | 1977-1991 | X (Twitter) WebView |
| `InitializeServices` | 1993-2135 | Tüm servislerin başlatılması |
| `InitializeEngagementHub` | 4838-4886 | Etkileşim Merkezi |
| `InitializeManualAnalysisTab` | 5015-5227 | Manuel Analiz sekmesi |
| `InitializeBotInteractionTab` | 5275-5364 | Bot Etkileşim sekmesi |
| `InitializeGuruPanel` | 5487-5591 | Üstat Paneli |
| `InitializeFenerbahcePanel` | 5699-5828 | Fenerbahçe sekmesi |
| `InitializeHiveHub` | 5830-5883 | HIVE Intel hub |
| `InitializeMetaTeacherInto` | 5885-5953 | Meta-Teacher içgörüleri |
| `InitializeWisdomInto` | 5999-6015 | Wisdom kütüphanesi |
| `InitializeOmniScoutInto` | ~6030 | Omni-Scout UI (Yeni) |
| `InitializeOracleInto` | ~6080 | Oracle UI (Yeni) |

### MainForm.cs - Core Fonksiyonlar
| Fonksiyon | Satır | Açıklama |
|-----------|-------|----------|
| `LoadSettings` | 2138-2245 | Config'den UI'ya yükleme |
| `BtnSave_Click` | 2247-2334 | UI'dan Config'e kaydetme |
| `BtnStart_Click` | 2336-2361 | Watcherları başlatma |
| `PerformManualAnalysis` | 4137-4215 | Manuel analiz işlemi |
| `PostMorningMotivation` | 2558+ | **(v3.8.2)** Motivasyon tweeti ve zamanlaması |
| `ProcessTelegramCommands` | 4414-4756 | Telegram komutları (/ONAY, /ANALIZ vb.) |
| `ProcessSignal` | 3985-4126 | Sinyal işleme mantığı |
| `ProcessNewsQueue` | 3705-3915 | Haber kuyruğu işleme |
| `Log` / `LogAI` / `LogNews` | 4249-4312 | Loglama fonksiyonları |

### MainForm.cs - WebView & X (Twitter) Fonksiyonları
| Fonksiyon | Satır | Açıklama |
|-----------|-------|----------|
| `PerformInternalPostAsync` | 2708-2809 | Tweet atma (WebView2) |
| `PerformInternalThreadAsync` | 2811-3199 | Thread atma (WebView2) |
| `PerformInternalSearchAsync` | 3201-3532 | X arama (WebView2) |
| `SaveTwitterCookiesAsync` | 1875-1925 | Cookie kaydetme |
| `InjectTwitterCookiesAsync` | 1927-1975 | Cookie yükleme |

### MainForm.cs - UI Bölgeleri (InitializeComponent içinde)
| Bölge | Satır Aralığı | İçerik |
|-------|---------------|--------|
| Field Tanımları | 60-175 | Tüm UI kontrol tanımları |
| Panel Tanımları | 260-285 | `pnlDashboard`, `pnlSettings`, `pnlHive` vb. |
| Sidebar Navigation | 286-420 | `btnNav...` butonları |
| Dashboard Header | 425-530 | Sayaçlar, Ticker, Start/Stop |
| Settings Panel | 908-1165 | Tüm ayarlar UI |
| AI & Model Yönetimi | 939-1080 | Gemini/Perplexity, Benchmark |

### Services/ - Önemli Dosyalar
| Dosya | Satır | İçerik |
|-------|-------|--------|
| `ModelBenchmarkService.cs` | 55-125 | `FetchAvailableModelsAsync()` |
| `ModelBenchmarkService.cs` | 130-145 | `RunBenchmarkAsync()` |
| `ModelBenchmarkService.cs` | 290-385 | `UpdateTaskPreferencesFromResults()` — benchmark→ModelManager dinamik güncelleme |
| `ModelManager.cs` | 42-150 | `InitializeTaskPreferences()` |
| `ModelManager.cs` | 155-220 | `SendRequest()` + fallback |
| `GeminiService.cs` | ~580-720 | `SendRequest()` ana mantık |
| `SocialIntelService.cs` | ~200-400 | Python script çağrısı |
| `SentinelService.cs` | ~80-150 | `ProcessTweetReplies()` |
| `NewsEngine.cs` | ~100-200 | Haber işleme mantığı |
| `OperationManager.cs` | 295-305 | `SyncGeminiProviders()` model isimleri |

## 🔄 Workflow Examples (Akış Şemaları)

### 1. Kullanıcıdan Gelen "Analiz Talebi" Akışı
1.  **Algılama:** `SentinelService` -> `ProcessTweetReplies` çalışır.
2.  **Veri Çekme:** `SocialIntelService.cs` -> `social_intel.py` (`fetch_replies`) çağrılır.
3.  **Analiz:** Gelen yanıt `GeminiService` ile analiz edilir. "TALEP: THYAO" olduğu anlaşılır.
4.  **Aksiyon:** `OperatorForm` üzerinde kullanıcıya "Analiz İsteği Geldi" uyarısı düşer.

### 2. Meta-Teacher (Konsey) Döngüsü
1.  **Tetikleme:** Zamanlayıcı (Timer) `SocialIntelService.PerformMetaTeacherLoopAsync` metodunu çağırır.
2.  **Liste:** `InfluencerControlService` üzerinden "Konsey Üyeleri" listesi alınır.
3.  **Tarama:** Her üye için `social_intel.py` (`search_influencer`) çalıştırılır. Tarih filtresiyle (Since Date) yeni tweetler aranır.
4.  **Öğrenme:** Bulunan analizler `MemoryEngine` içine kaydedilir (`Learn`).
5.  **İçgörü:** Önemli bir strateji bulunursa `OnMetaTeacherInsight` eventi tetiklenir ve kullanıcıya sunulur.

### 3. Cortex Strateji Döngüsü (HIVE Phase 3)
1.  **Veri Hazırlığı:** `OmniScout` (Viral) ve `Oracle` (Piyasa) servisleri arka planda veri çeker ve `LastReport` değişkenini günceller.
2.  **Tetikleme:** Kullanıcı `OperatorForm` -> Sentez sekmesinden **"CORTEX ANALİZİ BAŞLAT"** butonuna basar.
3.  **Sentez:** `CortexService` tüm raporları `Gemini`'ye gönderir.
4.  **Sonuç:** AI, verileri çaprazlayarak (Cross-Reference) bir strateji üretir ve UI'da gösterir.

---

## ⚠️ Kritik Notlar & Kurallar

1.  **JSON İletişimi:** C# ve Python arasındaki veri alışverişi **her zaman JSON** formatındadır. Python tarafında `---JSON_START---` ve `---JSON_END---` markerları kullanılır.
2.  **Thread Safety:** `SentinelService` ve `OperationManager` asenkron çalışır. UI güncellemeleri için `Invoke` zorunludur.
3.  **Dil Kuralı:** Kod içi (değişkenler, yorumlar) İngilizce, **UI ve Loglar Türkçe** olmalıdır.
4.  **Hata Yönetimi:** Python scripti hata verirse JSON içinde `status: "error"` döner. C# tarafı bunu `Logger.Sys` ile loglamalıdır.

---

## 📂 Server Deployment Paths (Canlı Ortam)

Canlı sunucudaki (v3.7.6 ve sonrası) dosya yolları:

| İçerik | Sunucu Yolu |
| :--- | :--- |
| **Uygulama Dosyaları** | `G:\Diğer bilgisayarlar\Sunucu\XiDeAI Pro` |
| **Log Dosyaları** | `G:\Diğer bilgisayarlar\Sunucu\XiDeAI` |











































































