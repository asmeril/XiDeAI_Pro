# ?? XiDeAI Pro - Proje Gelistirme G?nl?g?

Bu g?nl?k, proje ?zerinde yapilan degisiklikleri, mimari kararlari ve g?nl?k ilerlemeyi takip etmek i?in tutulmaktadir.


## ?? 27 Subat 2026

## ?? 27 Subat 2026

### ?? v4.6.3 - Global 280-Character Limit Enforcer (HOTFIX)

**Degisiklikler:**
- **Merkezi X Karakter Siniri:** `ThreadService.SplitText` metodu `SocialIntelService` aktarim katmanina entegre edildi. Artik Haber, Sinyal, veya Manuel Analiz fark etmeksizin g?nderilen her tweet par?asi 280 karakteri asiyorsa akillica (c?mle b?t?nl?g? korunarak) alt zincirlere b?l?n?r. Mavi tiksiz hesaplarda yasanan "Tweet karakter sinirini asti" hatalari tamamen ?nlendi.
- **AI Prompt Revizyonlari:** `PromptManager.cs` i?erisindeki t?m promptlara AI'in 280 karakter altinda i?erik ?retmesini zorunlu kilan kati y?nergeler eklendi.

---

### ?? v4.6.2 - Cookie Format & Telegram Fixes (HOTFIX)

**Degisiklikler:**
- **undetected-chromedriver Cookie Senkronizasyonu:** WebView2'den kaydedilen JSON ?erezlerindeki `isSecure`, `isHttpOnly` ve `expires` anahtarlarinin Selenium'un bekledigi `secure`, `httpOnly`, `expiry` anahtarlarina d?n?st?r?lmesi saglandi. Bu sayede `Failed to load cookies` hatasi ??z?ld? (`social_intel.py` & `x_daemon.py`).
- **Telegram Baglanti Testi:** `/start` eksikliginden kaynakli "chat not found" (HTTP 400) hatalari UI'da mesaj kutusu seklinde g?sterilerek daha belirgin uyari mekanizmasi yazildi (`TelegramService.cs`).

---

## ?? 21 Ocak 2026

### ?? v4.4.0 - X Daemon Architecture (MAJOR)

**Problem:** Her X islemi i?in yeni Python/Chrome baslatiliyordu. Bu:
- 5-15 sn startup overhead
- Zombi Chrome prosesleri
- Lock dosyasi ?atismalari
- S?rekli 90s timeout hatalari

**??z?m:** HTTP Daemon mimarisi - tek Chrome instance, s?rekli a?ik.

**Yeni Dosyalar:**
- `Scripts/x_daemon.py` - HTTP sunucu (localhost:5580)
  - 8 endpoint: `/search`, `/timeline`, `/find_handle`, `/like`, `/post_thread`, `/reply`, `/fetch_news`, `/health`
  
**Degisen Dosyalar:**
- `SocialIntelService.cs`:
  - `StartDaemonAsync()`, `StopDaemon()`, `DaemonRequestAsync()` eklendi
  - 4 kritik metod daemon-first g?ncellendi
- `OperationManager.cs`: App baslangicinda daemon otomatik baslatma

**Performans Iyilestirmesi:**
- Eski: Her islem ~30-60 saniye (Chrome baslatma dahil)
- Yeni: Her islem ~5-20 saniye (Chrome hep a?ik)

---

## ?? 20 Ocak 2026

### ?? v4.2.x - Two-Step Intelligence Update

Bu g?ncelleme dizisi, hem Bot Etkilesim hem de Haber mod?llerine "iki asamali zeka" sistemini getirmektedir.

#### v4.2.0 - Bot Interaction Two-Step Logic
- **Ama?:** Tek promptlu sistemden, ?nce kategori tespit + sonra kategoriye ?zel yanit ?reten sisteme ge?is.
- **6 Kategori:** FINANS, ESREF_TEK, MILLI_TOPLUM, BILGE_KULTUR, INSAN_RUH, GUNLUK_MIZAH
- **Degisen Dosyalar:** `PromptManager.cs`, `GeminiService.cs`, `MainForm.cs`, `InteractionEngine.cs`

#### v4.2.1 - Round-Robin Category Search
- **Ama?:** Her taramada t?m konulari aramak yerine, d?ng?sel olarak tek bir kategoriye odaklanma.
- **Degisen Dosyalar:** `ConfigManager.cs`, `MainForm.cs`

#### v4.2.2 - News Two-Step Logic (MAJOR)
- **Ama?:** Haberleri kategorize edip, ?nem skoruna (1-10) g?re farkli akislara y?nlendirmek.
- **Yeni Skorlama:**
  - 10: Otomatik paylas (haber + analiz)
  - 9: Onay bekle (haber + analiz)
  - 7-8: Onay bekle (sadece haber)
  - <7: Reddet
- **7 Haber Kategorisi:** EKONOMI, SIYASET, TEKNOLOJI, GLOBAL, KRIPTO, SPOR, YASAM
- **?zel ?zellikler:**
  - SON DAKIKA boost: "Son dakika" i?eren haberler minimum skor 8
  - Fenerbah?e bypass: FB haberleri minimum skor 7
  - Kategoriye ?zel AI promptlari (Bot etkilesim gibi)
- **Degisen Dosyalar:**
  - `PromptManager.cs` (10 yeni metod)
  - `GeminiService.cs` (3 yeni metod: DetectNewsCategory, AnalyzeNewsImpactTwoStep, GenerateNewsCategoryAnalysis)
  - `NewsEngine.cs` (ProcessNews tamamen yenilendi)
  - `MainForm.cs` (Event handler'lar g?ncellendi)

---

### ?? FanZone 2.0 (v4.0.0 G?ncellemesi)
Kullanici geri bildirimiyle FanZone mod?l?, "sadece tweet atma" araci olmaktan ?ikip tam tesekk?ll? bir **Etkilesim Merkezi** haline getirildi.

**1. Yeni "Kadro" Paneli (UI)**
*   Aray?z?n sagina **Takip Listesi** eklendi.
*   Resmi Hesaplar, Sporcular ve Muhabirler ayristirildi.
*   Anlik g?ncelleme yetenegi eklendi.

**2. Otomatik Sporcu Kesfi (Auto-Discovery)**
*   `AthleteDiscoveryService` gelistirildi. Sistem, Google aramasi yaparak Fenerbah?e kadrosunu tarar ve Twitter adreslerini bulup havuza ekler.

**3. Polyglot (?ok Dilli) Yanit**
*   Yabanci oyuncularin tweetlerine (Ingilizce, Portekizce vb.) hem kendi dillerinde hem de T?rk?e yanit verme yetenegi kazandirildi.
*   ?rn: *"Great job! / Harika is!"*

**4. Etkilesim Garantisi**
*   Resmi hesaplara **%100 Like & RT** garantisi.
*   Sporcu hesaplarina **%100 Like** garantisi.

## ?? 19 Ocak 2026 (Devam)

### ?? v3.9.4 - "Phoenix" Stability & UI Recovery
Twitter posting tikanikligi, kilitlenme sorunlari ve bos aray?z problemleri tamamen giderildi.

**1. Twitter & Kilit Mekanizmasi (Fixed)**
*   **Sorun:** `social_intel.py` s?recinin kilit mekanizmasinda asili kalmasi t?m sosyal medya akisini durduruyordu.
*   **??z?m:** `lock_manager.py` optimize edildi, bekleme s?releri kisaltildi ve stale lock temizleme mantigi g??lendirildi.
*   **Iyilestirme:** `screenshot.py` ve `MainForm.cs` i?inde `VIP-` sembol temizleme mantigi standardize edildi.

**2. Sinyal Analiz & KPI Recover (Fixed)**
*   **Sorun:** "Sinyal Analiz" tabi bos g?r?n?yor, istatistikler g?ncellenmiyordu.
*   **??z?m:** `LoadSignalHistory` ve `UpdateKPICards` metodlari hayata ge?irildi. Sinyal kayit (`RecordSignal`) mantigi veri kaybini ?nlemek i?in paylasim ?ncesine ?ekildi.
*   **G?r?n?rl?k:** `SignalEngine.OnLog` olaylari UI'ya baglandi, robot sessizligi giderildi.

**3. Mimar? Iyilestirmeler**
*   `OperationManager` servis y?kleme sirasi bagimliliklara g?re yeniden d?zenlendi.
*   Loglama sistemi seffaflastirildi.

## ?? 16 Ocak 2026

### ?? ACIL D?ZELTME: v3.7.6 Sunucu Sorunlari
Sunucu tarafinda tespit edilen (v3.7.6) kritik hatalar analiz edildi ve d?zeltildi.

**1. "Fenomenler" Sekmesi ??kmesi (Fixed)**
*   **Sorun:** Sekme a?ilisinda `InitializeInfluencerPanel` ?alisirken, kategori filtresi `SelectedIndex` degisimi `Refresh` tetikliyordu. Ancak liste (`ListView`) hen?z olusturulmadigi i?in `NullReferenceException` olusuyor ve uygulama kapaniyordu.
*   **??z?m:** Filtre tetikleyicisi (Event Handler), liste olusturulduktan sonraya tasindi.

**2. ?stat Analizi Screenshot Hatasi (Fixed)**
*   **Sorun:** Sunucuda `chromedriver.exe` AppData klas?r?nde bulunamiyordu (Kurulum Program Files'a yapiliyor). Bu nedenle `screenshot.py` s?r?c?y? bulamayip hata veriyordu.
*   **??z?m:** `ScreenshotService.cs` i?ine akilli yol tespiti eklendi. Artik s?r?c? su sirayla araniyor:
    1.  `AppData/XiDeAI/drivers` (Eski y?ntem)
    2.  `AppDirectory/drivers` (Portable/Server kurulumu i?in)
    3.  `AppDirectory` (K?k dizin)

### ?? UI/UX Modernizasyon (v2.0)
Kullanici geri bildirimleri ve "Finansal Analiz Odakli" vizyon dogrultusunda aray?z bastan asagi yenilendi.

**Yapilan Degisiklikler:**
1.  **?? Sidebar (Navigasyon) D?zenlendi:**
    *   Butonlar **ANALIZ**, **ZEKA** ve **SISTEM** basliklari altinda gruplandi.
    *   G?rsel karmasa giderildi, daha temiz bir hiyerarsi kuruldu.
2.  **?? Sinyal Merkezi 2.0:**
    *   **KPI Paneli:** G?nl?k Sinyal, Basari Orani ve Trend g?stergeleri eklendi.
    *   **Akilli Filtreler:** Checkbox yigini yerine modern "Chip" butonlar kullanildi.
    *   **Smart Grid:** Tablo renklendirildi, skorlara g?re yesil/sari/gri renk kodlari tanimlandi.
3.  **??? HIVE Hub (Birlesme):**
    *   "Haberler" ve "Fenomenler" mod?lleri, HIVE Intel ?atisi altina tasindi.
4.  **?? Iletisim Merkezi (Consolidation):**
    *   `Bot Etkilesim` ve `Etkilesim Merkezi` tek ?ati altinda birlestirildi.
5.  **?? Ayarlar 2.0:**
    *   Kategori bazli (Sol Men? - Sag Detay) yapiya ge?ildi.

### ??? Derin Inceleme ve Indeksleme
Proje ?zerinde ?alisan AI asistanin (ben) kod tabanina tam hakimiyet saglamasi i?in detayli bir "Deep Inspection" (Derin Inceleme) yapildi.

## ?? 16 Ocak 2026 (Bug?n)

### ??? v3.7.8 - Stability Update
Kullanici geri bildirimlerine dayali kritik d?zeltmeler ve iyilestirmeler yapildi.

**1. "Haberler" Tabi Restorasyonu**
*   **Durum:** v3.7.7'de "HIVE Intel" altina tasinan haberler mod?l?, kullanici aliskanligi nedeniyle sol men?de eksik hissediliyordu.
*   **Islem:** `MainForm.cs` i?inde `btnNavNews` butonu geri getirilerek sol men?ye, "ZEKA (HIVE)" basliginin altina eklendi.

**2. Screenshot Service (K?k Neden ??z?m?)**
*   **Sorun:** Sunucu ortaminda `chromedriver.exe` yolunun bulunamamasi veya s?r?m uyumsuzlugu.
*   **Islem:** `ScreenshotService.cs`'e ?zel sunucu yolu (`C:\Users\asmeril\AppData\...`) eklendi. `screenshot.py` dosyasina `SessionNotCreatedException` yakalama ve otomatik driver g?ncelleme (fallback) yetenegi kazandirildi.

**3. Gelismis Hata Raporlama (Apex/Omni/Oracle)**
*   **Sorun:** Analiz servisleri hata verdiginde sadece "Sentez basarisiz" yaziyordu.
*   **Islem:** T?m servislerin (`ApexService`, `OmniScoutService`, `OracleService`) hata yakalama bloklari detaylandirildi.

**4. Model Isimleri D?zeltmesi (Kritik)**
*   **Sorun:** `ModelManager.cs` i?inde yanlis Gemini model isimleri (`gemini-flash`, `gemini-pro-2.0`) kullaniliyordu. API bu isimleri tanimiyordu ve bos yanit d?n?yordu.
*   **Islem:** T?m model isimleri dogru API identifierlariyla degistirildi (`gemini-2.0-flash-exp`, `gemini-1.5-flash`, `gemini-1.5-pro`).
*   **?? NOT:** Bu model isimleri daha sonra (v3.8.5) deprecate edilip `gemini-2.5-flash`, `gemini-2.5-pro` ile degistirildi.

### ?? v3.7.9 - Ayarlar Sayfasi Yeniden Tasarimi

**1. Eksik Butonlarin Geri Getirilmesi**
*   **?? Test** butonu: Se?ili modeli test edip yanit s?resini g?sterir.
*   **?? Modeller** butonu: T?m kullanilabilir Gemini modellerini listeler.

**2. Akilli Model Se?imi (Benchmark)**
*   **Yeni Servis:** `ModelBenchmarkService.cs` - T?m modelleri paralel test eder.
*   **Benchmark Grid:** Model adi, tier, yanit s?resi ve durum tablosu.
*   **Otomatik Se?:** Benchmark sonu?larina g?re en uygun modeli se?er.

**3. UI/UX Iyilestirmeleri**
*   Kompakt tek satir d?zeni (label + input + button ayni satirda).
*   Sol panel k???lt?ld? (180px), daha fazla i?erik alani.
*   Renk kodlamasi: Gemini=LimeGreen, Perplexity=Cyan, TradingView=Cyan, Telegram=Orange.

**4. Dinamik Model Listesi (Canli API)**
*   **?? Modeller** butonu artik API'den ger?ek zamanli model listesi ?eker.
*   `ModelBenchmarkService.FetchAvailableModelsAsync()` metodu eklendi.
*   Embedding, TTS ve image modellerini otomatik filtreler.
*   ComboBox'i g?ncel modellerle g?nceller.
*   Fallback: API basarisiz olursa ?nceden tanimli modeller kullanilir.

---

## ?? 16 Ocak 2026

### v3.8.1 Release Notes
- **Tarih:** 16.01.2026
- **Odak:** HIVE Phase 3 - Cortex Integration
- **Degisiklikler:**
  - `CortexService.cs` implemente edildi.
  - OperatorForm Sentez sekmesine Cortex entegrasyonu yapildi.
  - `OmniScout` ve `Oracle` servislerine `LastReport` ?zelligi eklendi.
  - `SentinelService` duplicate kod temizligi yapildi.
  - Cross-Reference (?apraz Referans) mantigi kuruldu.

### ?? v3.8.0 Release - HIVE Protocol Phase 1 & 2

> **Odak:** HIVE Mod?l? entegrasyonunun tamamlanmasi (OmniScout, Oracle) ve Operasyon Merkezi (OperatorForm) iyilestirmeleri.

#### ?? Yeni ?zellikler (HIVE Mod?l?)
*   **Omni-Scout (Viral) Tab:** HIVE mod?l?ne eklendi. Global viral trendleri ve Reddit akisini tarayip raporluyor.
*   **Oracle (Kahin) Tab:** HIVE mod?l?ne eklendi. Polymarket verileriyle gelecek senaryolari (scenario generation) ?retiyor.
*   **Canli Sentez Raporu:** OperatorForm "Sentez" sekmesi artik Sentinel'den gelen ger?ek etkilesim verileriyle (toplam yanit, duygu durumu, ?ne ?ikan geri bildirimler) doluyor.

#### ??? Iyilestirmeler & D?zeltmeler
*   **Sentinel Analysis History:** Sentinel servisine ge?mis analizleri bellekte tutma yetenegi eklendi (`_analysisHistory`).
*   **Icra (Execution) G?venligi:** Tweet g?nderim butonu (`BtnLaunchNext`) artik API hatalarini yutmuyor, kullaniciya net hata mesaji g?steriyor ve logluyor.
*   **UI Tutarliligi:** T?m HIVE sekmeleri i?in standart `UpdateUI` metodlari eklendi.

#### ?? Bilinen Eksikler (HIVE Phase 3)
*   **Cortex Engine:** ?apraz referans motoru hen?z aktif degil.
*   **Vision-Grid:** G?rsel analiz mod?l? beklemede.

### ?? v3.8.2 - Resurrection Update (Dirilis)

**Odak:** Kullanici geri bildirimlerine dayali kritik sistem onarimlari ve mod?llerin (Influencer, Haber, Motivasyon) yeniden devreye alinmasi.

**1. "Fenomen Veritabani" Restorasyonu**
*   **Sorun:** Sidebar men?s?nden "Fenomenler" butonu kaybolmustu, panele erisilemiyordu.
*   **??z?m:** `MainForm.cs` i?inde `btnNavInfluencers` butonu HIVE grubuna yeniden eklendi.

**2. Motivasyon Mod?l? Onarimi**
*   **Sorun:** Sabah motivasyon tweetleri atilmiyordu. Kod incelemesinde ilgili metodun (`PostMorningMotivation`) silindigi tespit edildi.
*   **??z?m:** Metot yeniden yazilarak sisteme entegre edildi. Zamanlama mantigi "tam 09:00" yerine "09:00-10:00 araligi" olarak esnetildi, b?ylece bilgisayarin o dakika kapali olmasi durumunda bile a?ildiginda telafi etmesi saglandi.

**3. Haber Filtresi Gevsetme (Anti-Silent Mode)**
*   **Sorun:** Haber mod?l?, kati filtreler (24 saat siniri ve dar keyword listesi) nedeniyle bir?ok ?nemli haberi "sessizce" yutuyordu.
*   **??z?m:**
    *   **S?re:** 24 saat siniri **48 saate** ?ikarildi (Hafta sonu akisi i?in).
    *   **Keyword:** Finansal terim listesi ("temett?", "bilan?o", "halka arz" vb.) 3 katina ?ikarildi.
    *   **Test Modu:** `ConfigManager`'a `NewsTestMode` eklendi. Aktif edildiginde t?m filtreleri bypass eder.

**4. Bot Iletisim Analizi**
*   **Durum:** Botun cevap vermemesi sorunu incelendi. Sorunun bot kodunda degil, kaynak tweet (haber/motivasyon) eksikliginde oldugu anlasildi. Tweet akisi basladiginda botun dogal olarak etkilesime girecegi teyit edildi.

---

## 16 Ocak 2026

### v3.8.3 Release

> **Odak:** G?nl?k Rapor istatistiklerinin d?zeltilmesi, Guru Taramalari i?in performans takibinin etkinlestirilmesi ve rapor i?eriginin "reklam dostu" hale getirilmesi.

#### ? Yeni ?zellikler & Iyilestirmeler

**1. "D?r?st Istatistik, Sik Vitrin" Raporlama**
*   **Sorun:** G?nl?k raporda kazanan/kaybeden oranlari g?sterilmiyordu ??nk? Guru mod?l?nden gelen sinyallerin kapanis fiyatlari takip edilmiyordu.
*   **??z?m (D?r?stl?k):** `PerformanceTracker` artik t?m sinyalleri (pozitif/negatif) dahil ederek "Basari Orani" ve "Kar Fakt?r?" hesapliyor.
*   **??z?m (Vitrin):** Ancak raporun g?rsel kisminda (liste) **sadece en ?ok kazandiran ilk 3 sinyal** listeleniyor. Kaybettirenler gizleniyor ancak istatistige dahil ediliyor.

**2. Guru Scan Performans Takibi**
*   **Eksik:** "Efe HMA", "Trend Temelli" gibi g?r?nt? islemeyle alinan sinyaller `PerformanceTracker`'a kaydedilmiyor, bu y?zden basari oranlari "0%" kaliyordu.
*   **??z?m:** `MainForm.cs` i?inde tarama sonu?lari artik `RecordSignal` ile veritabanina isleniyor.

**3. Kapanis ?ncesi Fiyat G?ncellemesi (PnL Fix)**
*   **Sorun:** G?n sonu raporu hazirlanirken sinyallerin "o anki" fiyati g?ncellenmedigi i?in PnL hesaplanamiyordu.
*   **??z?m:** `PostMarketCloseSummary` ?alismadan hemen ?nce, o g?n?n t?m a?ik sinyalleri i?in `PriceFetchService` ile g?ncel fiyat kontrol? yapiliyor.

**4. Versiyon Y?netimi**
*   T?m proje (Installer, Assembly, Docs) v3.8.3 s?r?m?ne senkronize edildi.



## ?? 18 Ocak 2026

### ??? v3.8.3 Hotfix & Infrastructure Upgrade

**1. Haber Takip Hatti Onarimi (News Pipeline Fix)**
*   **Sorun:** RSS ve Twitter'dan haberler ?ekiliyor ancak ana sayfaya d?sm?yor veya islenmiyordu.
*   **K?k Neden:** `MainForm.cs` i?inde `InitializeNewsPanel` metodunda, `NewsTracker` servisinin olay tetikleyicisi (`OnNewsDetected`) dinlenmiyordu. Servis bosluga bagiriyordu.
*   **??z?m:** Eksik olan `+= OnNewsReceived` baglantisi eklendi.

**2. Atomic Lock Manager (Dosya Kilit Sistemi)**
*   **Ama?:** `social_intel.py` scriptinin ayni anda birden fazla kez ?alisip X oturumunu bozmasini (cookies ?akismasi) engellemek.
*   **Uygulama:** `Scripts/lock_manager.py` mod?l? yazildi. Platform bagimsiz (Windows/Linux) dosya kilitleme ve "stale lock" temizleme mantigi kuruldu.

---

## 18 Ocak 2026

### v3.8.4 Release - Fenomenler Tab Fix

**Fenomenler Sekmesi ??kme D?zeltmesi**
*   **Sorun:** "Fenomenler" sekmesine tiklandiginda uygulama hi?bir hata vermeden kapaniyordu.
*   **K?k Neden:** `RefreshInfluencerListView` metodu ComboBox'i temizlerken `SelectedIndexChanged` eventini tetikliyor, bu da yeniden ayni metodu ?agirarak sonsuz d?ng? olusturuyordu. D?ng? sirasinda `null` referans hatasi olusuyordu.
*   **??z?m:** 
    1. `_suppressEvents` bayragi eklenerek event handler susturuldu.
    2. `if (selected == null) return;` guard eklendi.
    3. `SelectedIndex == -1` kontrol? eklendi.

### v3.8.5 Release - AI Model Migration

**1. Gemini Model Isimleri G?ncellendi (KRITIK)**
*   **Sorun:** `gemini-1.5-flash` ve `gemini-1.5-pro` modelleri API tarafindan `NotFound` hatasi d?nd?r?yordu (deprecate edilmis).
*   **??z?m:** T?m model referanslari g?ncellendi:
    *   `gemini-1.5-flash` ? `gemini-2.5-flash` (Stabil, ?retim)
    *   `gemini-1.5-pro` ? `gemini-2.5-pro` (Stabil, ?retim)
*   **Etkilenen Dosyalar:** `ModelManager.cs`, `OperationManager.cs`, `ModelBenchmarkService.cs`

**2. NewsEngine Rate Limiting**
*   **Sorun:** 13 haber ayni anda islenmeye ?alisilinca `TooManyRequests` hatasi aliniyordu.
*   **??z?m:** `ProcessNews` metoduna 2 saniyelik bekleme (`await Task.Delay(2000)`) eklendi.

**3. Otomatik Model Optimizasyonu (YENI)**
*   **?zellik:** Sistem artik her a?ilista ve gece 03:00'te Gemini API modellerini benchmark testine sokuyor.
*   **Fayda:** O anki en hizli ve maliyet-etkin model (?rn: `2.0-flash` vs `2.5-flash`) otomatik se?iliyor.

**4. Sosyal Veri Stabilizasyonu (Hotfix)**
*   **Sorun:** Eda Erdem vb. hesaplarda `malformed result item` hatasi aliniyordu.
*   **??z?m:** `SocialIntelService` JSON ayristiricisina defansif kod eklendi. Eksik alan geldiginde hata vermek yerine varsayilan deger ataniyor.

---

## 18 Ocak 2026

### v3.9.0 Release - Visionary Guru Analysis (18.01.2026)
**Odak:** ?stat Analizi (Guru) mod?l?ne AI Vision (G?rsel Analiz) ve Smart Money mantigi eklenmesi.
- **AI Vision:** `GeminiService.AnalyzeChartImage` ile grafikler artik teknik olarak yorumlaniyor.
- **Smart Money:** MSB, FVG ve Likidite kavramlari analizlere dahil edildi.
- **Kalite:** Guru analizleri artik Sinyal kalitesine y?kseltildi, "Piyasa G?r?sleri" kaldirildi.

### v3.9.1 Release - Prompt & Stability Polish (18.01.2026)
**Odak:** Kullanici deneyimi iyilestirmesi ve sistem kararliligi.
- **Prompt Fix:** Tweet ?iktilarindaki gereksiz basliklar (`(Birinci Tweet Metni)`) temizlendi.
- **Build Fix:** C# tarafindaki tirnak isareti ve degisken ismi hatalari giderildi.
- **Auto-Publish:** Yayin s?reci v3.9.1 i?in otonom olarak tamamlandi.

---

## 19 Ocak 2026

### v3.9.2 Release - Guru Tagging & Strategy Focus (19.01.2026)
**Odak:** ?stat analizlerinde hoca etiketleme hatalarinin giderilmesi ve tarama adi vurgusu.

**1. Dinamik Hoca Etiketleme (Handle-Based):**
*   **D?zeltme:** Artik `@Efelerin Efesi` (hatali) yerine dogrudan hocanin handle'i (`@EFELERiiNEFESi3`) dinamik olarak kullaniliyor.
*   **Iyilestirme:** AI'nin hocayi yanlis etiketlemesini veya bosluklu isim kullanmasini engelleyen handle-based yapiya ge?ildi.

**2. Tarama Adi ve Vurgu Revizyonu:**
*   **Terminoloji:** "Radar" kelimesi yerine kullanici istegi dogrultusunda "Tarama" vurgusu tercih edildi.
*   **Vurgu:** Ilk tweetlerde `Efe HMA` veya `Trend Temelli` gibi tarama tablosunun adi (strategy) ?n plana ?ikarildi.
*   **Dinamik Intro:** 5 farkli giris tarzi bu yeni metadata ile uyumlu hale getirildi.

**3. Kritik Build & Syntax Onarimlari:**
*   `PromptManager.cs` ?zerindeki verbatim string sonlandirma hatasi (`";` syntax hatasi) giderildi.
*   Unicode/Emoji kaynakli derleme risklerini ?nlemek i?in teknik prompt i?erigi sadelestirildi.

---

## 19 Ocak 2026

### v3.9.3 Release

> TODO: Release notes eklenecek.

---

## 19 Ocak 2026

### v3.9.4 Release

> TODO: Release notes eklenecek.

---

## 20 Ocak 2026

### ?? v4.0.0 Release - "Clean Slate" (Temiz Sayfa)

> **Odak:** HIVE Protocol'?n tamamen temizlenmesi, kod tabaninin AI egitimine hazirlanmasi ve yerel RAG sistemi altyapisinin kurulmasi.

#### ?? HIVE Protocol Temizligi (Major Refactor)

**1. Tamamen Kaldirilan Mod?ller:**
*   `SentinelService.cs` - Etkilesim izleme servisi
*   `ApexService.cs` - Ar-Ge analiz servisi
*   `OmniScoutService.cs` - Viral trend servisi
*   `OracleService.cs` - Senaryo ?retim servisi
*   `WisdomService.cs` - Bilgelik servisi
*   `CortexService.cs` - ?apraz referans motoru
*   `MetaTeacherService.cs` - Meta-?grenme servisi
*   `OperatorForm.cs` - HIVE operasyon formu (Backup: `HiveProjesi/Forms/`)

**2. Temizlenen Kod Artiklari:**
*   `MainForm.cs`: HIVE Hub paneli, Sentinel Inbox, navigasyon butonlari ve Telegram komutlari silindi.
*   `GeminiService.cs`: `GenerateMetaAnalysisV2` ve `GenerateMetaAnalysis` metodlari kaldirildi.
*   `OperationManager.cs`: HIVE servis referanslari ve yorumlari temizlendi.
*   T?m `[HIVE REMOVED]` etiketli yorum satirlari silindi.

**3. Yedekleme:**
*   T?m HIVE kodu `d:\Projects\HiveProjesi` klas?r?ne tasindi.
*   `KURULUM_REHBERI.md` ile yeniden entegrasyon talimatlari belgelendi.

#### ?? Yerel AI Egitim Sistemi (YENI)

**1. Kod Export Script'i (`Scripts/export_for_ai.ps1`):**
*   T?m `.cs`, `.py` ve `.md` dosyalarini `AI_Knowledge_Base/` klas?r?ne toplar.
*   49 C# + 13 Python + 13 dok?mantasyon dosyasi export edildi.

**2. RAG Sistemi (`Scripts/setup_rag.py`):**
*   LangChain + ChromaDB kullanarak kod tabanini vekt?r veritabanina indeksler.
*   DeepSeek Coder V2 ile uyumlu.

**3. Interaktif Chat (`Scripts/xideai_rag.py`):**
*   Kod tabani hakkinda soru-cevap yapabilen CLI aray?z?.
*   ?rnek: "MainForm.cs ne is yapar?" ? AI detayli a?iklama verir.

#### ??? Teknik D?zeltmeler

**1. Duplicate Build Hatasi ??z?m?:**
*   **Sorun:** `AI_Knowledge_Base/codebase/` i?indeki .cs dosyalari derlemeye dahil oluyordu.
*   **??z?m:** `.csproj` dosyasina `<Compile Remove="AI_Knowledge_Base\**\*.cs" />` eklendi.

**2. SendTweetAsync API Uyumlulugu:**
*   **Sorun:** Metod artik `bool` yerine `string?` (tweet URL) d?nd?r?yor.
*   **??z?m:** 4 adet string?bool d?n?s?m hatasi d?zeltildi.

---

---

## 20 Ocak 2026

### v4.3.0 Release

> **Odak:** Sinyal sisteminin "Two-Step Intelligence" ile g??lendirilmesi. Hibrit puanlama, strateji odakli personalar ve i?erik tiering sistemi.

#### ?? Hybrid Signal Intelligence (Hibrit Sinyal Zekasi)

**1. Iki Kademeli Puanlama Sistemi:**
*   **Normalized Score:** Teknik g?stergelerden gelen ham puan (0-100).
*   **Final Score (Hybrid):** AI onayi, g?rsel formasyon ve fenomen destegi ile zenginlestirilmis nihai karar puani.
*   **Content Tiering:** Puana g?re i?erik derinligi belirlenir:
    *   **Premium (85+):** Detayli analiz, formasyon grafigi, fenomen alintilari.
    *   **Standard (65-84):** Temel analiz ve hedef fiyatlar.
    *   **Summary (50-64):** Kisa ?zet.

**2. Strateji Bazli Persona Y?netimi (Prompt Dispatcher):**
*   **KING/BOMBA:** "Agresif ve Hype Odakli" - Emoji ve cosku agirlikli dil.
*   **TEFO:** "Teknik ve Formasyon Odakli" - Seviyeler ve kirilimlar.
*   **ANKA:** "Dirilis ve D?n?s Odakli" - Dip d?n?s sinyalleri.
*   **DIP/ZIRVE:** "RSI ve Asirilik Odakli" - Tepki b?lgeleri.
*   **Standart:** Dengeli profesyonel dil.

**3. Otomatik I?erik Zenginlestirme:**
*   **Gemini 2.0 Vision:** Grafiklerdeki formasyonlari (Flama, Tobo vb.) otomatik okur ve analize dahil eder.
*   **Influencer Intelligence:** Ilgili hisse hakkinda konusan fenomenlerin tweetlerini (Smart Money) analize ekler.

#### ??? Teknik Iyilestirmeler
*   `SignalEngine`: Is akisi `ProcessStructuredSignal` ile modernize edildi.
*   `PromptManager`: `GetStrategySpecificPrompt` ile stratejiye ?zel prompt se?imi eklendi.
*   `ThreadService`: `PostAIGeneratedThread` ile AI tarafindan ?retilen hazir thread formati (||| ayraci) desteklendi.
*   **Zirve Sinyali Fix:** Esik degeri 12'den 10'a d?s?r?lerek algilama hassasiyeti artirildi.

---

## 20 Ocak 2026

### v4.3.1 Release

> TODO: Release notes eklenecek.

---

## 20 Ocak 2026

### v4.3.2 Release

> TODO: Release notes eklenecek.

---

## 20 Ocak 2026

### v4.3.3 Release

> TODO: Release notes eklenecek.

---

## 20 Ocak 2026

### v4.3.4 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.3.5 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.0 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.1 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.2 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.3 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.4 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.5 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.6 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.7 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.8 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.4.9 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.5.0 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.5.1 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.5.2 Release

> TODO: Release notes eklenecek.

---

## 21 Ocak 2026

### v4.5.3 Release

> TODO: Release notes eklenecek.

---

## 22 Ocak 2026

### v4.5.4 Release

> TODO: Release notes eklenecek.

---

## 22 Ocak 2026

### v4.6.0 Release

> TODO: Release notes eklenecek.

---

## 27 Subat 2026

### ? v4.6.1 (2026-02-27)
- **Anti-Bot G?venligi:** Standart Selenium yerine donanimsal d?zeyde Chrome parmak izlerini gizleyen `undetected-chromedriver` k?t?phanesi entegre edildi. Cloudflare ve X bot duvarlari asildi.
- **G?venli Baslangi? (Safe Boot):** Program a?ilir a?ilmaz arka planda tetiklenen X Daemon, Haber Taramasi, Fenerbah?e Mod?l?, DeepScan ve Trend Etkilesim servislerinin otomatik baslamasi **devre disi birakildi**. T?m mod?ller artik sadece ana ekrandaki "START" butonuna manuel basildiginda g?venle ?alismaya baslar.
- **Iyilestirme:** `MainForm.cs`, `OperationManager.cs`, `x_daemon.py` ve `social_intel.py` dosyalarinda anti-bot senkronizasyonlari tamamlandi.cek.

---

## 27 Subat 2026

### v4.6.4 (2026-02-27) - Twitter Threading & Session Fix
- **Fix:** `MainForm.cs` ve `OperationEngine.cs` ?zerindeki kapanis raporu (Market Close) d?ng?s? d?zeltildi. Artik raporlar ayri ayri tweetler yerine tek bir zincir (thread) olarak paylasilacak.
- **Iyilestirme:** `x_daemon.py` ?zerindeki zincirleme mantigi g??lendirildi. ?oklu buton bulma ve "Add another post" butonu dogrulama adimlari eklendi.
- **Bug Fix:** Cookie y?kleme sonrasi yasanan giris ekranina y?nlenme problemi, `refresh()` yerine dogrudan home navigasyonu (`x.com/home`) yapilarak ??z?ld?.
- **Teknik:** `update-version.ps1` kullanilarak t?m proje dosyalari ve manifest v4.6.4 s?r?m?ne y?kseltildi.

---

## 27 Subat 2026

### v4.6.5 (2026-02-27) - Hotfix: JSON Parsing Error
- **Fix:** Influencer search sirasinda olusan "'L' is an invalid start of a value" hatasi giderildi.
- **Detay:** `social_intel.py` i?indeki `log_debug` fonksiyonunda bulunan hardcoded (`d:\Projects`) log yolu ve bu yol hata verdiginde `stdout`'a basilan "Log error" mesaji temizlendi.
- **Iyilestirme:** T?m debug loglari `APPDATA_DIR` altina tasindi ve Python `print` ?iktilari JSON akisini bozmamasi i?in `stderr`'e y?nlendirildi.
- **B?y?k Fix:** v4.6.4 ile gelen thread ve cookie senkronizasyonu d?zeltmeleri korunarak sistem stabilize edildi.

---

## 27 Subat 2026

### v4.6.6 (2026-02-27) - Hotfix: News Threading Stability
- **Fix:** Haber paylasimi sirasinda olusan "Durum ?" ve metin kesilmesi (truncation) sorunu giderildi.
- **Iyilestirme:** `x_daemon.py` i?inde `send_keys` yerine JavaScript (`document.execCommand`) tabanli yazim mekanizmasi kullanilarak T?rk karakterlerinin %100 dogru iletilmesi saglandi.
- **AI Stabilitesi:** `GeminiService` promptlari k?seli parantezlerden temizlendi ve model temperature degeri 0.3'e ?ekilerek daha istikrarli ?ikti elde edildi.
- **Emniyet:** `NewsEngine.cs` i?inde separator (`|||`) eksikligine karsi otomatik b?lme (auto-split) korumasi eklendi.

---

## 01 Mart 2026

### v4.6.7 Release
- **Fix:** AI Context Deprivation hatasi ??z?ld?. Haber ?zetlerinin eksik g?nderilmesi engellendi.
- **Fix:** "Durum KRITIK" kelimesinin Gemini Safety (G?venlik/Sans?r) filtrelerine takilip metni 126. karakterde kesmesi sorunu; prompt'un bastan yazilmasiyla ??z?ld?.
- **Tone Refinement:** Teknik analiz promptlarindaki asiri samimi ("Tevfik Hoca", "Efelerin Efesi") persona kaldirilarak daha profesyonel bir finansal analist tonuna ge?ildi.

---

## 01 Mart 2026

### v4.6.8 Release (Hotfix)
- **Kritik Fix (AI Truncation):** Haber analizleri sirasinda AI'in metni kelime ortasinda (?rn: "seff-", "y-") aniden kesmesi sorununun k?k nedeni tespit edildi ve ??z?ld?. 
- **Detay:** Gemini'nin yerlesik G?venlik Filtreleri (Safety Filters), "suikast", "kapatilacak" gibi kelimeleri `HARM_CATEGORY_DANGEROUS_CONTENT` olarak algilayip metin ?retimini anlik olarak durduruyordu. C# tarafinda bu filtreler API istegine `BLOCK_NONE` parametresi eklenerek tamamen devre disi birakildi. Artik hassas kelimeler i?eren finansal/siyasi haberler kesintisiz analiz edilecek.

---

## 01 Mart 2026

### v4.6.9 Release

> TODO: Release notes eklenecek.

---

## 01 Mart 2026

### v4.6.10 Release

> TODO: Release notes eklenecek.

---

## 02 Mart 2026

### v4.6.11 Release

> TODO: Release notes eklenecek.

---

## 02 Mart 2026

### v4.6.12 Release

> TODO: Release notes eklenecek.

---

## 02 Mart 2026

### v4.6.13 Release

> TODO: Release notes eklenecek.

---

## 03 Mart 2026

### v4.6.14 Release

> TODO: Release notes eklenecek.

---

## 04 Mart 2026

### v4.6.15 Release - Sinyal ve Rapor Görünürlük Çözümü

**Değişiklikler:**
- **Merkezi X Daemon Entegrasyonu:** Sabah motivasyonu ve akşam raporları artık ana `x_daemon` üzerinden, tarayıcı çakışmaları engellenerek tek bir kararlı oturumla paylaşılıyor.
- **Sağlam Başarı Doğrulaması:** Tekil tweet gönderimlerinde (raporlar gibi), tweet kutusu kapandıktan sonra X'in hata mesajları (toast) taranıyor ve gerçek başarı teyit ediliyor.
- **isCritical Throttle Desteği:** Raporlar "kritik" olarak işaretlendi. Bu sayede global 3 dakikalık hız sınırı (antispam throttle), bu önemli paylaşımlar için 1 dakikaya inerek gecikmeleri önlüyor.
- **X UI Uyumluluğu:** Paylaşım kutusundaki buton algılama sorunları için çoklu CSS seçici desteği ve JavaScript tabanlı tıklama mekanizması eklendi.


