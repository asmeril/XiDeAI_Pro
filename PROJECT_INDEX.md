> **Version:** 5.6.1 (Playwright Tekil Yanıt Hotfix)
> **Architecture:** Hybrid (C# WinForms + Canonical PostingService + Python Playwright Posting Engine + Selenium Research Fallback + WebView2 Session Bridge)
> **Last Updated:** 2026-06-23

Bu indeks, proje Ã¼zerinde Ã§alÄ±ÅŸacak yapay zeka ve geliÅŸtiriciler iÃ§in **kod tabanÄ±nÄ±n haritasÄ±nÄ±** sunar. Yeni Ã¶zellik eklerken veya hata dÃ¼zeltirken burayÄ± referans alÄ±nÄ±z.

---

## ğŸ—ï¸ Core Architecture (Hibrit YapÄ±)

Proje 4 ana katmandan oluÅŸur:
1.  **Orchestrator (C#):** TÃ¼m mantÄ±ÄŸÄ± yÃ¶neten, servisleri baÅŸlatan ve kararlarÄ± veren katman. (`OperationManager`)
2.  **Publishing Engine (Python Playwright):** Thread gÃ¶nderiminde ana motor. (`playwright_daemon.py`)
3.  **Interaction Layer (Hybrid):**
    *   **Playwright-First (Thread/Tweet):** GÃ¶nderim iÅŸlemleri Playwright motoru ile yÃ¼rÃ¼r.
    *   **WebView2 Fallback:** KullanÄ±cÄ± gÃ¶rÃ¼nÃ¼rlÃ¼ÄŸÃ¼ gerektiren iÅŸlemler iÃ§in yedek.
    *   **Python/Selenium Fallback:** Fenomen tarama/araÅŸtÄ±rma iÃ§in halen aktif.
4.  **Intelligence Layer (AI):** LM Studio (Yerel Model, Birincil) + Gemini/Perplexity (Yedek/Bulut) entegrasyonu.

### âœ… Canonical Publishing Flow (Tek GerÃ§ek Hat)
1. ModÃ¼ller sadece iÃ§erik Ã¼retir; gÃ¶nderimi `PostingService` yapar.
2. `PostingService.PostTweetAsync` / `PostThreadAsync` tÃ¼m tweet/thread payloadlarÄ±nÄ± normalize eder ve `SocialIntelService` alt motoruna iletir.
3. `SocialIntelService` canonical olarak `Scripts/playwright_daemon.py` kullanÄ±r; WebView2 internal bridge debug-only bÄ±rakÄ±lmÄ±ÅŸtÄ±r.
4. `playwright_daemon.py` gerÃ§ek `/status/<id>` URL yakalamadan success dÃ¶nmez; thread iÃ§in `posted_count == total_chunks` beklenir.
5. C# tarafÄ±nda success sadece `PostingService.IsVerifiedTweet/IsVerifiedThread` doÄŸrulamasÄ±ndan sonra kabul edilir.

### âŒ KaÃ§Ä±nÄ±lacak Eski DavranÄ±ÅŸlar
- AynÄ± iÅŸlemde hem fail hem success logu yazmak.
- C# tarafÄ±ndan hazÄ±rlanmÄ±ÅŸ thread parÃ§alarÄ±nÄ± Python'da gereksiz yeniden parÃ§alamak.
- Thread sayaÃ§larÄ±nÄ± parÃ§a sayÄ±sÄ± yerine sabit +1 artÄ±rmak.
- WebView2 modal kapandÄ± veya butona tÄ±klandÄ± diye post'u baÅŸarÄ± saymak.

---

## ğŸ“‚ Services Map (C#)

TÃ¼m servisler `Services/` klasÃ¶rÃ¼ altÄ±ndadÄ±r ve `OperationManager.cs` tarafÄ±ndan yÃ¶netilir.

| Servis DosyasÄ± | Temel GÃ¶revleri | BaÄŸlÄ± OlduÄŸu Python Script |
| :--- | :--- | :--- |
| **`SocialIntelService.cs`** | **DÃ¼ÅŸÃ¼k seviye X (Twitter) kÃ¶prÃ¼sÃ¼.** PostingService tarafÄ±ndan Ã§aÄŸrÄ±lÄ±r; Playwright subprocess ile doÄŸrulanmÄ±ÅŸ gÃ¶nderim yapar. AraÅŸtÄ±rma/interaction iÃ§in daemon ve Selenium fallback iÃ§erir. | `playwright_daemon.py`, `x_daemon.py`, `social_intel.py` |
| **`PostingService.cs`** | **v5.3.0 Canonical gÃ¶nderim servisi.** TÃ¼m modÃ¼ller iÃ§in tek tweet/thread doÄŸrulama kapÄ±sÄ±. `/status/` URL ve thread parÃ§a sayÄ±sÄ± doÄŸrulanmadan success dÃ¶nmez. | `playwright_daemon.py` |
| **`OperationManager.cs`** | **Orkestra Åefi.** Servisleri baÅŸlatÄ±r, daemon'Ä± baÅŸlatÄ±r, durdurur ve birbirine baÄŸlar. | - |
| `GeminiService.cs` | **AI Motoru.** PromptlarÄ± iÅŸler, gÃ¶rsel analiz yapar (Vision) ve thread Ã¼retir. **v4.2.2:** Two-Step News metodlarÄ± eklendi. | - |
| `ModelBenchmarkService.cs` | **v4.9.9** Gemini modellerini test eder, API'den canlÄ± model listesi Ã§eker, benchmark yapar. **v4.9.9:** `UpdateTaskPreferencesFromResults()` eklendi â€” benchmark sonucu ModelManager TaskType tercihlerini dinamik olarak gÃ¼nceller. | - |
| `NewsEngine.cs` | **v4.2.2:** Two-Step Logic (1-10 skor, 7 kategori, SON DAKÄ°KA boost). Haber akÄ±ÅŸÄ±nÄ± kategorize eder ve iÅŸler. **v5.1.3:** Flash haber garantili 2-tweet format (`GetFlashNewsAnalysisPrompt`); `BuildMinimalNewsTweet` â€” AI null dÃ¶nse bile baÅŸlÄ±k+link tweet'i gÃ¶nderilir; maxTokens 800/900'a yÃ¼kseldi; `BuildNewsLeadTweet` link+flash tag eklendi. **v5.1.4:** CS8602 null guard â€” `threadContent != null &&` eklendi. |
| `NewsTrackerService.cs` | RSS ve Twitter'dan haber tarar. **v3.8.3:** `OnNewsDetected` eventini tetikler. | `x_daemon.py` |
| `ThreadService.cs` | Zincir (Thread) oluÅŸturma mantÄ±ÄŸÄ±nÄ± kurar. | - |
| `ThreadPipeline.cs` | **Merkezi Thread HazÄ±rlayÄ±cÄ±.** Lead tweet, parÃ§a normalizasyonu ve ortak split kurallarÄ±nÄ± tek yerde toplar. | - |
| `SymbolNormalizer.cs` | **v5.2.3** Merkezi sembol normalizasyonu ve BIST sembol doÄŸrulamasÄ±. `VIP'VIP-VAKBN`, `VIP-VAKBN`, `BIST:VAKBN` gibi varyantlarÄ± canonical sembole indirger; kÄ±rpÄ±lmÄ±ÅŸ/bozuk sembolleri engeller. | - |
| `InfluencerControlService.cs` | Takip edilecek fenomenlerin veritabanÄ±nÄ± yÃ¶netir. **(v5.2.2)** `UpdateScore(handle, delta)` eklendi â€” engagement bazlÄ± otomatik skor gÃ¼ncelleme (0-100 aralÄ±ÄŸÄ±). | - |
| `PriceFetchService.cs` | **Fiyat Motoru.** BIST ve Kripto paralarÄ±n anlÄ±k fiyatÄ±nÄ± Ã§eker. (Parallel Async). | - |
| `SignalEngine.cs` | Sinyal iÅŸleme motoru. Sinyalleri filtreler, formatlar ve yayÄ±nlar. | - |
| `LogFileWatcher.cs` | **v5.2.3** `Sinyal_Log_Database.txt` iÃ§in snapshot tabanlÄ± izleme. Byte-tail kaynaklÄ± satÄ±r ortasÄ± okuma (`ACSELâ†’SEL`, `DEVAâ†’VA`) engellendi. | - |
| `SignalParser.cs` | **v5.2.3** Strict Alpha/PreMove parse. Header, `KAPALI`, bilinmeyen BIST sembolÃ¼, fiyatÄ± sÄ±fÄ±r ve geÃ§ersiz durumlar atlanÄ±r. | - |
| `ModelManager.cs` | **v4.10.0** AI provider yÃ¶neticisi. Aktif provider'Ä± seÃ§er, fallback/routing yapar. `SyncGeminiProviders()` ile LMStudio dahil tÃ¼m provider'larÄ± senkronize eder. | - |
| `LMStudioProvider.cs` | **v4.10.0** LM Studio / LM Link local model provider'Ä± (OpenAI uyumlu). `SendRequest()` + `SendRequestWithImage()` destekler. **v4.10.2:** `PrepareImageForVision()` â€” 4K DPI ekran gÃ¶rÃ¼ntÃ¼lerini 1024px JPEG'e dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r. **v5.0.0:** `/no_think` prefix (Qwen3 reasoning bastirma), vision timeout 600s. **v5.1.3:** `reasoning_content` fallback KALDIRILDI â€” `content=boÅŸ`+`finish_reason=length` durumunda `null` dÃ¶ndÃ¼rÃ¼lÃ¼yor; `finish_reason=length` logu eklendi. **v5.1.4:** `AnalyzeNewsUnified` maxTokens 450â†’1500 (Qwen3.6-27b finish_reason=length sorunu Ã§Ã¶zÃ¼ldÃ¼); prompt'a "dÃ¼ÅŸÃ¼nme adÄ±mÄ± YOK" hint eklendi. |
| `ManualAnalysisService.cs` | **v4.10.8** Manuel analiz servisi. **Yerel model aktifken:** `IndicatorExtractor` atlanÄ±r, kÄ±sa thread iÃ§in ekran gÃ¶rÃ¼ntÃ¼sÃ¼ tekrar gÃ¶nderilmez, ana analiz metni indicator context olarak kullanÄ±lÄ±r. | - |

> **Not (v4.0.0):** HIVE servisleri (Sentinel, Apex, Omni, Oracle, Wisdom, Cortex) kaldÄ±rÄ±lmÄ±ÅŸtÄ±r. Yedek: `d:\Projects\HiveProjesi`

### ğŸ”‘ Key Classes & Methods

#### `SocialIntelService.cs`
- `FindInfluencerAnalyses(symbol, market)`: Fenomenlerin analizlerini arar. (Ã–nce VIP timeline, sonra genel arama).
- **(v5.2.2)** Daemon'dan post alÄ±nÄ±nca `engagement/10` formÃ¼lÃ¼yle `InfluencerControlService.UpdateScore()` Ã§aÄŸrÄ±lÄ±r â€” etkin fenomenler Ã¼ste Ã§Ä±kar.
- **(v5.2.2)** Genel arama parse hatasÄ±nda handle boÅŸ kalÄ±rsa tweet atlanÄ±r (eski: `X-User` fallback kaldÄ±rÄ±ldÄ±).
- **(v5.2.3)** `IsBadSocialResult(author, content, url)`: kendi hesap, bot Ã§Ä±ktÄ±sÄ± (`Piyasa GÃ¶rÃ¼ÅŸleri`, `Teknik Analizim`, `XiDeAI`), `ERROR_404` ve ana sayfa URL sonuÃ§larÄ±nÄ± filtreler.
- `PostTweet(text)` / `PostThreadAsync(tweets)`: DÃ¼ÅŸÃ¼k seviye Playwright posting kÃ¶prÃ¼sÃ¼. **v5.3.0:** WebView2 internal bridge canonical yoldan Ã§Ä±karÄ±ldÄ±; `/status/` URL ve `posted_count/total_chunks` doÄŸrulamasÄ± zorunlu.
- `CheckSafety(actionType)`: **(v4.6.0)** GÃ¼venlik kontrolÃ¼ yapar (HÄ±z limiti ve gÃ¼nlÃ¼k kotalar).
- `PerformDeepScanAsync()`: Rastgele seÃ§ilen fenomenleri tarayarak bilgi tabanÄ±nÄ± gÃ¼nceller.
- **(v5.3.0)** `PostTweet`/`PostThreadAsync` yalnÄ±zca `PostingService` tarafÄ±ndan production gÃ¶nderim iÃ§in kullanÄ±lmalÄ±dÄ±r; internal WebView2 bridge canonical yoldan Ã§Ä±karÄ±ldÄ±.

#### `PostingService.cs`
- **(v5.3.0)** `PostTweetAsync(text, mediaPath, module)`: Tekil tweetleri canonical Playwright hattÄ±na gÃ¶nderir, gerÃ§ek `/status/` URL yoksa hata dÃ¶ndÃ¼rÃ¼r.
- **(v5.3.0)** `PostThreadAsync(parts, mediaPath, module)`: Thread parÃ§alarÄ±nÄ± `ThreadPipeline.EnsureWithinLimit` ile normalize eder; tÃ¼m parÃ§alar gÃ¶nderilmeden success dÃ¶nmez.
- **(v5.3.0)** `IsVerifiedTweet` / `IsVerifiedThread`: Uygulama genelinde tek baÅŸarÄ± standardÄ±.

#### `FanZoneService.cs`
- **(v5.3.0)** Kritik hesap taramasÄ±nda tweet URL'si iÅŸlem Ã¶ncesi deÄŸil `ProcessTweet` iÃ§inde dedupe edilir; like/RT baÅŸarÄ± ikonlarÄ± yalnÄ±zca gerÃ§ek `status=success` dÃ¶nerse iÅŸaretlenir.

#### `InteractionEngine.cs`
- **(v5.3.0)** `RunTargetedCheck(category)` artÄ±k `Influencer.Handle` deÄŸerlerini gÃ¶nderir; Ã¶nceki `string.Join(targets)` class-name Ã¼retme hatasÄ± giderildi.
- **(v5.3.2)** Viral reply adaylarÄ± Ã¶neri aÅŸamasÄ±nda interaction memory'ye yazÄ±lmaz; sadece onaylÄ± yanÄ±t gerÃ§ek baÅŸarÄ±yla gÃ¶nderildikten sonra iÅŸaretlenir.
- **(v5.3.3)** Otomatik bot dÃ¶ngÃ¼sÃ¼nden direkt Like/RT kaldÄ±rÄ±ldÄ±. Hedef fenomen etkileÅŸimi sadece manuel tetiklenir; varsayÄ±lan aksiyon yavaÅŸ modda Like-only, Python tarafÄ±nda son 6 saat + gerÃ§ek tweet sahibi filtresi zorunludur.

#### `ThreadService.cs`
- **(v4.10.8)** Tweet parÃ§alarÄ± `.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5)` filtresiyle kÄ±sa/boÅŸ parÃ§alar temizlenir.
- **(v5.3.0)** Sinyal, batch, gÃ¼nlÃ¼k/haftalÄ±k rapor threadleri `PostingService` Ã¼zerinden gÃ¶nderilir.
- **(v5.3.4)** Sinyal threadleri en fazla 4 parÃ§a ile sÄ±nÄ±rlandÄ±; beÄŸeni/RT Ã§aÄŸrÄ±sÄ± kaldÄ±rÄ±ldÄ±, sonuÃ§ tweeti seviye/teyit/risk diline Ã§ekildi.
- **(v5.4.1)** Sinyal lead tweetlerinde ham `AKTIF/PULLBACK_ADAY` yerine takipÃ§i dostu `Sinyal canlÄ±, teyit aranÄ±yor` / `Geri Ã§ekilme takibi, acele yok` etiketleri kullanÄ±lÄ±r.

#### `LogFileWatcher.cs`
- **(v5.2.3)** `LoadSeenKeys(path)`: servis baÅŸlarken mevcut aÃ§Ä±k sinyalleri hafÄ±zaya alÄ±r, geÃ§miÅŸ satÄ±rlarÄ± tekrar tetiklemez.
- **(v5.2.3)** `ReadStableLines(path)`: iDeal dosyasÄ±nÄ± snapshot olarak okur; rewrite edilen dosyada byte offset kullanÄ±lmaz.
- **(v5.2.3)** `TryBuildSignalKey(line, out key, out strategy)`: `Symbol|Strategy|Period|Tarih|Durum` anahtarÄ± Ã¼retir, `KAPALI` ve geÃ§ersiz satÄ±rlarÄ± atlar.

#### `SignalParser.cs`
- **(v5.2.3)** `ParseDbLine(line, strategyOverride)`: 12 kolonlu iDeal DB satÄ±rlarÄ±nÄ± strict parse eder; `D` periyodu `G` olarak normalize edilir; `SymbolNormalizer` ile canonical sembol kullanÄ±lÄ±r.

#### `SignalPersistenceService.cs`
- **(v5.3.0)** Processed key artÄ±k `Symbol|Strategy|Period|Durum|DetectedAt` tabanlÄ±dÄ±r; aynÄ± sembol/periyotta farklÄ± strateji veya yeni tarihli sinyal yanlÄ±ÅŸlÄ±kla bastÄ±rÄ±lmaz.

#### `SymbolNormalizer.cs`
- **(v5.2.3)** `NormalizeSignalSymbol(rawSymbol)`: `VIP'VIP-VAKBN` ve benzeri bozuk prefixleri temizleyerek canonical BIST sembolÃ¼ Ã¼retir.
- **(v5.2.3)** `IsKnownBistSymbol(symbol)`: `Config/symbols_bist.txt` Ã¼zerinden sembol doÄŸrular; config yoksa makul BIST formatÄ±na fallback yapar.

#### `PromptManager.cs`
- **(v4.10.8)** Derin analiz prompt'una `### GÃ–RSEL OKUMA (GRAFÄ°K)` bÃ¶lÃ¼mÃ¼ eklendi â€” yerel modelin grafik okuma kalitesini artÄ±rÄ±r.
- **(v5.1.1)** `GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: Yeniden yazÄ±ldÄ±. Eski tek-tweet ÅŸablon â†’ 6-7 tweet fenomen thread yapÄ±sÄ± (Hook â†’ XU100 yorum â†’ YÄ±ldÄ±zlar â†’ Kazazedeler â†’ Pulse anlarÄ± â†’ YarÄ±na bakÄ±ÅŸ â†’ CTA).
- **(v5.1.1)** TÃ¼m `### GÃ–REV` bloklarÄ±na X Algoritma Fenomen KurallarÄ± enjekte edildi: Hook (kanca ilk cÃ¼mle), kÄ±sa/boÅŸluklu format (dwell time), ELI5 hikayeleÅŸtirme, CTA (son tweette RT/takip).
- **(v5.1.1)** Contrarian Filter: `DailyTrends` = `[XU100_CANLI_VERI: MOD=X, TREND=Y%] YATIRIMCI_SOSYAL_ALGI: #...` â€” AI hard data ile sosyal algÄ± zÄ±tlÄ±ÄŸÄ±nÄ± Smart Money tuzagÄ± olarak yorumlar.
- **(v5.2.2)** `GetSignalAnalysisPrompt`, `GetDeepManualAnalysisPrompt`, `GetDeepTechnicalAnalysisPrompt`: YASAK SÃ–ZCÃœKLER listesi eklendi (fÄ±sÄ±ltÄ± alÄ±ÅŸ, akÄ±llÄ± para, piyasa kurdu vb.). Son tweet ZORUNLU: AL/Ä°ZLE/BEKLE karar + soru formatÄ±.
- **(v5.5.6)** GetSporReplyPrompt: Yeni "SPOR" kategorisi eklendi. Transfer, maç ve spor kulübü (Fenerbahçe vb.) paylaşımları için özel taraftar/spor yorumcusu promptu oluşturuldu.
- **(v5.5.6)** Tüm X (Twitter) yanıt promptlarına (GetReplyGenerationPrompt vb.) kural güncellemesi (EK KURALLAR kural 3): Yanıtların zorunlu soru sorması engellendi ve makul kısalıkta olması sağlandı.
- **(v5.2.2)** `GetAlphaSignalPrompt` / `GetPreMoveSignalPrompt`: Robotik ton kaldÄ±rÄ±ldÄ± (borsa kurdu, fÄ±sÄ±ldayan vb.). Fenomen mention: varsa doÄŸal, yoksa ekleme (zorunlu deÄŸil).
- **(v5.2.9)** `GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash, sectorMap)`: `sectorMap` parametresi eklendi. `GetEkonomiNewsAnalysisPrompt`, `GetTeknolojiNewsAnalysisPrompt`, `GetYasamNewsAnalysisPrompt` BIST SektÃ¶r HaritasÄ±'nÄ± prompt'a enjekte eder; halusinatÃ¶r sembol Ã¼retimi engellendi. Her kategori prompt'u "TAM OLARAK 3 TWEET" ve `|||` zorunluluÄŸu ile gÃ¼ncellendi.
- **(v5.4.9)** `GetGuruHonoringThreadPrompt`: Takas ve AKD analizi iÃ§in "DiÄŸer" kuralÄ±, T+2 gecikmesi ve kurumsal/bireysel oranlama mantÄ±ÄŸÄ± (`takasRulesSection`) eklendi.

#### `MainForm.cs`
- **(v5.1.1)** `RefreshTrendsAsync()`: `Market_Status.txt` okunur â†’ `[XU100_CANLI_VERI: MOD, TREND%]` hard data + Twitter trendleri birleÅŸik `DailyTrends` string'i oluÅŸturur.
- **(v5.1.1)** `PostMarketCloseSummary()`: `Market_Pulse_Alarm.txt` okunarak bugÃ¼nÃ¼n nabÄ±z alarmlarÄ± `pulseAnomalies` string'ine toplanÄ±r ve `GenerateMarketCloseTableTweet` zincirine iletilir.
- **(v5.3.0)** Sabah motivasyon ve gÃ¼n sonu raporu `_tweetedToday` iÃ§ine sadece doÄŸrulanmÄ±ÅŸ baÅŸarÄ±dan sonra iÅŸlenir; `*_PENDING` guard eklendi.
- **(v5.3.0)** Fenomen silme UI'Ä± `InfluencerControlService.DeleteInfluencer()` kullanÄ±r; kopya liste Ã¼zerinden silme hatasÄ± giderildi.
- **(v5.3.0)** Telegram `/ONAY` etkileÅŸim onayÄ± reply sonucunu kontrol eder; baÅŸarÄ±sÄ±zsa pending kayÄ±t silinmez.
- **(v5.3.0)** Manuel analiz tweet butonu sadece baÅŸarÄ±lÄ± analiz sonucunda aktif olur.
- **(v5.3.2)** Bot EtkileÅŸim tabÄ±na manuel `Åimdi Tara`, `BIST Fenomen`, `Kripto Fenomen`, `Durum` kontrolleri eklendi; checkbox timer'Ä± baÅŸlatÄ±r/durdurur.
- **(v5.3.2)** `/BOTDURUM`, `/ETKILESIMTARA`, `/ETKILESIMTEST @handle` Telegram komutlarÄ± eklendi.
- **(v5.3.3)** `CheckForInteractions()` artÄ±k otomatik Like/RT yapmaz; yalnÄ±zca taze, geÃ§erli handle'lÄ±, spam olmayan tweetler iÃ§in onaylÄ± reply adayÄ± Ã¼retir.
- **(v5.3.4)** Manuel analiz paylaÅŸÄ±mÄ± sadece doÄŸrulanmÄ±ÅŸ 4 parÃ§alÄ±k `ShortThread` ile yapÄ±lÄ±r; detay rapor artÄ±k X thread'e fallback edilmez.
- **(v5.3.4)** GÃ¼n sonu Ã¶zeti paylaÅŸÄ±mÄ± en fazla 4 tweet ile sÄ±nÄ±rlandÄ±; factual kapanÄ±ÅŸ formatÄ± ve YTD gÃ¼venliÄŸi zorunlu.
- **(v5.3.5)** Telegram `/ANALIZ` UI ile aynÄ± `TradingViewChartId` akÄ±ÅŸÄ±nÄ± kullanÄ±r; opsiyonel Ã¼Ã§Ã¼ncÃ¼ argÃ¼man baz (`TL/USD/EUR/XU100`) olarak alÄ±nÄ±r.
- **(v5.3.5)** Analiz kimliÄŸi sade, seviye/teyit/risk odaklÄ± tona Ã§ekildi; fenomen persona/clickbait/FOMO dili azaltÄ±ldÄ±.
- **(v5.3.6)** EtkileÅŸim reply Ã¼retimi kategori personasÄ± yerine nÃ¶tr kÄ±sa editÃ¶r tonu kullanÄ±r; hassas/alakasÄ±z iÃ§erikte `SKIP` cevabÄ± aksiyonu iptal eder.
- **(v5.3.7)** Telegram haber onay bildirimleri kÄ±sa, dÃ¼z metin ve karar odaklÄ± formata alÄ±ndÄ±; uzun reasoning/summary kaynaklÄ± Markdown riski azaltÄ±ldÄ±.
- **(v5.3.9)** Tek tweet manuel paylaÅŸÄ±mÄ± 280+ karakteri otomatik thread'e Ã§evirmez; kullanÄ±cÄ± aÃ§Ä±kÃ§a thread modunu seÃ§mek zorundadÄ±r.
- **(v5.4.0)** Manuel analiz short-thread formatÄ± 4-8 parÃ§aya Ã§Ä±karÄ±ldÄ±; ilk 2 tweet kÄ±sa Ã¶zet/devam rehberi, sonraki tweetler seviye/teyit/risk detaylarÄ±dÄ±r. 120 karakter altÄ± parÃ§alar geÃ§ersiz sayÄ±lÄ±r.
- **(v5.4.1)** AynÄ± sembol iÃ§in 7 gÃ¼n iÃ§inde tekrar sinyal gelirse detaylÄ± analiz yerine Ã¶nceki analize atÄ±f yapan 1-2 tweetlik pekiÅŸtirme thread'i paylaÅŸÄ±lÄ±r.
- **(v5.4.2)** EtkileÅŸim adaylarÄ± otomatikte yalnÄ±z finans niyeti taÅŸÄ±yan tweetlerden seÃ§ilir; promo/giveaway/RT Ã§aÄŸrÄ±sÄ± hard-block edilir ve Telegram komutlarÄ± `/ONAY_ID` formatÄ±na alÄ±nÄ±r.
- **(v5.4.3)** Ãœstat paneli yalnÄ±z `GuruHandle` mention'Ä±na izin verir; kaynak tarama tweet URL'si zorunludur ve hoca saygÄ±sÄ± Ã¶lÃ§Ã¼lÃ¼ teknik analiz diline Ã§ekildi.
- **(v5.4.4)** Sinyal tablosu `Tarih/Saat` gÃ¶sterir ve `Durum` alanÄ± gerÃ§ek sinyal durumunu takipÃ§i dostu metinle yansÄ±tÄ±r; Ã¼stat paneli Ã¶nizleme alanÄ± bÃ¼yÃ¼tÃ¼ldÃ¼ ve taslak/yayÄ±n/red geÃ§miÅŸi eklendi.
- **(v5.4.5)** Ãœstat paneline Ã§oklu hoca seÃ§imi ve `@matisay67` takas/AKD/BOFA analizi desteÄŸi eklendi; tablo parse aÅŸamasÄ± teknik analiz yapÄ±lacak adaylarÄ± gerekÃ§eyle seÃ§er.

#### `NewsEngine.cs`
- **(v5.3.6)** Haber threadleri en fazla 3 parÃ§aya sÄ±nÄ±rlandÄ±; son parÃ§ada haber Ã¶zeti/YTD gÃ¼venliÄŸi zorunlu hale getirildi.
- **(v5.3.8)** Normal skor 9 haberler Telegram onayÄ±na dÃ¼ÅŸmez; `SKIPPED_REVIEW` history'ye yazÄ±lÄ±r. YalnÄ±z skor 10 veya breaking 9+ auto-post olur.

#### `PerformanceTracker.cs`
- `RecordSignal(signal)`: Bot, Manuel veya Guru kaynaktan gelen sinyali veritabanÄ±na iÅŸler.

#### `GeminiService.cs`
- `AnalyzeChartImage(symbol, path)`: **(v3.9.0)** Grafik gÃ¶rsellerini teknik analize (RSI, Trend, Formasyon) dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
- `GenerateGuruHonoringThread(...)`: GÃ¶rsel analiz ve fiyat verisini kullanarak guru threadi Ã¼retir.
- `DetectNewsCategory(title, source)`: **(v4.2.2)** Haber kategorisini tespit eder (7 kategori).
- `AnalyzeNewsImpactTwoStep(title, source)`: **(v4.2.2)** Ã–nce kategori, sonra 1-10 skor Ã¼retir.
- `GenerateNewsCategoryAnalysis(category, title, source, link)`: **(v4.2.2)** Kategoriye Ã¶zel analiz thread'i Ã¼retir.
- **(v5.2.9)** `LoadSectorMapContext()`: `Config/BistSectorMap.md` dosyasÄ±nÄ± okur. `GenerateNewsCategoryAnalysis` ve `AnalyzeNewsForThread` Ã§aÄŸrÄ±larÄ±nda `sectorMap` parametresi olarak prompt'a enjekte edilir.
- `SendRequest(prompt)`: AI modeline metin tabanlÄ± istek gÃ¶nderir.
- `GenerateMarketCloseTableTweet(indicesData, topGainers, topLosers, topVolume, pulseAnomalies)`: **(v5.1.1)** GÃ¼n sonu kapanÄ±ÅŸ tweet thread'i Ã¼retir.

#### `LMStudioProvider.cs`
- `SendRequest(prompt)`: LM Studio'ya metin isteÄŸi gÃ¶nderir (OpenAI compat). **(v5.0.0)** Prompt baÅŸÄ±na `/no_think\n` prefix eklenir, timeout 300s.
- `SendRequestWithImage(prompt, imagePath)`: GÃ¶rsel + metin isteÄŸi gÃ¶nderir. **(v5.0.0)** Timeout 600s, `max_tokens` minimum 8192.
- `PrepareImageForVision(imagePath, maxDimension)`: **(v4.10.2)** GÃ¶rseli max 1024px'e kÃ¼Ã§Ã¼ltÃ¼r ve JPEG 85% kalitesinde kodlar.
- `BuildRequestBody(prompt, maxTokens)` / `BuildVisionRequestBody(prompt, imageUrl, maxTokens)`: **(v5.2.3)** LM Studio OpenAI-compatible isteklerine `enable_thinking=false`, `reasoning_effort=none`, `chat_template_kwargs.enable_thinking=false` ekler.
- `ExtractContentFromChoice(choice)`: **(v5.2.3)** `reasoning_content` artÄ±k publishable kabul edilmez. `content` boÅŸsa veya `finish_reason=length` ise provider `null` dÃ¶ndÃ¼rÃ¼r ve fallback tetikler.

#### `ManualAnalysisService.cs`
- **(v4.10.8)** Yerel model aktifken `IndicatorExtractor` Ã§aÄŸrÄ±sÄ± atlanÄ±r (token tasarrufu).
- **(v4.10.8)** KÄ±sa thread Ã¼retiminde yerel model iÃ§in ekran gÃ¶rÃ¼ntÃ¼sÃ¼ tekrar gÃ¶nderilmez.
- **(v4.10.8)** Yerel modele indicator context yerine ana analiz metni iletilir.

---

## ğŸ Python Scripts Map (Scripts/)

Python scriptleri "Worker" (Ä°ÅŸÃ§i) olarak Ã§alÄ±ÅŸÄ±r. C# tarafÄ±ndan komut satÄ±rÄ± argÃ¼manlarÄ± ile Ã§aÄŸrÄ±lÄ±r ve JSON Ã§Ä±ktÄ±sÄ± Ã¼retirler.

| Script DosyasÄ± | GÃ¶rev TanÄ±mÄ± | KÃ¼tÃ¼phaneler |
| :--- | :--- | :--- |
| **`playwright_daemon.py`** | **(v4.9.6 Yeni) Thread & YayÄ±n Motoru.** X-Hive bazlÄ± yeni sÃ¼per hÄ±zlÄ± bot. **v5.2.3:** `_robust_click_publish()` ile X overlay/click intercept durumlarÄ±nda Escape â†’ normal click â†’ force click â†’ JS click fallback zinciri ve hata screenshot'Ä±. | `playwright.async_api` |
| **`x_daemon.py`** | **HTTP Daemon (localhost:5580).** Tek Chrome instance ile sÃ¼rekli Ã§alÄ±ÅŸÄ±r. **(v4.9.4)** `_post_single_tweet` URL yakalama - home fallback kaldÄ±rÄ±ldÄ±, toast/profile retry eklendi. | `selenium`, `undetected_chromedriver` |
| **`social_intel.py`** | **Dev X Otomasyonu.** Selenium ile giriÅŸ yapar, arama yapar, veri Ã§eker, etkileÅŸim kurar. **v5.2.3:** TÃ¼rkÃ§e engagement parse (`B=bin`, `Mn=milyon`), own-account/bot-output filtreleri, 404 sentinel kaldÄ±rma ve status URL dedupe eklendi. | `selenium`, `pickle` |
| `omni_scout.py` | Reddit ve diÄŸer kaynaklardan viral veri Ã§eker. | `praw` (Reddit API) |
| `oracle.py` | Tahmin piyasalarÄ± verisi (Polymarket vs.) | `requests` |
| `screenshot.py` | BIST/Crypto grafiklerinin ekran gÃ¶rÃ¼ntÃ¼sÃ¼nÃ¼ alÄ±r. **v5.2.3:** Python tarafÄ±nda da `VIP'VIP-*` ve prefix sembol normalizasyonu yapar. | `selenium` |
| **`lock_manager.py`** | **Atomic File Lock.** X (Twitter) oturumlarÄ±nÄ±n Ã§akÄ±ÅŸmasÄ±nÄ± Ã¶nler. **(v4.9.3)** `acquire_lock` timeout 180s â†’ 360s. | `msvcrt`(Win) / `fcntl`(Linux) |

### ğŸ `social_intel.py` Capabilities
Bu script "Standalone" (Tek baÅŸÄ±na) Ã§alÄ±ÅŸabilen gÃ¼Ã§lÃ¼ bir bottur.
- **Driver Pool:** `ChromeDriverPool` sÄ±nÄ±fÄ± ile tarayÄ±cÄ±larÄ± Ã¶nbelleÄŸe alÄ±r (Performans artÄ±ÅŸÄ±).
- **Smart Search:** `find_influencer_posts` fonksiyonu ile hem timeline hem de genel arama yapar.
- **Human-Like Behavior:** **(v4.6.0)** `human_delay` fonksiyonu ile insansÄ± beklemeler yapar ve yakalanmayÄ± Ã¶nler.
- **Robust Typing:** **(v4.6.6)** Metni `document.execCommand('insertText', ...)` kullanarak JS enjeksiyonu ile yazar. React senkronizasyonu iÃ§in "WAKE UP" mekanizmasÄ± iÃ§erir ve TÃ¼rk karakterleri iÃ§in ultra-stabilitedir.
- **Commands:** `search_influencer`, `post_tweet`, `fetch_replies`, `discover_influencers` vb.

---

## ğŸ–¥ï¸ UI Map (ArayÃ¼z HaritasÄ±)

### ğŸ  MainForm (Ana Ekran)
*   **Sidebar (Navigasyon):**
    *   `Ana Ekran`, `Sinyal Merkezi`, `Manuel Analiz`, `Bot EtkileÅŸim`, `Ayarlar`
    *   `GeÃ§miÅŸ`, `Fenomenler`, `Haberler` (Restore Edildi), `Ãœstat Paneli`, `FenerbahÃ§e`
    *   `HIVE Intel`, `EtkileÅŸim Merkezi`
*   **Dashboard (`pnlDashboard`):**
    *   **Header:** API/Web SayaÃ§larÄ±, Ticker, Start/Stop ButonlarÄ±.
    *   **Tabs:** `Piyasa Analiz (Grafik)`, `Sosyal Medya AkÄ±ÅŸÄ± (X)`.
*   **Sinyal Merkezi (`pnlSignals`):**
    *   **Filtreler:** Strateji (King, Bomba...), Periyot, EÅŸik DeÄŸerler.
    *   **Grid:** `dgvSignals` (CanlÄ± sinyaller).
*   **Manuel Analiz (`pnlAnalysis`):**
    *   **Kontroller:** Pazar, Periyot, Sembol seÃ§imi.
    *   **Aksiyon:** Analiz Et -> SonuÃ§ (Text) + Grafik (Resim) -> Tweetle.
*   **HIVE Intel (`pnlHive`):**
    *   **Apex Ar-Ge:** Makaleler (Papers) ve GitHub RepolarÄ±.
    *   **Meta-Teacher:** Konsey (Guru) iÃ§gÃ¶rÃ¼leri tablosu.
    *   **Wisdom:** Bilgelik kÃ¼tÃ¼phanesi (`WisdomLibControl`).

### âš™ï¸ Ayarlar Paneli DetaylarÄ± (`pnlSettings`)
> **Konum:** `MainForm.cs` satÄ±r ~908-1165

**YapÄ±:** `SplitContainer` (Sol: Kategori ListBox, SaÄŸ: Ä°Ã§erik Panel)

| Kategori | Panel | Kontroller |
|----------|-------|------------|
| ğŸ”‘ API & BaÄŸlantÄ±lar | `pnlSetApi` | `txtApiKey`, `txtApiSecret`, `txtAccessToken`, `txtTokenSecret` (Twitter) |
|  |  | `txtGeminiKey`, `txtPerplexityKey`, `cmbGeminiModel` (AI) |
|  |  | `btnTestApi` (ğŸ§ª Test), `btnListModels` (ğŸ“‹ Modeller) |
|  |  | `dgvBenchmark` (Benchmark Grid), `btnRunBenchmark`, `btnAutoSelect` |
|  |  | `txtTelToken`, `txtTelChatId` (Telegram) |
|  |  | `txtTvSymbol`, `txtTvChartId` (TradingView) |
| ğŸ›¡ï¸ Spam & GÃ¼venlik | `pnlSetSpam` | `chkSpamSignals`, `chkSpamBatches`, `chkSpamManual`, `chkSpamNews` |
| ğŸ¯ Hedef & Otomasyon | `pnlSetTarget` | `txtTargetAccounts`, `chkAuto` |

**Key UI Elements:**
- **Benchmark Panel:** `pnlBenchmark` (satÄ±r ~1040-1080)
- **Kaydet Butonu:** `btnSave` (satÄ±r ~1155) â†’ `BtnSave_Click`

### ğŸ¤– OperatorForm (Ä°cra Paneli)
*   **Intelligence:** Cortex Zeka Raporu (Sol Panel).
*   **Execution:** Tweet Zinciri (SaÄŸ Panel), BaÅŸlat Butonu.
*   **Sentinel:** CanlÄ± etkileÅŸim akÄ±ÅŸÄ±.

---

## ğŸ“ Key Line References (SatÄ±r HaritasÄ±)

> **Not:** Bu satÄ±rlar deÄŸiÅŸebilir. Ancak arama yapmadan Ã¶nce burayÄ± kontrol edin.

### MainForm.cs - Panel Initialize FonksiyonlarÄ±
| Fonksiyon | SatÄ±r | AÃ§Ä±klama |
|-----------|-------|----------|
| `InitializeComponent` | 197-1208 | **ANA UI KURULUMU** - TÃ¼m paneller, kontroller |
| `ShowPanel` | 1210-1238 | Panel gÃ¶rÃ¼nÃ¼rlÃ¼k yÃ¶netimi |
| `InitializeInfluencerPanel` | 1247-1397 | Fenomenler sekmesi |
| `InitializeHistoryPanel` | 1468-1516 | GeÃ§miÅŸ sekmesi |
| `InitializeNewsPanel` | 1518-1598 | Haberler sekmesi |
| `InitializeChart` | 1806-1854 | TradingView grafik |
| `InitializeTwitterWebView` | 1977-1991 | X (Twitter) WebView |
| `InitializeServices` | 1993-2135 | TÃ¼m servislerin baÅŸlatÄ±lmasÄ± |
| `InitializeEngagementHub` | 4838-4886 | EtkileÅŸim Merkezi |
| `InitializeManualAnalysisTab` | 5015-5227 | Manuel Analiz sekmesi |
| `InitializeBotInteractionTab` | 5275-5364 | Bot EtkileÅŸim sekmesi |
| `InitializeGuruPanel` | 5487-5591 | Ãœstat Paneli |
| `InitializeFenerbahcePanel` | 5699-5828 | FenerbahÃ§e sekmesi |
| `InitializeHiveHub` | 5830-5883 | HIVE Intel hub |
| `InitializeMetaTeacherInto` | 5885-5953 | Meta-Teacher iÃ§gÃ¶rÃ¼leri |
| `InitializeWisdomInto` | 5999-6015 | Wisdom kÃ¼tÃ¼phanesi |
| `InitializeOmniScoutInto` | ~6030 | Omni-Scout UI (Yeni) |
| `InitializeOracleInto` | ~6080 | Oracle UI (Yeni) |

### MainForm.cs - Core Fonksiyonlar
| Fonksiyon | SatÄ±r | AÃ§Ä±klama |
|-----------|-------|----------|
| `LoadSettings` | 2138-2245 | Config'den UI'ya yÃ¼kleme |
| `BtnSave_Click` | 2247-2334 | UI'dan Config'e kaydetme |
| `BtnStart_Click` | 2336-2361 | WatcherlarÄ± baÅŸlatma |
| `PerformManualAnalysis` | 4137-4215 | Manuel analiz iÅŸlemi |
| `PostMorningMotivation` | 2558+ | **(v3.8.2)** Motivasyon tweeti ve zamanlamasÄ± |
| `ProcessTelegramCommands` | 4414-4756 | Telegram komutlarÄ± (/ONAY, /ANALIZ vb.) |
| `ProcessSignal` | 3985-4126 | Sinyal iÅŸleme mantÄ±ÄŸÄ± |
| `ProcessNewsQueue` | 3705-3915 | Haber kuyruÄŸu iÅŸleme |
| `Log` / `LogAI` / `LogNews` | 4249-4312 | Loglama fonksiyonlarÄ± |

### MainForm.cs - WebView & X (Twitter) FonksiyonlarÄ±
| Fonksiyon | SatÄ±r | AÃ§Ä±klama |
|-----------|-------|----------|
| `PerformInternalPostAsync` | 2708-2809 | Tweet atma (WebView2) |
| `PerformInternalThreadAsync` | 2811-3199 | Thread atma (WebView2) |
| `PerformInternalSearchAsync` | 3201-3532 | X arama (WebView2) |
| `SaveTwitterCookiesAsync` | 1875-1925 | Cookie kaydetme |
| `InjectTwitterCookiesAsync` | 1927-1975 | Cookie yÃ¼kleme |

### MainForm.cs - UI BÃ¶lgeleri (InitializeComponent iÃ§inde)
| BÃ¶lge | SatÄ±r AralÄ±ÄŸÄ± | Ä°Ã§erik |
|-------|---------------|--------|
| Field TanÄ±mlarÄ± | 60-175 | TÃ¼m UI kontrol tanÄ±mlarÄ± |
| Panel TanÄ±mlarÄ± | 260-285 | `pnlDashboard`, `pnlSettings`, `pnlHive` vb. |
| Sidebar Navigation | 286-420 | `btnNav...` butonlarÄ± |
| Dashboard Header | 425-530 | SayaÃ§lar, Ticker, Start/Stop |
| Settings Panel | 908-1165 | TÃ¼m ayarlar UI |
| AI & Model YÃ¶netimi | 939-1080 | Gemini/Perplexity, Benchmark |

### Services/ - Ã–nemli Dosyalar
| Dosya | SatÄ±r | Ä°Ã§erik |
|-------|-------|--------|
| `ModelBenchmarkService.cs` | 55-125 | `FetchAvailableModelsAsync()` |
| `ModelBenchmarkService.cs` | 130-145 | `RunBenchmarkAsync()` |
| `ModelBenchmarkService.cs` | 290-385 | `UpdateTaskPreferencesFromResults()` â€” benchmarkâ†’ModelManager dinamik gÃ¼ncelleme |
| `ModelManager.cs` | 42-150 | `InitializeTaskPreferences()` |
| `ModelManager.cs` | 155-220 | `SendRequest()` + fallback |
| `GeminiService.cs` | ~580-720 | `SendRequest()` ana mantÄ±k |
| `SocialIntelService.cs` | ~200-400 | Python script Ã§aÄŸrÄ±sÄ± |
| `SentinelService.cs` | ~80-150 | `ProcessTweetReplies()` |
| `NewsEngine.cs` | ~100-200 | Haber iÅŸleme mantÄ±ÄŸÄ± |
| `OperationManager.cs` | 295-305 | `SyncGeminiProviders()` model isimleri |

## ğŸ”„ Workflow Examples (AkÄ±ÅŸ ÅemalarÄ±)

### 1. KullanÄ±cÄ±dan Gelen "Analiz Talebi" AkÄ±ÅŸÄ±
1.  **AlgÄ±lama:** `SentinelService` -> `ProcessTweetReplies` Ã§alÄ±ÅŸÄ±r.
2.  **Veri Ã‡ekme:** `SocialIntelService.cs` -> `social_intel.py` (`fetch_replies`) Ã§aÄŸrÄ±lÄ±r.
3.  **Analiz:** Gelen yanÄ±t `GeminiService` ile analiz edilir. "TALEP: THYAO" olduÄŸu anlaÅŸÄ±lÄ±r.
4.  **Aksiyon:** `OperatorForm` Ã¼zerinde kullanÄ±cÄ±ya "Analiz Ä°steÄŸi Geldi" uyarÄ±sÄ± dÃ¼ÅŸer.

### 2. Meta-Teacher (Konsey) DÃ¶ngÃ¼sÃ¼
1.  **Tetikleme:** ZamanlayÄ±cÄ± (Timer) `SocialIntelService.PerformMetaTeacherLoopAsync` metodunu Ã§aÄŸÄ±rÄ±r.
2.  **Liste:** `InfluencerControlService` Ã¼zerinden "Konsey Ãœyeleri" listesi alÄ±nÄ±r.
3.  **Tarama:** Her Ã¼ye iÃ§in `social_intel.py` (`search_influencer`) Ã§alÄ±ÅŸtÄ±rÄ±lÄ±r. Tarih filtresiyle (Since Date) yeni tweetler aranÄ±r.
4.  **Ã–ÄŸrenme:** Bulunan analizler `MemoryEngine` iÃ§ine kaydedilir (`Learn`).
5.  **Ä°Ã§gÃ¶rÃ¼:** Ã–nemli bir strateji bulunursa `OnMetaTeacherInsight` eventi tetiklenir ve kullanÄ±cÄ±ya sunulur.

### 3. Cortex Strateji DÃ¶ngÃ¼sÃ¼ (HIVE Phase 3)
1.  **Veri HazÄ±rlÄ±ÄŸÄ±:** `OmniScout` (Viral) ve `Oracle` (Piyasa) servisleri arka planda veri Ã§eker ve `LastReport` deÄŸiÅŸkenini gÃ¼nceller.
2.  **Tetikleme:** KullanÄ±cÄ± `OperatorForm` -> Sentez sekmesinden **"CORTEX ANALÄ°ZÄ° BAÅLAT"** butonuna basar.
3.  **Sentez:** `CortexService` tÃ¼m raporlarÄ± `Gemini`'ye gÃ¶nderir.
4.  **SonuÃ§:** AI, verileri Ã§aprazlayarak (Cross-Reference) bir strateji Ã¼retir ve UI'da gÃ¶sterir.

---

## âš ï¸ Kritik Notlar & Kurallar

1.  **JSON Ä°letiÅŸimi:** C# ve Python arasÄ±ndaki veri alÄ±ÅŸveriÅŸi **her zaman JSON** formatÄ±ndadÄ±r. Python tarafÄ±nda `---JSON_START---` ve `---JSON_END---` markerlarÄ± kullanÄ±lÄ±r.
2.  **Thread Safety:** `SentinelService` ve `OperationManager` asenkron Ã§alÄ±ÅŸÄ±r. UI gÃ¼ncellemeleri iÃ§in `Invoke` zorunludur.
3.  **Dil KuralÄ±:** Kod iÃ§i (deÄŸiÅŸkenler, yorumlar) Ä°ngilizce, **UI ve Loglar TÃ¼rkÃ§e** olmalÄ±dÄ±r.
4.  **Hata YÃ¶netimi:** Python scripti hata verirse JSON iÃ§inde `status: "error"` dÃ¶ner. C# tarafÄ± bunu `Logger.Sys` ile loglamalÄ±dÄ±r.

---

## ğŸ“‚ Server Deployment Paths (CanlÄ± Ortam)

CanlÄ± sunucudaki (v3.7.6 ve sonrasÄ±) dosya yollarÄ±:

| Ä°Ã§erik | Sunucu Yolu |
| :--- | :--- |
| **Uygulama DosyalarÄ±** | `G:\DiÄŸer bilgisayarlar\Sunucu\XiDeAI Pro` |
| **Log DosyalarÄ±** | `G:\DiÄŸer bilgisayarlar\Sunucu\XiDeAI` |











































































