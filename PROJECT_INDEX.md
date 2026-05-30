> **Version:** 5.1.1 (Live)
> **Architecture:** Hybrid (C# WinForms + Python Playwright Thread Engine + Selenium Research Fallback + WebView2 Bridge)
> **Last Updated:** 2026-05-31

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
1. `SocialIntelService.PostThreadAsync` payload üretir (`preserve_chunks=true`).
2. `ThreadPipeline.cs` sinyal ve haber thread parçalarını normalize eder, lead tweet üretir.
3. `playwright_daemon.py` ilk tweeti yayınlar, sonra reply-chain ile devam eder.
4. URL yakalama kendi profile timeline/toast üzerinden yapılır (rastgele timeline linki engellenir).
5. C# sonuç kontrolü yalnızca `result.status == success` ise başarı sayar/loglar.

### ❌ Kaçınılacak Eski Davranışlar
- Aynı işlemde hem fail hem success logu yazmak.
- C# tarafından hazırlanmış thread parçalarını Python'da gereksiz yeniden parçalamak.
- Thread sayaçlarını parça sayısı yerine sabit +1 artırmak.

---

## 📂 Services Map (C#)

Tüm servisler `Services/` klasörü altındadır ve `OperationManager.cs` tarafından yönetilir.

| Servis Dosyası | Temel Görevleri | Bağlı Olduğu Python Script |
| :--- | :--- | :--- |
| **`SocialIntelService.cs`** | **Ana X (Twitter) Köprüsü.** Daemon-first mimari ile işlem yapar. `StartDaemonAsync()`, `DaemonRequestAsync()` metodları. | `x_daemon.py`, `social_intel.py` |
| **`OperationManager.cs`** | **Orkestra Şefi.** Servisleri başlatır, daemon'ı başlatır, durdurur ve birbirine bağlar. | - |
| `GeminiService.cs` | **AI Motoru.** Promptları işler, görsel analiz yapar (Vision) ve thread üretir. **v4.2.2:** Two-Step News metodları eklendi. | - |
| `ModelBenchmarkService.cs` | **v4.9.9** Gemini modellerini test eder, API'den canlı model listesi çeker, benchmark yapar. **v4.9.9:** `UpdateTaskPreferencesFromResults()` eklendi — benchmark sonucu ModelManager TaskType tercihlerini dinamik olarak günceller. | - |
| `NewsEngine.cs` | **v4.2.2:** Two-Step Logic (1-10 skor, 7 kategori, SON DAKIKA boost). Haber akışını kategorize eder ve işler. | `x_daemon.py` |
| `NewsTrackerService.cs` | RSS ve Twitter'dan haber tarar. **v3.8.3:** `OnNewsDetected` eventini tetikler. | `x_daemon.py` |
| `ThreadService.cs` | Zincir (Thread) oluşturma mantığını kurar. | - |
| `ThreadPipeline.cs` | **Merkezi Thread Hazırlayıcı.** Lead tweet, parça normalizasyonu ve ortak split kurallarını tek yerde toplar. | - |
| `InfluencerControlService.cs` | Takip edilecek fenomenlerin veritabanını yönetir. | - |
| `PriceFetchService.cs` | **Fiyat Motoru.** BIST ve Kripto paraların anlık fiyatını çeker. (Parallel Async). | - |
| `SignalEngine.cs` | Sinyal işleme motoru. Sinyalleri filtreler, formatlar ve yayınlar. | - |
| `ModelManager.cs` | **v4.10.0** AI provider yöneticisi. Aktif provider'ı seçer, fallback/routing yapar. `SyncGeminiProviders()` ile LMStudio dahil tüm provider'ları senkronize eder. | - |
| `LMStudioProvider.cs` | **v4.10.0** LM Studio / LM Link local model provider'ı (OpenAI uyumlu). `SendRequest()` + `SendRequestWithImage()` destekler. **v4.10.2:** `PrepareImageForVision()` — 4K DPI ekran görüntülerini 1024px JPEG'e dönüştürür. **v5.0.0:** `/no_think` prefix (Qwen3 reasoning bastırma), vision timeout 600s, `reasoning_content` fallback. | - |
| `ManualAnalysisService.cs` | **v4.10.8** Manuel analiz servisi. **Yerel model aktifken:** `IndicatorExtractor` atlanır, kısa thread için ekran görüntüsü tekrar gönderilmez, ana analiz metni indicator context olarak kullanılır. | - |

> **Not (v4.0.0):** HIVE servisleri (Sentinel, Apex, Omni, Oracle, Wisdom, Cortex) kaldırılmıştır. Yedek: `d:\Projects\HiveProjesi`

### 🔑 Key Classes & Methods

#### `SocialIntelService.cs`
- `FindInfluencerAnalyses(symbol, market)`: Fenomenlerin analizlerini arar. (Önce VIP timeline, sonra genel arama).
- `PostTweet(text)` / `PostThreadAsync(tweets)`: Tweet atar. Önce dahili WebView2'yi dener, başarısız olursa Python'a düşer (Fallback).
- `CheckSafety(actionType)`: **(v4.6.0)** Güvenlik kontrolü yapar (Hız limiti ve günlük kotalar).
- `PerformDeepScanAsync()`: Rastgele seçilen fenomenleri tarayarak bilgi tabanını günceller.

#### `ThreadService.cs`
- **(v4.10.8)** Tweet parçaları `.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5)` filtresiyle kısa/boş parçalar temizlenir.

#### `PromptManager.cs`
- **(v4.10.8)** Derin analiz prompt'una `### GÖRSEL OKUMA (GRAFİK)` bölümü eklendi — yerel modelin grafik okuma kalitesini artırır.
- **(v5.1.1)** `GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: Yeniden yazıldı. Eski tek-tweet şablon → 6-7 tweet fenomen thread yapısı (Hook → XU100 yorum → Yıldızlar → Kazazedeler → Pulse anları → Yarına bakış → CTA).
- **(v5.1.1)** Tüm `### GÖREV` bloklarına X Algoritma Fenomen Kuralları enjekte edildi: Hook (kanca ilk cümle), kısa/boşluklu format (dwell time), ELI5 hikayeleştirme, CTA (son tweette RT/takip).
- **(v5.1.1)** Contrarian Filter: `DailyTrends` = `[XU100_CANLI_VERI: MOD=X, TREND=Y%] YATIRIMCI_SOSYAL_ALGI: #...` — AI hard data ile sosyal algı zıtlığını Smart Money tuzağı olarak yorumlar.

#### `MainForm.cs`
- **(v5.1.1)** `RefreshTrendsAsync()`: `Market_Status.txt` okunur → `[XU100_CANLI_VERI: MOD, TREND%]` hard data + Twitter trendleri birleşik `DailyTrends` string'i oluşturur.
- **(v5.1.1)** `PostMarketCloseSummary()`: `Market_Pulse_Alarm.txt` okunarak bugünün nabız alarmları `pulseAnomalies` string'ine toplanır ve `GenerateMarketCloseTableTweet` zincirine iletilir.

#### `PerformanceTracker.cs`
- `RecordSignal(signal)`: Bot, Manuel veya Guru kaynaktan gelen sinyali veritabanına işler.

#### `GeminiService.cs`
- `AnalyzeChartImage(symbol, path)`: **(v3.9.0)** Grafik görsellerini teknik analize (RSI, Trend, Formasyon) dönüştürür.
- `GenerateGuruHonoringThread(...)`: Görsel analiz ve fiyat verisini kullanarak guru threadi üretir.
- `DetectNewsCategory(title, source)`: **(v4.2.2)** Haber kategorisini tespit eder (7 kategori).
- `AnalyzeNewsImpactTwoStep(title, source)`: **(v4.2.2)** Önce kategori, sonra 1-10 skor üretir.
- `GenerateNewsCategoryAnalysis(category, title, source, link)`: **(v4.2.2)** Kategoriye özel analiz thread'i üretir.
- `SendRequest(prompt)`: AI modeline metin tabanlı istek gönderir.
- `GenerateMarketCloseTableTweet(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: **(v5.1.1)** Gün sonu kapanış tweet thread'i üretir. `pulseAnomalies` parametresi ile gün içi nabız alarmlarını (Market_Pulse_Alarm.txt) fenomen thread formatına (6-7 tweet) dönüştürür.

#### `LMStudioProvider.cs`
- `SendRequest(prompt)`: LM Studio'ya metin isteği gönderir (OpenAI compat). **(v5.0.0)** Prompt başına `/no_think\n` prefix eklenir, timeout 300s.
- `SendRequestWithImage(prompt, imagePath)`: Görsel + metin isteği gönderir. **(v5.0.0)** Timeout 600s, `max_tokens` minimum 8192.
- `PrepareImageForVision(imagePath, maxDimension)`: **(v4.10.2)** Görseli max 1024px'e küçültür ve JPEG 85% kalitesinde kodlar.
- `ExtractContentFromChoice(choice)`: **(v5.0.0)** `content` boşsa `reasoning_content`'e fallback yapar.

#### `ManualAnalysisService.cs`
- **(v4.10.8)** Yerel model aktifken `IndicatorExtractor` çağrısı atlanır (token tasarrufu).
- **(v4.10.8)** Kısa thread üretiminde yerel model için ekran görüntüsü tekrar gönderilmez.
- **(v4.10.8)** Yerel modele indicator context yerine ana analiz metni iletilir.

---

## 🐍 Python Scripts Map (Scripts/)

Python scriptleri "Worker" (İşçi) olarak çalışır. C# tarafından komut satırı argümanları ile çağrılır ve JSON çıktısı üretirler.

| Script Dosyası | Görev Tanımı | Kütüphaneler |
| :--- | :--- | :--- |
| **`playwright_daemon.py`** | **(v4.9.6 Yeni) Thread & Yayın Motoru.** X-Hive bazlı yeni süper hızlı bot. Emoji/Link hesabını natif olarak yapıp 1/n thread oluşturur. | `playwright.async_api` |
| **`x_daemon.py`** | **HTTP Daemon (localhost:5580).** Tek Chrome instance ile sürekli çalışır. **(v4.9.4)** `_post_single_tweet` URL yakalama - home fallback kaldırıldı, toast/profile retry eklendi. | `selenium`, `undetected_chromedriver` |
| **`social_intel.py`** | **Dev X Otomasyonu.** Selenium ile giriş yapar, arama yapar, veri çeker, etkileşim kurar. **(v4.9.3)** `_post_one(media_path=None)` — 1. tweet'e grafik görseli desteği eklendi. | `selenium`, `pickle` |
| `omni_scout.py` | Reddit ve diğer kaynaklardan viral veri çeker. | `praw` (Reddit API) |
| `oracle.py` | Tahmin piyasaları verisi (Polymarket vs.) | `requests` |
| `screenshot.py` | BIST/Crypto grafiklerinin ekran görüntüsünü alır. | `selenium` |
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























































































