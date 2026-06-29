> **Version:** 5.6.4
> **Architecture:** Hybrid (C# WinForms + Canonical PostingService + Python Playwright Posting Engine + Selenium Research Fallback + WebView2 Session Bridge)
> **Last Updated:** 29 June 2026

Bu indeks, proje ÃƒÂ¼zerinde ÃƒÂ§alÃ„Â±Ã…Å¸acak yapay zeka ve geliÃ…Å¸tiriciler iÃƒÂ§in **kod tabanÃ„Â±nÃ„Â±n haritasÃ„Â±nÃ„Â±** sunar. Yeni ÃƒÂ¶zellik eklerken veya hata dÃƒÂ¼zeltirken burayÃ„Â± referans alÃ„Â±nÃ„Â±z.

---

## ÄŸÅ¸Ââ€”Ã¯Â¸Â Core Architecture (Hibrit YapÃ„Â±)

Proje 4 ana katmandan oluÃ…Å¸ur:
1.  **Orchestrator (C#):** TÃƒÂ¼m mantÃ„Â±Ã„Å¸Ã„Â± yÃƒÂ¶neten, servisleri baÃ…Å¸latan ve kararlarÃ„Â± veren katman. (`OperationManager`)
2.  **Publishing Engine (Python Playwright):** Thread gÃƒÂ¶nderiminde ana motor. (`playwright_daemon.py`)
3.  **Interaction Layer (Hybrid):**
    *   **Playwright-First (Thread/Tweet):** GÃƒÂ¶nderim iÃ…Å¸lemleri Playwright motoru ile yÃƒÂ¼rÃƒÂ¼r.
    *   **WebView2 Fallback:** KullanÃ„Â±cÃ„Â± gÃƒÂ¶rÃƒÂ¼nÃƒÂ¼rlÃƒÂ¼Ã„Å¸ÃƒÂ¼ gerektiren iÃ…Å¸lemler iÃƒÂ§in yedek.
    *   **Python/Selenium Fallback:** Fenomen tarama/araÃ…Å¸tÃ„Â±rma iÃƒÂ§in halen aktif.
4.  **Intelligence Layer (AI):** LM Studio (Yerel Model, Birincil) + Gemini/Perplexity (Yedek/Bulut) entegrasyonu.

### Ã¢Å“â€¦ Canonical Publishing Flow (Tek GerÃƒÂ§ek Hat)
1. ModÃƒÂ¼ller sadece iÃƒÂ§erik ÃƒÂ¼retir; gÃƒÂ¶nderimi `PostingService` yapar.
2. `PostingService.PostTweetAsync` / `PostThreadAsync` tÃƒÂ¼m tweet/thread payloadlarÃ„Â±nÃ„Â± normalize eder ve `SocialIntelService` alt motoruna iletir.
3. `SocialIntelService` canonical olarak `Scripts/playwright_daemon.py` kullanÃ„Â±r; WebView2 internal bridge debug-only bÃ„Â±rakÃ„Â±lmÃ„Â±Ã…Å¸tÃ„Â±r.
4. `playwright_daemon.py` gerÃƒÂ§ek `/status/<id>` URL yakalamadan success dÃƒÂ¶nmez; thread iÃƒÂ§in `posted_count == total_chunks` beklenir.
5. C# tarafÃ„Â±nda success sadece `PostingService.IsVerifiedTweet/IsVerifiedThread` doÃ„Å¸rulamasÃ„Â±ndan sonra kabul edilir.

### Ã¢ÂÅ’ KaÃƒÂ§Ã„Â±nÃ„Â±lacak Eski DavranÃ„Â±Ã…Å¸lar
- AynÃ„Â± iÃ…Å¸lemde hem fail hem success logu yazmak.
- C# tarafÃ„Â±ndan hazÃ„Â±rlanmÃ„Â±Ã…Å¸ thread parÃƒÂ§alarÃ„Â±nÃ„Â± Python'da gereksiz yeniden parÃƒÂ§alamak.
- Thread sayaÃƒÂ§larÃ„Â±nÃ„Â± parÃƒÂ§a sayÃ„Â±sÃ„Â± yerine sabit +1 artÃ„Â±rmak.
- WebView2 modal kapandÃ„Â± veya butona tÃ„Â±klandÃ„Â± diye post'u baÃ…Å¸arÃ„Â± saymak.

---

## ÄŸÅ¸â€œâ€š Services Map (C#)

TÃƒÂ¼m servisler `Services/` klasÃƒÂ¶rÃƒÂ¼ altÃ„Â±ndadÃ„Â±r ve `OperationManager.cs` tarafÃ„Â±ndan yÃƒÂ¶netilir.

| Servis DosyasÃ„Â± | Temel GÃƒÂ¶revleri | BaÃ„Å¸lÃ„Â± OlduÃ„Å¸u Python Script |
| :--- | :--- | :--- |
| **`SocialIntelService.cs`** | **DÃƒÂ¼Ã…Å¸ÃƒÂ¼k seviye X (Twitter) kÃƒÂ¶prÃƒÂ¼sÃƒÂ¼.** PostingService tarafÃ„Â±ndan ÃƒÂ§aÃ„Å¸rÃ„Â±lÃ„Â±r; Playwright subprocess ile doÃ„Å¸rulanmÃ„Â±Ã…Å¸ gÃƒÂ¶nderim yapar. AraÃ…Å¸tÃ„Â±rma/interaction iÃƒÂ§in daemon ve Selenium fallback iÃƒÂ§erir. | `playwright_daemon.py`, `x_daemon.py`, `social_intel.py` |
| **`PostingService.cs`** | **v5.3.0 Canonical gÃƒÂ¶nderim servisi.** TÃƒÂ¼m modÃƒÂ¼ller iÃƒÂ§in tek tweet/thread doÃ„Å¸rulama kapÃ„Â±sÃ„Â±. `/status/` URL ve thread parÃƒÂ§a sayÃ„Â±sÃ„Â± doÃ„Å¸rulanmadan success dÃƒÂ¶nmez. | `playwright_daemon.py` |
| **`MemoryEngine.cs`** | **v5.6.4 Akıllı Hafıza Motoru (Knowledge Base).** Fenomen tweetlerini depolar/prune eder ve robotun kendi ürettiği analiz geçmişini/cooldown durumunu yönetir. | - |
| **`OperationManager.cs`** | **Orkestra Ã…Âefi.** Servisleri baÃ…Å¸latÃ„Â±r, daemon'Ã„Â± baÃ…Å¸latÃ„Â±r, durdurur ve birbirine baÃ„Å¸lar. | - |
| `GeminiService.cs` | **AI Motoru.** PromptlarÃ„Â± iÃ…Å¸ler, gÃƒÂ¶rsel analiz yapar (Vision) ve thread ÃƒÂ¼retir. **v4.2.2:** Two-Step News metodlarÃ„Â± eklendi. | - |
| `ModelBenchmarkService.cs` | **v4.9.9** Gemini modellerini test eder, API'den canlÃ„Â± model listesi ÃƒÂ§eker, benchmark yapar. **v4.9.9:** `UpdateTaskPreferencesFromResults()` eklendi Ã¢â‚¬â€ benchmark sonucu ModelManager TaskType tercihlerini dinamik olarak gÃƒÂ¼nceller. | - |
| `NewsEngine.cs` | **v4.2.2:** Two-Step Logic (1-10 skor, 7 kategori, SON DAKÃ„Â°KA boost). Haber akÃ„Â±Ã…Å¸Ã„Â±nÃ„Â± kategorize eder ve iÃ…Å¸ler. **v5.1.3:** Flash haber garantili 2-tweet format (`GetFlashNewsAnalysisPrompt`); `BuildMinimalNewsTweet` Ã¢â‚¬â€ AI null dÃƒÂ¶nse bile baÃ…Å¸lÃ„Â±k+link tweet'i gÃƒÂ¶nderilir; maxTokens 800/900'a yÃƒÂ¼kseldi; `BuildNewsLeadTweet` link+flash tag eklendi. **v5.1.4:** CS8602 null guard Ã¢â‚¬â€ `threadContent != null &&` eklendi. |
| `NewsTrackerService.cs` | RSS ve Twitter'dan haber tarar. **v3.8.3:** `OnNewsDetected` eventini tetikler. | `x_daemon.py` |
| `ThreadService.cs` | Zincir (Thread) oluÃ…Å¸turma mantÃ„Â±Ã„Å¸Ã„Â±nÃ„Â± kurar. | - |
| `ThreadPipeline.cs` | **Merkezi Thread HazÃ„Â±rlayÃ„Â±cÃ„Â±.** Lead tweet, parÃƒÂ§a normalizasyonu ve ortak split kurallarÃ„Â±nÃ„Â± tek yerde toplar. | - |
| `SymbolNormalizer.cs` | **v5.2.3** Merkezi sembol normalizasyonu ve BIST sembol doÃ„Å¸rulamasÃ„Â±. `VIP'VIP-VAKBN`, `VIP-VAKBN`, `BIST:VAKBN` gibi varyantlarÃ„Â± canonical sembole indirger; kÃ„Â±rpÃ„Â±lmÃ„Â±Ã…Å¸/bozuk sembolleri engeller. | - |
| `InfluencerControlService.cs` | Takip edilecek fenomenlerin veritabanÃ„Â±nÃ„Â± yÃƒÂ¶netir. **(v5.2.2)** `UpdateScore(handle, delta)` eklendi Ã¢â‚¬â€ engagement bazlÃ„Â± otomatik skor gÃƒÂ¼ncelleme (0-100 aralÃ„Â±Ã„Å¸Ã„Â±). | - |
| `PriceFetchService.cs` | **Fiyat Motoru.** BIST ve Kripto paralarÃ„Â±n anlÃ„Â±k fiyatÃ„Â±nÃ„Â± ÃƒÂ§eker. (Parallel Async). | - |
| `SignalEngine.cs` | Sinyal iÃ…Å¸leme motoru. Sinyalleri filtreler, formatlar ve yayÃ„Â±nlar. | - |
| `LogFileWatcher.cs` | **v5.2.3** `Sinyal_Log_Database.txt` iÃƒÂ§in snapshot tabanlÃ„Â± izleme. Byte-tail kaynaklÃ„Â± satÃ„Â±r ortasÃ„Â± okuma (`ACSELÃ¢â€ â€™SEL`, `DEVAÃ¢â€ â€™VA`) engellendi. | - |
| `SignalParser.cs` | **v5.2.3** Strict Alpha/PreMove parse. Header, `KAPALI`, bilinmeyen BIST sembolÃƒÂ¼, fiyatÃ„Â± sÃ„Â±fÃ„Â±r ve geÃƒÂ§ersiz durumlar atlanÃ„Â±r. | - |
| `ModelManager.cs` | **v4.10.0** AI provider yÃƒÂ¶neticisi. Aktif provider'Ã„Â± seÃƒÂ§er, fallback/routing yapar. `SyncGeminiProviders()` ile LMStudio dahil tÃƒÂ¼m provider'larÃ„Â± senkronize eder. | - |
| `LMStudioProvider.cs` | **v4.10.0** LM Studio / LM Link local model provider'Ã„Â± (OpenAI uyumlu). `SendRequest()` + `SendRequestWithImage()` destekler. **v4.10.2:** `PrepareImageForVision()` Ã¢â‚¬â€ 4K DPI ekran gÃƒÂ¶rÃƒÂ¼ntÃƒÂ¼lerini 1024px JPEG'e dÃƒÂ¶nÃƒÂ¼Ã…Å¸tÃƒÂ¼rÃƒÂ¼r. **v5.0.0:** `/no_think` prefix (Qwen3 reasoning bastirma), vision timeout 600s. **v5.1.3:** `reasoning_content` fallback KALDIRILDI Ã¢â‚¬â€ `content=boÃ…Å¸`+`finish_reason=length` durumunda `null` dÃƒÂ¶ndÃƒÂ¼rÃƒÂ¼lÃƒÂ¼yor; `finish_reason=length` logu eklendi. **v5.1.4:** `AnalyzeNewsUnified` maxTokens 450Ã¢â€ â€™1500 (Qwen3.6-27b finish_reason=length sorunu ÃƒÂ§ÃƒÂ¶zÃƒÂ¼ldÃƒÂ¼); prompt'a "dÃƒÂ¼Ã…Å¸ÃƒÂ¼nme adÃ„Â±mÃ„Â± YOK" hint eklendi. |
| `ManualAnalysisService.cs` | **v4.10.8** Manuel analiz servisi. **Yerel model aktifken:** `IndicatorExtractor` atlanÃ„Â±r, kÃ„Â±sa thread iÃƒÂ§in ekran gÃƒÂ¶rÃƒÂ¼ntÃƒÂ¼sÃƒÂ¼ tekrar gÃƒÂ¶nderilmez, ana analiz metni indicator context olarak kullanÃ„Â±lÃ„Â±r. | - |

> **Not (v4.0.0):** HIVE servisleri (Sentinel, Apex, Omni, Oracle, Wisdom, Cortex) kaldÃ„Â±rÃ„Â±lmÃ„Â±Ã…Å¸tÃ„Â±r. Yedek: `d:\Projects\HiveProjesi`

### ÄŸÅ¸â€â€˜ Key Classes & Methods

#### `SocialIntelService.cs`
- `FindInfluencerAnalyses(symbol, market)`: Fenomenlerin analizlerini arar. (Ãƒâ€“nce VIP timeline, sonra genel arama).
- **(v5.2.2)** Daemon'dan post alÃ„Â±nÃ„Â±nca `engagement/10` formÃƒÂ¼lÃƒÂ¼yle `InfluencerControlService.UpdateScore()` ÃƒÂ§aÃ„Å¸rÃ„Â±lÃ„Â±r Ã¢â‚¬â€ etkin fenomenler ÃƒÂ¼ste ÃƒÂ§Ã„Â±kar.
- **(v5.2.2)** Genel arama parse hatasÃ„Â±nda handle boÃ…Å¸ kalÃ„Â±rsa tweet atlanÃ„Â±r (eski: `X-User` fallback kaldÃ„Â±rÃ„Â±ldÃ„Â±).
- **(v5.2.3)** `IsBadSocialResult(author, content, url)`: kendi hesap, bot ÃƒÂ§Ã„Â±ktÃ„Â±sÃ„Â± (`Piyasa GÃƒÂ¶rÃƒÂ¼Ã…Å¸leri`, `Teknik Analizim`, `XiDeAI`), `ERROR_404` ve ana sayfa URL sonuÃƒÂ§larÃ„Â±nÃ„Â± filtreler.
- `PostTweet(text)` / `PostThreadAsync(tweets)`: DÃƒÂ¼Ã…Å¸ÃƒÂ¼k seviye Playwright posting kÃƒÂ¶prÃƒÂ¼sÃƒÂ¼. **v5.3.0:** WebView2 internal bridge canonical yoldan ÃƒÂ§Ã„Â±karÃ„Â±ldÃ„Â±; `/status/` URL ve `posted_count/total_chunks` doÃ„Å¸rulamasÃ„Â± zorunlu.
- `CheckSafety(actionType)`: **(v4.6.0)** GÃƒÂ¼venlik kontrolÃƒÂ¼ yapar (HÃ„Â±z limiti ve gÃƒÂ¼nlÃƒÂ¼k kotalar).
- `PerformDeepScanAsync()`: Rastgele seÃƒÂ§ilen fenomenleri tarayarak bilgi tabanÃ„Â±nÃ„Â± gÃƒÂ¼nceller.
- **(v5.3.0)** `PostTweet`/`PostThreadAsync` yalnÃ„Â±zca `PostingService` tarafÃ„Â±ndan production gÃƒÂ¶nderim iÃƒÂ§in kullanÃ„Â±lmalÃ„Â±dÃ„Â±r; internal WebView2 bridge canonical yoldan ÃƒÂ§Ã„Â±karÃ„Â±ldÃ„Â±.

#### `PostingService.cs`
- **(v5.3.0)** `PostTweetAsync(text, mediaPath, module)`: Tekil tweetleri canonical Playwright hattÃ„Â±na gÃƒÂ¶nderir, gerÃƒÂ§ek `/status/` URL yoksa hata dÃƒÂ¶ndÃƒÂ¼rÃƒÂ¼r.
- **(v5.3.0)** `PostThreadAsync(parts, mediaPath, module)`: Thread parÃƒÂ§alarÃ„Â±nÃ„Â± `ThreadPipeline.EnsureWithinLimit` ile normalize eder; tÃƒÂ¼m parÃƒÂ§alar gÃƒÂ¶nderilmeden success dÃƒÂ¶nmez.
- **(v5.3.0)** `IsVerifiedTweet` / `IsVerifiedThread`: Uygulama genelinde tek baÃ…Å¸arÃ„Â± standardÃ„Â±.

#### `MemoryEngine.cs` (v5.6.4)
- **(v5.6.4)** `HasRecentAnalysisPosted(symbol, maxAgeHours)`: Robotun son N saat içinde aynı sembol için analiz/thread paylaşıp paylaşmadığını kontrol eder.
- **(v5.6.4)** `Recall(symbol, maxAgeHours)`: Belirli bir sembol için hafızadaki tweetleri getirir.
- **(v5.6.4)** `SaveKnowledgeBase()`: 10 günden eski tweet verilerini temizleyerek (pruning) veritabanını kaydeder.

#### `FanZoneService.cs`
- **(v5.3.0)** Kritik hesap taramasÃ„Â±nda tweet URL'si iÃ…Å¸lem ÃƒÂ¶ncesi deÃ„Å¸il `ProcessTweet` iÃƒÂ§inde dedupe edilir; like/RT baÃ…Å¸arÃ„Â± ikonlarÃ„Â± yalnÃ„Â±zca gerÃƒÂ§ek `status=success` dÃƒÂ¶nerse iÃ…Å¸aretlenir.

#### `InteractionEngine.cs`
- **(v5.3.0)** `RunTargetedCheck(category)` artÃ„Â±k `Influencer.Handle` deÃ„Å¸erlerini gÃƒÂ¶nderir; ÃƒÂ¶nceki `string.Join(targets)` class-name ÃƒÂ¼retme hatasÃ„Â± giderildi.
- **(v5.3.2)** Viral reply adaylarÃ„Â± ÃƒÂ¶neri aÃ…Å¸amasÃ„Â±nda interaction memory'ye yazÃ„Â±lmaz; sadece onaylÃ„Â± yanÃ„Â±t gerÃƒÂ§ek baÃ…Å¸arÃ„Â±yla gÃƒÂ¶nderildikten sonra iÃ…Å¸aretlenir.
- **(v5.3.3)** Otomatik bot dÃƒÂ¶ngÃƒÂ¼sÃƒÂ¼nden direkt Like/RT kaldÃ„Â±rÃ„Â±ldÃ„Â±. Hedef fenomen etkileÃ…Å¸imi sadece manuel tetiklenir; varsayÃ„Â±lan aksiyon yavaÃ…Å¸ modda Like-only, Python tarafÃ„Â±nda son 6 saat + gerÃƒÂ§ek tweet sahibi filtresi zorunludur.

#### `ThreadService.cs`
- **(v4.10.8)** Tweet parÃƒÂ§alarÃ„Â± `.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5)` filtresiyle kÃ„Â±sa/boÃ…Å¸ parÃƒÂ§alar temizlenir.
- **(v5.3.0)** Sinyal, batch, gÃƒÂ¼nlÃƒÂ¼k/haftalÃ„Â±k rapor threadleri `PostingService` ÃƒÂ¼zerinden gÃƒÂ¶nderilir.
- **(v5.3.4)** Sinyal threadleri en fazla 4 parÃƒÂ§a ile sÃ„Â±nÃ„Â±rlandÃ„Â±; beÃ„Å¸eni/RT ÃƒÂ§aÃ„Å¸rÃ„Â±sÃ„Â± kaldÃ„Â±rÃ„Â±ldÃ„Â±, sonuÃƒÂ§ tweeti seviye/teyit/risk diline ÃƒÂ§ekildi.
- **(v5.4.1)** Sinyal lead tweetlerinde ham `AKTIF/PULLBACK_ADAY` yerine takipÃƒÂ§i dostu `Sinyal canlÃ„Â±, teyit aranÃ„Â±yor` / `Geri ÃƒÂ§ekilme takibi, acele yok` etiketleri kullanÃ„Â±lÃ„Â±r.

#### `LogFileWatcher.cs`
- **(v5.2.3)** `LoadSeenKeys(path)`: servis baÃ…Å¸larken mevcut aÃƒÂ§Ã„Â±k sinyalleri hafÃ„Â±zaya alÃ„Â±r, geÃƒÂ§miÃ…Å¸ satÃ„Â±rlarÃ„Â± tekrar tetiklemez.
- **(v5.2.3)** `ReadStableLines(path)`: iDeal dosyasÃ„Â±nÃ„Â± snapshot olarak okur; rewrite edilen dosyada byte offset kullanÃ„Â±lmaz.
- **(v5.2.3)** `TryBuildSignalKey(line, out key, out strategy)`: `Symbol|Strategy|Period|Tarih|Durum` anahtarÃ„Â± ÃƒÂ¼retir, `KAPALI` ve geÃƒÂ§ersiz satÃ„Â±rlarÃ„Â± atlar.

#### `SignalParser.cs`
- **(v5.2.3)** `ParseDbLine(line, strategyOverride)`: 12 kolonlu iDeal DB satÃ„Â±rlarÃ„Â±nÃ„Â± strict parse eder; `D` periyodu `G` olarak normalize edilir; `SymbolNormalizer` ile canonical sembol kullanÃ„Â±lÃ„Â±r.

#### `SignalPersistenceService.cs`
- **(v5.3.0)** Processed key artÃ„Â±k `Symbol|Strategy|Period|Durum|DetectedAt` tabanlÃ„Â±dÃ„Â±r; aynÃ„Â± sembol/periyotta farklÃ„Â± strateji veya yeni tarihli sinyal yanlÃ„Â±Ã…Å¸lÃ„Â±kla bastÃ„Â±rÃ„Â±lmaz.

#### `SymbolNormalizer.cs`
- **(v5.2.3)** `NormalizeSignalSymbol(rawSymbol)`: `VIP'VIP-VAKBN` ve benzeri bozuk prefixleri temizleyerek canonical BIST sembolÃƒÂ¼ ÃƒÂ¼retir.
- **(v5.2.3)** `IsKnownBistSymbol(symbol)`: `Config/symbols_bist.txt` ÃƒÂ¼zerinden sembol doÃ„Å¸rular; config yoksa makul BIST formatÃ„Â±na fallback yapar.

#### `PromptManager.cs`
- **(v4.10.8)** Derin analiz prompt'una `### GÃƒâ€“RSEL OKUMA (GRAFÃ„Â°K)` bÃƒÂ¶lÃƒÂ¼mÃƒÂ¼ eklendi Ã¢â‚¬â€ yerel modelin grafik okuma kalitesini artÃ„Â±rÃ„Â±r.
- **(v5.1.1)** `GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: Yeniden yazÃ„Â±ldÃ„Â±. Eski tek-tweet Ã…Å¸ablon Ã¢â€ â€™ 6-7 tweet fenomen thread yapÃ„Â±sÃ„Â± (Hook Ã¢â€ â€™ XU100 yorum Ã¢â€ â€™ YÃ„Â±ldÃ„Â±zlar Ã¢â€ â€™ Kazazedeler Ã¢â€ â€™ Pulse anlarÃ„Â± Ã¢â€ â€™ YarÃ„Â±na bakÃ„Â±Ã…Å¸ Ã¢â€ â€™ CTA).
- **(v5.1.1)** TÃƒÂ¼m `### GÃƒâ€“REV` bloklarÃ„Â±na X Algoritma Fenomen KurallarÃ„Â± enjekte edildi: Hook (kanca ilk cÃƒÂ¼mle), kÃ„Â±sa/boÃ…Å¸luklu format (dwell time), ELI5 hikayeleÃ…Å¸tirme, CTA (son tweette RT/takip).
- **(v5.1.1)** Contrarian Filter: `DailyTrends` = `[XU100_CANLI_VERI: MOD=X, TREND=Y%] YATIRIMCI_SOSYAL_ALGI: #...` Ã¢â‚¬â€ AI hard data ile sosyal algÃ„Â± zÃ„Â±tlÃ„Â±Ã„Å¸Ã„Â±nÃ„Â± Smart Money tuzagÃ„Â± olarak yorumlar.
- **(v5.2.2)** `GetSignalAnalysisPrompt`, `GetDeepManualAnalysisPrompt`, `GetDeepTechnicalAnalysisPrompt`: YASAK SÃƒâ€“ZCÃƒÅ“KLER listesi eklendi (fÃ„Â±sÃ„Â±ltÃ„Â± alÃ„Â±Ã…Å¸, akÃ„Â±llÃ„Â± para, piyasa kurdu vb.). Son tweet ZORUNLU: AL/Ã„Â°ZLE/BEKLE karar + soru formatÃ„Â±.
- **(v5.5.6)** GetSporReplyPrompt: Yeni "SPOR" kategorisi eklendi. Transfer, maÃ§ ve spor kulÃ¼bÃ¼ (FenerbahÃ§e vb.) paylaÅŸÄ±mlarÄ± iÃ§in Ã¶zel taraftar/spor yorumcusu promptu oluÅŸturuldu.
- **(v5.5.6)** TÃ¼m X (Twitter) yanÄ±t promptlarÄ±na (GetReplyGenerationPrompt vb.) kural gÃ¼ncellemesi (EK KURALLAR kural 3): YanÄ±tlarÄ±n zorunlu soru sormasÄ± engellendi ve makul kÄ±salÄ±kta olmasÄ± saÄŸlandÄ±.
- **(v5.2.2)** `GetAlphaSignalPrompt` / `GetPreMoveSignalPrompt`: Robotik ton kaldÃ„Â±rÃ„Â±ldÃ„Â± (borsa kurdu, fÃ„Â±sÃ„Â±ldayan vb.). Fenomen mention: varsa doÃ„Å¸al, yoksa ekleme (zorunlu deÃ„Å¸il).
- **(v5.2.9)** `GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash, sectorMap)`: `sectorMap` parametresi eklendi. `GetEkonomiNewsAnalysisPrompt`, `GetTeknolojiNewsAnalysisPrompt`, `GetYasamNewsAnalysisPrompt` BIST SektÃƒÂ¶r HaritasÃ„Â±'nÃ„Â± prompt'a enjekte eder; halusinatÃƒÂ¶r sembol ÃƒÂ¼retimi engellendi. Her kategori prompt'u "TAM OLARAK 3 TWEET" ve `|||` zorunluluÃ„Å¸u ile gÃƒÂ¼ncellendi.
- **(v5.4.9)** `GetGuruHonoringThreadPrompt`: Takas ve AKD analizi iÃƒÂ§in "DiÃ„Å¸er" kuralÃ„Â±, T+2 gecikmesi ve kurumsal/bireysel oranlama mantÃ„Â±Ã„Å¸Ã„Â± (`takasRulesSection`) eklendi.

#### `MainForm.cs`
- **(v5.1.1)** `RefreshTrendsAsync()`: `Market_Status.txt` okunur Ã¢â€ â€™ `[XU100_CANLI_VERI: MOD, TREND%]` hard data + Twitter trendleri birleÃ…Å¸ik `DailyTrends` string'i oluÃ…Å¸turur.
- **(v5.1.1)** `PostMarketCloseSummary()`: `Market_Pulse_Alarm.txt` okunarak bugÃƒÂ¼nÃƒÂ¼n nabÃ„Â±z alarmlarÃ„Â± `pulseAnomalies` string'ine toplanÃ„Â±r ve `GenerateMarketCloseTableTweet` zincirine iletilir.
- **(v5.3.0)** Sabah motivasyon ve gÃƒÂ¼n sonu raporu `_tweetedToday` iÃƒÂ§ine sadece doÃ„Å¸rulanmÃ„Â±Ã…Å¸ baÃ…Å¸arÃ„Â±dan sonra iÃ…Å¸lenir; `*_PENDING` guard eklendi.
- **(v5.3.0)** Fenomen silme UI'Ã„Â± `InfluencerControlService.DeleteInfluencer()` kullanÃ„Â±r; kopya liste ÃƒÂ¼zerinden silme hatasÃ„Â± giderildi.
- **(v5.3.0)** Telegram `/ONAY` etkileÃ…Å¸im onayÃ„Â± reply sonucunu kontrol eder; baÃ…Å¸arÃ„Â±sÃ„Â±zsa pending kayÃ„Â±t silinmez.
- **(v5.3.0)** Manuel analiz tweet butonu sadece baÃ…Å¸arÃ„Â±lÃ„Â± analiz sonucunda aktif olur.
- **(v5.3.2)** Bot EtkileÃ…Å¸im tabÃ„Â±na manuel `Ã…Âimdi Tara`, `BIST Fenomen`, `Kripto Fenomen`, `Durum` kontrolleri eklendi; checkbox timer'Ã„Â± baÃ…Å¸latÃ„Â±r/durdurur.
- **(v5.3.2)** `/BOTDURUM`, `/ETKILESIMTARA`, `/ETKILESIMTEST @handle` Telegram komutlarÃ„Â± eklendi.
- **(v5.3.3)** `CheckForInteractions()` artÃ„Â±k otomatik Like/RT yapmaz; yalnÃ„Â±zca taze, geÃƒÂ§erli handle'lÃ„Â±, spam olmayan tweetler iÃƒÂ§in onaylÃ„Â± reply adayÃ„Â± ÃƒÂ¼retir.
- **(v5.3.4)** Manuel analiz paylaÃ…Å¸Ã„Â±mÃ„Â± sadece doÃ„Å¸rulanmÃ„Â±Ã…Å¸ 4 parÃƒÂ§alÃ„Â±k `ShortThread` ile yapÃ„Â±lÃ„Â±r; detay rapor artÃ„Â±k X thread'e fallback edilmez.
- **(v5.3.4)** GÃƒÂ¼n sonu ÃƒÂ¶zeti paylaÃ…Å¸Ã„Â±mÃ„Â± en fazla 4 tweet ile sÃ„Â±nÃ„Â±rlandÃ„Â±; factual kapanÃ„Â±Ã…Å¸ formatÃ„Â± ve YTD gÃƒÂ¼venliÃ„Å¸i zorunlu.
- **(v5.3.5)** Telegram `/ANALIZ` UI ile aynÃ„Â± `TradingViewChartId` akÃ„Â±Ã…Å¸Ã„Â±nÃ„Â± kullanÃ„Â±r; opsiyonel ÃƒÂ¼ÃƒÂ§ÃƒÂ¼ncÃƒÂ¼ argÃƒÂ¼man baz (`TL/USD/EUR/XU100`) olarak alÃ„Â±nÃ„Â±r.
- **(v5.3.5)** Analiz kimliÃ„Å¸i sade, seviye/teyit/risk odaklÃ„Â± tona ÃƒÂ§ekildi; fenomen persona/clickbait/FOMO dili azaltÃ„Â±ldÃ„Â±.
- **(v5.3.6)** EtkileÃ…Å¸im reply ÃƒÂ¼retimi kategori personasÃ„Â± yerine nÃƒÂ¶tr kÃ„Â±sa editÃƒÂ¶r tonu kullanÃ„Â±r; hassas/alakasÃ„Â±z iÃƒÂ§erikte `SKIP` cevabÃ„Â± aksiyonu iptal eder.
- **(v5.3.7)** Telegram haber onay bildirimleri kÃ„Â±sa, dÃƒÂ¼z metin ve karar odaklÃ„Â± formata alÃ„Â±ndÃ„Â±; uzun reasoning/summary kaynaklÃ„Â± Markdown riski azaltÃ„Â±ldÃ„Â±.
- **(v5.3.9)** Tek tweet manuel paylaÃ…Å¸Ã„Â±mÃ„Â± 280+ karakteri otomatik thread'e ÃƒÂ§evirmez; kullanÃ„Â±cÃ„Â± aÃƒÂ§Ã„Â±kÃƒÂ§a thread modunu seÃƒÂ§mek zorundadÃ„Â±r.
- **(v5.4.0)** Manuel analiz short-thread formatÃ„Â± 4-8 parÃƒÂ§aya ÃƒÂ§Ã„Â±karÃ„Â±ldÃ„Â±; ilk 2 tweet kÃ„Â±sa ÃƒÂ¶zet/devam rehberi, sonraki tweetler seviye/teyit/risk detaylarÃ„Â±dÃ„Â±r. 120 karakter altÃ„Â± parÃƒÂ§alar geÃƒÂ§ersiz sayÃ„Â±lÃ„Â±r.
- **(v5.4.1)** AynÃ„Â± sembol iÃƒÂ§in 7 gÃƒÂ¼n iÃƒÂ§inde tekrar sinyal gelirse detaylÃ„Â± analiz yerine ÃƒÂ¶nceki analize atÃ„Â±f yapan 1-2 tweetlik pekiÃ…Å¸tirme thread'i paylaÃ…Å¸Ã„Â±lÃ„Â±r.
- **(v5.4.2)** EtkileÃ…Å¸im adaylarÃ„Â± otomatikte yalnÃ„Â±z finans niyeti taÃ…Å¸Ã„Â±yan tweetlerden seÃƒÂ§ilir; promo/giveaway/RT ÃƒÂ§aÃ„Å¸rÃ„Â±sÃ„Â± hard-block edilir ve Telegram komutlarÃ„Â± `/ONAY_ID` formatÃ„Â±na alÃ„Â±nÃ„Â±r.
- **(v5.4.3)** ÃƒÅ“stat paneli yalnÃ„Â±z `GuruHandle` mention'Ã„Â±na izin verir; kaynak tarama tweet URL'si zorunludur ve hoca saygÃ„Â±sÃ„Â± ÃƒÂ¶lÃƒÂ§ÃƒÂ¼lÃƒÂ¼ teknik analiz diline ÃƒÂ§ekildi.
- **(v5.4.4)** Sinyal tablosu `Tarih/Saat` gÃƒÂ¶sterir ve `Durum` alanÃ„Â± gerÃƒÂ§ek sinyal durumunu takipÃƒÂ§i dostu metinle yansÃ„Â±tÃ„Â±r; ÃƒÂ¼stat paneli ÃƒÂ¶nizleme alanÃ„Â± bÃƒÂ¼yÃƒÂ¼tÃƒÂ¼ldÃƒÂ¼ ve taslak/yayÃ„Â±n/red geÃƒÂ§miÃ…Å¸i eklendi.
- **(v5.4.5)** ÃƒÅ“stat paneline ÃƒÂ§oklu hoca seÃƒÂ§imi ve `@matisay67` takas/AKD/BOFA analizi desteÃ„Å¸i eklendi; tablo parse aÃ…Å¸amasÃ„Â± teknik analiz yapÃ„Â±lacak adaylarÃ„Â± gerekÃƒÂ§eyle seÃƒÂ§er.

#### `NewsEngine.cs`
- **(v5.3.6)** Haber threadleri en fazla 3 parÃƒÂ§aya sÃ„Â±nÃ„Â±rlandÃ„Â±; son parÃƒÂ§ada haber ÃƒÂ¶zeti/YTD gÃƒÂ¼venliÃ„Å¸i zorunlu hale getirildi.
- **(v5.3.8)** Normal skor 9 haberler Telegram onayÃ„Â±na dÃƒÂ¼Ã…Å¸mez; `SKIPPED_REVIEW` history'ye yazÃ„Â±lÃ„Â±r. YalnÃ„Â±z skor 10 veya breaking 9+ auto-post olur.

#### `PerformanceTracker.cs`
- `RecordSignal(signal)`: Bot, Manuel veya Guru kaynaktan gelen sinyali veritabanÃ„Â±na iÃ…Å¸ler.

#### `GeminiService.cs`
- `AnalyzeChartImage(symbol, path)`: **(v3.9.0)** Grafik gÃƒÂ¶rsellerini teknik analize (RSI, Trend, Formasyon) dÃƒÂ¶nÃƒÂ¼Ã…Å¸tÃƒÂ¼rÃƒÂ¼r.
- `GenerateGuruHonoringThread(...)`: GÃƒÂ¶rsel analiz ve fiyat verisini kullanarak guru threadi ÃƒÂ¼retir.
- `DetectNewsCategory(title, source)`: **(v4.2.2)** Haber kategorisini tespit eder (7 kategori).
- `AnalyzeNewsImpactTwoStep(title, source)`: **(v4.2.2)** Ãƒâ€“nce kategori, sonra 1-10 skor ÃƒÂ¼retir.
- `GenerateNewsCategoryAnalysis(category, title, source, link)`: **(v4.2.2)** Kategoriye ÃƒÂ¶zel analiz thread'i ÃƒÂ¼retir.
- **(v5.2.9)** `LoadSectorMapContext()`: `Config/BistSectorMap.md` dosyasÃ„Â±nÃ„Â± okur. `GenerateNewsCategoryAnalysis` ve `AnalyzeNewsForThread` ÃƒÂ§aÃ„Å¸rÃ„Â±larÃ„Â±nda `sectorMap` parametresi olarak prompt'a enjekte edilir.
- `SendRequest(prompt)`: AI modeline metin tabanlÃ„Â± istek gÃƒÂ¶nderir.
- `GenerateMarketCloseTableTweet(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: **(v5.1.1)** GÃƒÂ¼n sonu kapanÃ„Â±Ã…Å¸ tweet thread'i ÃƒÂ¼retir.

#### `LMStudioProvider.cs`
- `SendRequest(prompt)`: LM Studio'ya metin isteÃ„Å¸i gÃƒÂ¶nderir (OpenAI compat). **(v5.0.0)** Prompt baÃ…Å¸Ã„Â±na `/no_think\n` prefix eklenir, timeout 300s.
- `SendRequestWithImage(prompt, imagePath)`: GÃƒÂ¶rsel + metin isteÃ„Å¸i gÃƒÂ¶nderir. **(v5.0.0)** Timeout 600s, `max_tokens` minimum 8192.
- `PrepareImageForVision(imagePath, maxDimension)`: **(v4.10.2)** GÃƒÂ¶rseli max 1024px'e kÃƒÂ¼ÃƒÂ§ÃƒÂ¼ltÃƒÂ¼r ve JPEG 85% kalitesinde kodlar.
- `BuildRequestBody(prompt, maxTokens)` / `BuildVisionRequestBody(prompt, imageUrl, maxTokens)`: **(v5.2.3)** LM Studio OpenAI-compatible isteklerine `enable_thinking=false`, `reasoning_effort=none`, `chat_template_kwargs.enable_thinking=false` ekler.
- `ExtractContentFromChoice(choice)`: **(v5.2.3)** `reasoning_content` artÃ„Â±k publishable kabul edilmez. `content` boÃ…Å¸sa veya `finish_reason=length` ise provider `null` dÃƒÂ¶ndÃƒÂ¼rÃƒÂ¼r ve fallback tetikler.

#### `ManualAnalysisService.cs`
- **(v4.10.8)** Yerel model aktifken `IndicatorExtractor` ÃƒÂ§aÃ„Å¸rÃ„Â±sÃ„Â± atlanÃ„Â±r (token tasarrufu).
- **(v4.10.8)** KÃ„Â±sa thread ÃƒÂ¼retiminde yerel model iÃƒÂ§in ekran gÃƒÂ¶rÃƒÂ¼ntÃƒÂ¼sÃƒÂ¼ tekrar gÃƒÂ¶nderilmez.
- **(v4.10.8)** Yerel modele indicator context yerine ana analiz metni iletilir.

---

## ÄŸÅ¸ÂÂ Python Scripts Map (Scripts/)

Python scriptleri "Worker" (Ã„Â°Ã…Å¸ÃƒÂ§i) olarak ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r. C# tarafÃ„Â±ndan komut satÃ„Â±rÃ„Â± argÃƒÂ¼manlarÃ„Â± ile ÃƒÂ§aÃ„Å¸rÃ„Â±lÃ„Â±r ve JSON ÃƒÂ§Ã„Â±ktÃ„Â±sÃ„Â± ÃƒÂ¼retirler.

| Script DosyasÃ„Â± | GÃƒÂ¶rev TanÃ„Â±mÃ„Â± | KÃƒÂ¼tÃƒÂ¼phaneler |
| :--- | :--- | :--- |
| **`playwright_daemon.py`** | **(v4.9.6 Yeni) Thread & YayÃ„Â±n Motoru.** X-Hive bazlÃ„Â± yeni sÃƒÂ¼per hÃ„Â±zlÃ„Â± bot. **v5.2.3:** `_robust_click_publish()` ile X overlay/click intercept durumlarÃ„Â±nda Escape Ã¢â€ â€™ normal click Ã¢â€ â€™ force click Ã¢â€ â€™ JS click fallback zinciri ve hata screenshot'Ã„Â±. | `playwright.async_api` |
| **`x_daemon.py`** | **HTTP Daemon (localhost:5580).** Tek Chrome instance ile sÃƒÂ¼rekli ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r. **(v4.9.4)** `_post_single_tweet` URL yakalama - home fallback kaldÃ„Â±rÃ„Â±ldÃ„Â±, toast/profile retry eklendi. | `selenium`, `undetected_chromedriver` |
| **`social_intel.py`** | **Dev X Otomasyonu.** Selenium ile giriÃ…Å¸ yapar, arama yapar, veri ÃƒÂ§eker, etkileÃ…Å¸im kurar. **v5.2.3:** TÃƒÂ¼rkÃƒÂ§e engagement parse (`B=bin`, `Mn=milyon`), own-account/bot-output filtreleri, 404 sentinel kaldÃ„Â±rma ve status URL dedupe eklendi. | `selenium`, `pickle` |
| `omni_scout.py` | Reddit ve diÃ„Å¸er kaynaklardan viral veri ÃƒÂ§eker. | `praw` (Reddit API) |
| `oracle.py` | Tahmin piyasalarÃ„Â± verisi (Polymarket vs.) | `requests` |
| `screenshot.py` | BIST/Crypto grafiklerinin ekran gÃƒÂ¶rÃƒÂ¼ntÃƒÂ¼sÃƒÂ¼nÃƒÂ¼ alÃ„Â±r. **v5.2.3:** Python tarafÃ„Â±nda da `VIP'VIP-*` ve prefix sembol normalizasyonu yapar. | `selenium` |
| **`lock_manager.py`** | **Atomic File Lock.** X (Twitter) oturumlarÃ„Â±nÃ„Â±n ÃƒÂ§akÃ„Â±Ã…Å¸masÃ„Â±nÃ„Â± ÃƒÂ¶nler. **(v4.9.3)** `acquire_lock` timeout 180s Ã¢â€ â€™ 360s. | `msvcrt`(Win) / `fcntl`(Linux) |

### ÄŸÅ¸ÂÂ `social_intel.py` Capabilities
Bu script "Standalone" (Tek baÃ…Å¸Ã„Â±na) ÃƒÂ§alÃ„Â±Ã…Å¸abilen gÃƒÂ¼ÃƒÂ§lÃƒÂ¼ bir bottur.
- **Driver Pool:** `ChromeDriverPool` sÃ„Â±nÃ„Â±fÃ„Â± ile tarayÃ„Â±cÃ„Â±larÃ„Â± ÃƒÂ¶nbelleÃ„Å¸e alÃ„Â±r (Performans artÃ„Â±Ã…Å¸Ã„Â±).
- **Smart Search:** `find_influencer_posts` fonksiyonu ile hem timeline hem de genel arama yapar.
- **Human-Like Behavior:** **(v4.6.0)** `human_delay` fonksiyonu ile insansÃ„Â± beklemeler yapar ve yakalanmayÃ„Â± ÃƒÂ¶nler.
- **Robust Typing:** **(v4.6.6)** Metni `document.execCommand('insertText', ...)` kullanarak JS enjeksiyonu ile yazar. React senkronizasyonu iÃƒÂ§in "WAKE UP" mekanizmasÃ„Â± iÃƒÂ§erir ve TÃƒÂ¼rk karakterleri iÃƒÂ§in ultra-stabilitedir.
- **Commands:** `search_influencer`, `post_tweet`, `fetch_replies`, `discover_influencers` vb.

---

## ÄŸÅ¸â€“Â¥Ã¯Â¸Â UI Map (ArayÃƒÂ¼z HaritasÃ„Â±)

### ÄŸÅ¸ÂÂ  MainForm (Ana Ekran)
*   **Sidebar (Navigasyon):**
    *   `Ana Ekran`, `Sinyal Merkezi`, `Manuel Analiz`, `Bot EtkileÃ…Å¸im`, `Ayarlar`
    *   `GeÃƒÂ§miÃ…Å¸`, `Fenomenler`, `Haberler` (Restore Edildi), `ÃƒÅ“stat Paneli`, `FenerbahÃƒÂ§e`
    *   `HIVE Intel`, `EtkileÃ…Å¸im Merkezi`
*   **Dashboard (`pnlDashboard`):**
    *   **Header:** API/Web SayaÃƒÂ§larÃ„Â±, Ticker, Start/Stop ButonlarÃ„Â±.
    *   **Tabs:** `Piyasa Analiz (Grafik)`, `Sosyal Medya AkÃ„Â±Ã…Å¸Ã„Â± (X)`.
*   **Sinyal Merkezi (`pnlSignals`):**
    *   **Filtreler:** Strateji (King, Bomba...), Periyot, EÃ…Å¸ik DeÃ„Å¸erler.
    *   **Grid:** `dgvSignals` (CanlÃ„Â± sinyaller).
*   **Manuel Analiz (`pnlAnalysis`):**
    *   **Kontroller:** Pazar, Periyot, Sembol seÃƒÂ§imi.
    *   **Aksiyon:** Analiz Et -> SonuÃƒÂ§ (Text) + Grafik (Resim) -> Tweetle.
*   **HIVE Intel (`pnlHive`):**
    *   **Apex Ar-Ge:** Makaleler (Papers) ve GitHub RepolarÃ„Â±.
    *   **Meta-Teacher:** Konsey (Guru) iÃƒÂ§gÃƒÂ¶rÃƒÂ¼leri tablosu.
    *   **Wisdom:** Bilgelik kÃƒÂ¼tÃƒÂ¼phanesi (`WisdomLibControl`).

### Ã¢Å¡â„¢Ã¯Â¸Â Ayarlar Paneli DetaylarÃ„Â± (`pnlSettings`)
> **Konum:** `MainForm.cs` satÃ„Â±r ~908-1165

**YapÃ„Â±:** `SplitContainer` (Sol: Kategori ListBox, SaÃ„Å¸: Ã„Â°ÃƒÂ§erik Panel)

| Kategori | Panel | Kontroller |
|----------|-------|------------|
| ÄŸÅ¸â€â€˜ API & BaÃ„Å¸lantÃ„Â±lar | `pnlSetApi` | `txtApiKey`, `txtApiSecret`, `txtAccessToken`, `txtTokenSecret` (Twitter) |
|  |  | `txtGeminiKey`, `txtPerplexityKey`, `cmbGeminiModel` (AI) |
|  |  | `btnTestApi` (ÄŸÅ¸Â§Âª Test), `btnListModels` (ÄŸÅ¸â€œâ€¹ Modeller) |
|  |  | `dgvBenchmark` (Benchmark Grid), `btnRunBenchmark`, `btnAutoSelect` |
|  |  | `txtTelToken`, `txtTelChatId` (Telegram) |
|  |  | `txtTvSymbol`, `txtTvChartId` (TradingView) |
| ÄŸÅ¸â€ºÂ¡Ã¯Â¸Â Spam & GÃƒÂ¼venlik | `pnlSetSpam` | `chkSpamSignals`, `chkSpamBatches`, `chkSpamManual`, `chkSpamNews` |
| ÄŸÅ¸ÂÂ¯ Hedef & Otomasyon | `pnlSetTarget` | `txtTargetAccounts`, `chkAuto` |

**Key UI Elements:**
- **Benchmark Panel:** `pnlBenchmark` (satÃ„Â±r ~1040-1080)
- **Kaydet Butonu:** `btnSave` (satÃ„Â±r ~1155) Ã¢â€ â€™ `BtnSave_Click`

### ÄŸÅ¸Â¤â€“ OperatorForm (Ã„Â°cra Paneli)
*   **Intelligence:** Cortex Zeka Raporu (Sol Panel).
*   **Execution:** Tweet Zinciri (SaÃ„Å¸ Panel), BaÃ…Å¸lat Butonu.
*   **Sentinel:** CanlÃ„Â± etkileÃ…Å¸im akÃ„Â±Ã…Å¸Ã„Â±.

---

## ÄŸÅ¸â€œÂ Key Line References (SatÃ„Â±r HaritasÃ„Â±)

> **Not:** Bu satÃ„Â±rlar deÃ„Å¸iÃ…Å¸ebilir. Ancak arama yapmadan ÃƒÂ¶nce burayÃ„Â± kontrol edin.

### MainForm.cs - Panel Initialize FonksiyonlarÃ„Â±
| Fonksiyon | SatÃ„Â±r | AÃƒÂ§Ã„Â±klama |
|-----------|-------|----------|
| `InitializeComponent` | 197-1208 | **ANA UI KURULUMU** - TÃƒÂ¼m paneller, kontroller |
| `ShowPanel` | 1210-1238 | Panel gÃƒÂ¶rÃƒÂ¼nÃƒÂ¼rlÃƒÂ¼k yÃƒÂ¶netimi |
| `InitializeInfluencerPanel` | 1247-1397 | Fenomenler sekmesi |
| `InitializeHistoryPanel` | 1468-1516 | GeÃƒÂ§miÃ…Å¸ sekmesi |
| `InitializeNewsPanel` | 1518-1598 | Haberler sekmesi |
| `InitializeChart` | 1806-1854 | TradingView grafik |
| `InitializeTwitterWebView` | 1977-1991 | X (Twitter) WebView |
| `InitializeServices` | 1993-2135 | TÃƒÂ¼m servislerin baÃ…Å¸latÃ„Â±lmasÃ„Â± |
| `InitializeEngagementHub` | 4838-4886 | EtkileÃ…Å¸im Merkezi |
| `InitializeManualAnalysisTab` | 5015-5227 | Manuel Analiz sekmesi |
| `InitializeBotInteractionTab` | 5275-5364 | Bot EtkileÃ…Å¸im sekmesi |
| `InitializeGuruPanel` | 5487-5591 | ÃƒÅ“stat Paneli |
| `InitializeFenerbahcePanel` | 5699-5828 | FenerbahÃƒÂ§e sekmesi |
| `InitializeHiveHub` | 5830-5883 | HIVE Intel hub |
| `InitializeMetaTeacherInto` | 5885-5953 | Meta-Teacher iÃƒÂ§gÃƒÂ¶rÃƒÂ¼leri |
| `InitializeWisdomInto` | 5999-6015 | Wisdom kÃƒÂ¼tÃƒÂ¼phanesi |
| `InitializeOmniScoutInto` | ~6030 | Omni-Scout UI (Yeni) |
| `InitializeOracleInto` | ~6080 | Oracle UI (Yeni) |

### MainForm.cs - Core Fonksiyonlar
| Fonksiyon | SatÃ„Â±r | AÃƒÂ§Ã„Â±klama |
|-----------|-------|----------|
| `LoadSettings` | 2138-2245 | Config'den UI'ya yÃƒÂ¼kleme |
| `BtnSave_Click` | 2247-2334 | UI'dan Config'e kaydetme |
| `BtnStart_Click` | 2336-2361 | WatcherlarÃ„Â± baÃ…Å¸latma |
| `PerformManualAnalysis` | 4137-4215 | Manuel analiz iÃ…Å¸lemi |
| `PostMorningMotivation` | 2558+ | **(v3.8.2)** Motivasyon tweeti ve zamanlamasÃ„Â± |
| `ProcessTelegramCommands` | 4414-4756 | Telegram komutlarÃ„Â± (/ONAY, /ANALIZ vb.) |
| `ProcessSignal` | 3985-4126 | Sinyal iÃ…Å¸leme mantÃ„Â±Ã„Å¸Ã„Â± |
| `ProcessNewsQueue` | 3705-3915 | Haber kuyruÃ„Å¸u iÃ…Å¸leme |
| `Log` / `LogAI` / `LogNews` | 4249-4312 | Loglama fonksiyonlarÃ„Â± |

### MainForm.cs - WebView & X (Twitter) FonksiyonlarÃ„Â±
| Fonksiyon | SatÃ„Â±r | AÃƒÂ§Ã„Â±klama |
|-----------|-------|----------|
| `PerformInternalPostAsync` | 2708-2809 | Tweet atma (WebView2) |
| `PerformInternalThreadAsync` | 2811-3199 | Thread atma (WebView2) |
| `PerformInternalSearchAsync` | 3201-3532 | X arama (WebView2) |
| `SaveTwitterCookiesAsync` | 1875-1925 | Cookie kaydetme |
| `InjectTwitterCookiesAsync` | 1927-1975 | Cookie yÃƒÂ¼kleme |

### MainForm.cs - UI BÃƒÂ¶lgeleri (InitializeComponent iÃƒÂ§inde)
| BÃƒÂ¶lge | SatÃ„Â±r AralÃ„Â±Ã„Å¸Ã„Â± | Ã„Â°ÃƒÂ§erik |
|-------|---------------|--------|
| Field TanÃ„Â±mlarÃ„Â± | 60-175 | TÃƒÂ¼m UI kontrol tanÃ„Â±mlarÃ„Â± |
| Panel TanÃ„Â±mlarÃ„Â± | 260-285 | `pnlDashboard`, `pnlSettings`, `pnlHive` vb. |
| Sidebar Navigation | 286-420 | `btnNav...` butonlarÃ„Â± |
| Dashboard Header | 425-530 | SayaÃƒÂ§lar, Ticker, Start/Stop |
| Settings Panel | 908-1165 | TÃƒÂ¼m ayarlar UI |
| AI & Model YÃƒÂ¶netimi | 939-1080 | Gemini/Perplexity, Benchmark |

### Services/ - Ãƒâ€“nemli Dosyalar
| Dosya | SatÃ„Â±r | Ã„Â°ÃƒÂ§erik |
|-------|-------|--------|
| `ModelBenchmarkService.cs` | 55-125 | `FetchAvailableModelsAsync()` |
| `ModelBenchmarkService.cs` | 130-145 | `RunBenchmarkAsync()` |
| `ModelBenchmarkService.cs` | 290-385 | `UpdateTaskPreferencesFromResults()` Ã¢â‚¬â€ benchmarkÃ¢â€ â€™ModelManager dinamik gÃƒÂ¼ncelleme |
| `ModelManager.cs` | 42-150 | `InitializeTaskPreferences()` |
| `ModelManager.cs` | 155-220 | `SendRequest()` + fallback |
| `GeminiService.cs` | ~580-720 | `SendRequest()` ana mantÃ„Â±k |
| `SocialIntelService.cs` | ~200-400 | Python script ÃƒÂ§aÃ„Å¸rÃ„Â±sÃ„Â± |
| `SentinelService.cs` | ~80-150 | `ProcessTweetReplies()` |
| `NewsEngine.cs` | ~100-200 | Haber iÃ…Å¸leme mantÃ„Â±Ã„Å¸Ã„Â± |
| `OperationManager.cs` | 295-305 | `SyncGeminiProviders()` model isimleri |

## ÄŸÅ¸â€â€ Workflow Examples (AkÃ„Â±Ã…Å¸ Ã…ÂemalarÃ„Â±)

### 1. KullanÃ„Â±cÃ„Â±dan Gelen "Analiz Talebi" AkÃ„Â±Ã…Å¸Ã„Â±
1.  **AlgÃ„Â±lama:** `SentinelService` -> `ProcessTweetReplies` ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r.
2.  **Veri Ãƒâ€¡ekme:** `SocialIntelService.cs` -> `social_intel.py` (`fetch_replies`) ÃƒÂ§aÃ„Å¸rÃ„Â±lÃ„Â±r.
3.  **Analiz:** Gelen yanÃ„Â±t `GeminiService` ile analiz edilir. "TALEP: THYAO" olduÃ„Å¸u anlaÃ…Å¸Ã„Â±lÃ„Â±r.
4.  **Aksiyon:** `OperatorForm` ÃƒÂ¼zerinde kullanÃ„Â±cÃ„Â±ya "Analiz Ã„Â°steÃ„Å¸i Geldi" uyarÃ„Â±sÃ„Â± dÃƒÂ¼Ã…Å¸er.

### 2. Meta-Teacher (Konsey) DÃƒÂ¶ngÃƒÂ¼sÃƒÂ¼
1.  **Tetikleme:** ZamanlayÃ„Â±cÃ„Â± (Timer) `SocialIntelService.PerformMetaTeacherLoopAsync` metodunu ÃƒÂ§aÃ„Å¸Ã„Â±rÃ„Â±r.
2.  **Liste:** `InfluencerControlService` ÃƒÂ¼zerinden "Konsey ÃƒÅ“yeleri" listesi alÃ„Â±nÃ„Â±r.
3.  **Tarama:** Her ÃƒÂ¼ye iÃƒÂ§in `social_intel.py` (`search_influencer`) ÃƒÂ§alÃ„Â±Ã…Å¸tÃ„Â±rÃ„Â±lÃ„Â±r. Tarih filtresiyle (Since Date) yeni tweetler aranÃ„Â±r.
4.  **Ãƒâ€“Ã„Å¸renme:** Bulunan analizler `MemoryEngine` iÃƒÂ§ine kaydedilir (`Learn`).
5.  **Ã„Â°ÃƒÂ§gÃƒÂ¶rÃƒÂ¼:** Ãƒâ€“nemli bir strateji bulunursa `OnMetaTeacherInsight` eventi tetiklenir ve kullanÃ„Â±cÃ„Â±ya sunulur.

### 3. Cortex Strateji DÃƒÂ¶ngÃƒÂ¼sÃƒÂ¼ (HIVE Phase 3)
1.  **Veri HazÃ„Â±rlÃ„Â±Ã„Å¸Ã„Â±:** `OmniScout` (Viral) ve `Oracle` (Piyasa) servisleri arka planda veri ÃƒÂ§eker ve `LastReport` deÃ„Å¸iÃ…Å¸kenini gÃƒÂ¼nceller.
2.  **Tetikleme:** KullanÃ„Â±cÃ„Â± `OperatorForm` -> Sentez sekmesinden **"CORTEX ANALÃ„Â°ZÃ„Â° BAÃ…ÂLAT"** butonuna basar.
3.  **Sentez:** `CortexService` tÃƒÂ¼m raporlarÃ„Â± `Gemini`'ye gÃƒÂ¶nderir.
4.  **SonuÃƒÂ§:** AI, verileri ÃƒÂ§aprazlayarak (Cross-Reference) bir strateji ÃƒÂ¼retir ve UI'da gÃƒÂ¶sterir.

---

## Ã¢Å¡Â Ã¯Â¸Â Kritik Notlar & Kurallar

1.  **JSON Ã„Â°letiÃ…Å¸imi:** C# ve Python arasÃ„Â±ndaki veri alÃ„Â±Ã…Å¸veriÃ…Å¸i **her zaman JSON** formatÃ„Â±ndadÃ„Â±r. Python tarafÃ„Â±nda `---JSON_START---` ve `---JSON_END---` markerlarÃ„Â± kullanÃ„Â±lÃ„Â±r.
2.  **Thread Safety:** `SentinelService` ve `OperationManager` asenkron ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r. UI gÃƒÂ¼ncellemeleri iÃƒÂ§in `Invoke` zorunludur.
3.  **Dil KuralÃ„Â±:** Kod iÃƒÂ§i (deÃ„Å¸iÃ…Å¸kenler, yorumlar) Ã„Â°ngilizce, **UI ve Loglar TÃƒÂ¼rkÃƒÂ§e** olmalÃ„Â±dÃ„Â±r.
4.  **Hata YÃƒÂ¶netimi:** Python scripti hata verirse JSON iÃƒÂ§inde `status: "error"` dÃƒÂ¶ner. C# tarafÃ„Â± bunu `Logger.Sys` ile loglamalÃ„Â±dÃ„Â±r.

---

## ÄŸÅ¸â€œâ€š Server Deployment Paths (CanlÃ„Â± Ortam)

CanlÃ„Â± sunucudaki (v3.7.6 ve sonrasÃ„Â±) dosya yollarÃ„Â±:

| Ã„Â°ÃƒÂ§erik | Sunucu Yolu |
| :--- | :--- |
| **Uygulama DosyalarÃ„Â±** | `G:\DiÃ„Å¸er bilgisayarlar\Sunucu\XiDeAI Pro` |
| **Log DosyalarÃ„Â±** | `G:\DiÃ„Å¸er bilgisayarlar\Sunucu\XiDeAI` |












































































