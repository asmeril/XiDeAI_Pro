# 🤖 XiDeAI Pro - Proje Geliştirme Günlüğü

Bu günlük, proje üzerinde yapılan değişiklikleri, mimari kararları ve günlük ilerlemeyi takip etmek için tutulmaktadır.


## 📅 31 Mayıs 2026

### 🔧 v5.1.1 — iDeal Canlı Veri + Fenomen Thread + Robot Derleme Düzeltmeleri

**iDeal Robot → XiDeAI Entegrasyonu:**
- `Robot_XU100_Nabiz_Monitor.txt` yeni robot: 5 dk'da bir XU100/XU030/XU050 izler, `Market_Status.txt` ve `Market_Pulse_Alarm.txt` dosyalarını günceller.
- `MainForm.RefreshTrendsAsync`: `Market_Status.txt` okunarak `[XU100_CANLI_VERI]` hard data ile Twitter trendleri birleştirildi.
- `MainForm.PostMarketCloseSummary`: `Market_Pulse_Alarm.txt` pulse alarmları EOD thread zincirine besleniyor.

**Fenomen Thread Formatı:**
- `PromptManager.GetMarketClosePrompt`: Tek tweet → 6-7 tweet fenomen thread (Hook/Endeksler/Yıldızlar/Kazazedeler/Pulse/Yarına bakış/CTA).
- `GeminiService.GenerateMarketCloseTableTweet`: `pulseAnomalies` parametresi eklendi.
- Tüm prompt `### GÖREV` bloklarına X Algoritma Fenomen Kuralları enjekte edildi (Hook/Format/ELI5/CTA).
- Contrarian Filter: Hard data vs sosyal algı zıtlığı Smart Money tuzağı olarak yorumlanıyor.

**Robot Derleme Hataları (iDeal CSharpCodeProvider C#5 uyumu):**
- `Robot_Alpha_Scanner`: CS1056 — `$"..."` interpolation → `+` birleştirme.
- `Robot_PreMove_Scanner`: CS1056 — `bugunYukarı` identifier → `bugunYukari`; CS1012 — `new string('─',90)` → `new string('-',90)`.

---

## 📅 29 Mayıs 2026

### 🔧 v5.0.1 - Thread Reply Düzeltmesi (XHive compose/post Fallback)

**Sorun:**
- `_post_reply_in_thread`: Reply butonu `aria-disabled="true"` kalıyordu.
- Root cause: `fill()` metodu React state'ini tetiklemiyordu → post butonu enabled olmuyordu.
- Hata mesajı: "Thread part 2 failed: Reply button disabled"

**Çözüm:**
- **XHive yaklaşımı benimsendi:** `C:\Users\asmeril\AppData\Local\XHive\worker\x_daemon.py` incelendi.
- **`compose/post?in_reply_to={tweet_id}` URL fallback:** Reply butonu yerine compose URL doğrudan açılıyor.
- **`fill()` → başarısızsa `type(delay=20)` dual-write:** React state tetiklemek için klavye simülasyonu.
- **10x0.5s post butonu retry döngüsü:** Buton gecikmeli enabled olsa bile yakalar.

**Değişen Dosyalar:**
- `Scripts/playwright_daemon.py` (`_post_reply_in_thread` tamamen yeniden yazıldı)
- `XiDeAI_Pro.csproj` (Version: 5.0.1)
- `setup.iss` (MyAppVersion: 5.0.1)



**Sorunlar:**
- LM Studio'da Qwen3.6-27B modeli `budget_tokens=1024` parametresini tamamen görmezden geliyordu.
- Model tüm token bütçesini (`reasoning_tokens: 4358`) chain-of-thought düşünmeye harcıyor, `content` alanını boş bırakıyordu.
- Vision isteği 300 saniyede timeout alıyordu — analiz hiç tamamlanamıyordu.

**Çözümler:**
- **`/no_think` Prefix:** `LMStudioProvider.cs` — hem metin hem vision isteklerinde prompt başına `/no_think\n` eklendi. Qwen3'ün reasoning modunu tamamen kapatan resmi token.
- **`thinking` parametresi kaldırıldı:** LM Studio `budget_tokens`'ı zaten yok sayıyordu, gereksiz parametre temizlendi.
- **Vision timeout 600s'e çıkarıldı:** 300s → 600s (Qwen3 vision analizi uzun sürüyor).
- **Metin timeout 300s'e çıkarıldı:** 180s → 300s.
- **`reasoning_content` fallback:** `ExtractContentFromChoice()` — `content` boşsa `reasoning_content`'i döndürür.

**Değişen Dosyalar:**
- `Services/AI/LMStudioProvider.cs`
- `XiDeAI_Pro.csproj` (Version: 5.0.0)
- `setup.iss` (MyAppVersion: 5.0.0)

---

## 📅 29 Mayıs 2026

### 🚀 v4.10.9 - PublishSingleFile Bug Fix & Reasoning Token Starvation

**Sorunlar:**
- `release.ps1` içindeki `[xml]` parser csproj'u işlerken `PublishSingleFile=true` satırını sessizce siliyordu.
- Setup boyutu 64MB yerine 49MB çıkıyordu (single-file compile olmuyordu).
- `reasoning_content` fallback yoktu — Qwen3 tüm token'ı düşünmeye harcadığında `content` boş dönüyor, "empty vision response" hatası oluşuyordu.

**Çözümler:**
- **`release.ps1` fix:** `[xml]` parser → `[System.IO.File]::ReadAllText` + string-replace. PublishSingleFile artık asla silinmiyor.
- **`reasoning_content` fallback eklendi:** `LMStudioProvider.ExtractContentFromChoice()`.
- **`max_tokens=8192`:** Vision isteklerinde minimum 8192 token garanti altına alındı.
- **`budget_tokens=1024`:** Reasoning bütçesi sınırlandırıldı (LM Studio bunu yok sayıyor — v5.0.0'da `/no_think` ile aşıldı).

**Değişen Dosyalar:**
- `release.ps1`
- `Services/AI/LMStudioProvider.cs`

---

## 📅 29 Mayıs 2026

### 🚀 v4.10.8 - Manuel Analiz Yerel Model Optimizasyonu

**Sorunlar:**
- Yerel model (LM Studio) ile analiz yaparken `IndicatorExtractor` çağrısı ekstra token harcıyor, yanıt süresini uzatıyordu.
- Kısa thread üretiminde grafik ekran görüntüsü gereksiz yere tekrar gönderiliyordu.
- Thread parçaları arasında çok kısa/boş maddeler tweet zincirini bozuyordu.
- Derin analiz prompt'unda grafik okuma talimatı yoktu — model grafiği yorumlamıyordu.

**Çözümler:**
- **`ManualAnalysisService.cs`:** Yerel model aktifken (`usingLocalModel`) `IndicatorExtractor` atlanır; kısa thread için ekran görüntüsü tekrar gönderilmez; indicator context yerine ana analiz metni kullanılır.
- **`ThreadService.cs`:** `.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5)` filtresi eklendi.
- **`PromptManager.cs`:** Derin analiz prompt'una `### GÖRSEL OKUMA (GRAFİK)` bölümü eklendi.

**Değişen Dosyalar:**
- `Services/ManualAnalysisService.cs`
- `Services/ThreadService.cs`
- `Services/PromptManager.cs`

---

## 📅 31 Mart 2026

### 🚀 v4.9.6 - X-Hive Playwright Engine & Anti-Hallucination Guardrail

**Sorunlar:**
- `x_daemon.py` Selenium tabanlı olduğu için Thread gönderimlerinde kararsızlık, "Post Click Exception" ve yanlış timeout hataları üretiyordu.
- NewsEngine, "Deprem/Terör" gibi trajik haberlere AI'nin yüksek skor (10/10) vermesi sebebiyle, kullanıcı onayı sormadan otomatik (Auto-Post) paylaşım yapabiliyordu.
- Tırnak içi emojiler veya linkler 280 karakter limitine ulaşırken C# kaynaklı yanlış parçalanma yaşanıyordu.

**Çözümler:**
- **Yeni Playwright Daemon:** X-Hive projesindeki Playwright kullanan süper-kararlı bot motoru sisteme `playwright_daemon.py` olarak eklendi.
- **Akıllı Karakter Tespiti:** Yeni script X.com standardında (link 23 karakter, emoji 2 karakter vb.) sayım yaparak kusursuz `🧵 1/x` bölen algoritmayı hayata geçirdi. `SocialIntelService.cs`'deki C# kaba bölme iptal edildi.
- **Anti-Halüsinasyon (Güvenlik Duvarı):** `NewsEngine.cs` üzerine afet, terör gibi anahtar kelimeleri tespit edince skoru 8'e düşüren ve otomatik yayını kesin olarak engelleyip havuza atan "Safety Intercept" eklendi.

**Değişen Dosyalar:**
- `Scripts/playwright_daemon.py` (YENİ EKLENDİ)
- `Services/SocialIntelService.cs`
- `Services/NewsEngine.cs`

---

## 📅 30 Mart 2026

### 🚀 v4.9.5 - Thread Kalite & Etkileşim Optimizasyonu

**Sorunlar:**
- Thread'ler 280 karakter limitini akıllıca kullanmıyordu; tek cümlelik, kısa tweet'ler oluşuyordu.
- Endeks analizlerinde (XU100, XU030 vb.) fiyatın yanında gereksiz "TL" etiketi görünüyordu.
- AI üretilen thread'lerde fenomen/influencer etiketleri tutarsız ve çoğunlukla yoktu.

**Çözümler:**
- `Services/PromptManager.cs`: `GetShortThreadPromptWithHistory` (Manuel Analiz) ve tüm sinyal strateji prompt'ları (KING, BOMBA, TEFO, ANKA, DİP, ZİRVE) güncellendi:
  - Her tweet için **240-278 karakter** zorunluluğu ve **min. 3 tam cümle** kuralı eklendi.
  - **3. tweet'te fenomen @etiketleme zorunlu** hale getirildi — cümle içine doğal entegrasyon şart.
  - Eski çelişen kural blokları temizlendi, tek tutarlı kural seti oluşturuldu.
- `Services/ThreadService.cs`: `GetCurrencyForSymbol` güncellendi:
  - BIST endeksleri (`XU*`, `XB*`, `XI*` vb.) artık "TL" değil **"Puan"** etiketiyle gösteriliyor.
  - Para birimi boş döndüğünde fiyat formatlama doğru çalışıyor (`currencyLabel` fallback).

**Değişen Dosyalar:**
- `Services/PromptManager.cs`
- `Services/ThreadService.cs`

---

## 📅 30 Mart 2026

### 🚀 v4.9.4 - Incorrect Thread Reply & URL Extraction Fix (CRITICAL)

**Sorunlar:**
- Thread 2. ve sonraki tweetlerinde bot, kendi parent tweet'inin URL'sini yakalayamadığında `x.com/home` adresine fallback yapıyordu.
- Home timeline üzerinde bulduğu ilk "Reply" butonuna basarak yanlışlıkla başka kullanıcıların tweet'lerine yanıt veriyordu.

**Çözümler:**
- `x_daemon.py`: `_post_single_tweet` içerisindeki tehlikeli `x.com/home` fallback'i tamamen kaldırıldı.
- **Yöntem 1 (Toast):** Tweet atıldıktan hemen sonra DOM'daki "Görüntüle" (Toast) linkini yakalama mantığı eklendi.
- **Yöntem 2 (Retry Profile):** URL ilk denemede alınamazsa, Profil sayfasında **6 kez retry** (toplam ~12 sn) ile yeni tweet'in belirmesi beklenir.
- **Güvenlik Kapısı:** `cmd_post_thread` içerisine `/status/` kontrolü eklendi. Geçersiz veya eksik URL durumunda thread güvenli biçimde durdurulur (başka birine yanıt verilmesi imkansız hale getirildi).

**Değişen Dosyalar:**
- `Scripts/x_daemon.py` (v4.9.4)

---



## 📅 23 Mart 2026

### 🚀 v4.9.3 - Thread Media Fix, Lock Timeout, Publish Pipeline

**Sorunlar (Log Analizi Sonucu):**
- Thread 1. tweet'e grafik görseli (`media_path`) hiç eklenmiyordu. `_post_one` ve `_post_single_tweet` fonksiyonları `media_path` parametresi almıyordu.
- `x_session.lock` timeout 180s: 4 tweet × ~60s = 240s+ gerektiriyor, thread yarıda timeout ile kesiliyor.
- `copy-publish-assets.ps1` kurulu dizine doğrudan dosya kopyalama yapıyordu — her publish sonrası eksik dosya bırakıyordu.
- Publish pipeline: `csproj PostPublish` hedefi `copy-publish-assets.ps1`'i yanlış parametrelerle çağırıyordu.

**Çözümler:**
- `x_daemon.py`: `_post_single_tweet(media_path=None)` parametresi + medya yükleme bloğu + `cmd_post_thread` 1. tweet'e `media_path` geçirme.
- `social_intel.py`: `_post_one(media_path=None)` parametresi + medya yükleme bloğu + 1. tweet çağırısı güncellendi.
- `lock_manager.py`: `acquire_lock` timeout 180s → 360s.
- `SocialIntelService.cs`: `RunPythonScript` timeout 180s → 360s.
- `PromptManager.cs`: Thread prompt'a “EN AZ 3 cümle, tek cümle YASAK” kuralı eklendi.
- `csproj PostPublish`: `CopyToOutputDirectory: PreserveNewest` mekanizması kullanılıyor, `copy-publish-assets.ps1` çağrısı kaldırıldı.
- `copy-publish-assets.ps1`: Kurulu dizine doğrudan kopyalama bloğu tamamen kaldırıldı. Tek doğru akış: `dotnet publish → ISCC → kurulum`.

**Değişen Dosyalar:**
- `Scripts/x_daemon.py`
- `Scripts/social_intel.py`
- `Scripts/lock_manager.py`
- `Services/SocialIntelService.cs`
- `Services/PromptManager.cs`
- `XiDeAI_Pro.csproj`
- `copy-publish-assets.ps1`

---



### 🚀 v4.9.0 - Thread Posting Engine Tam Yeniden Yazımı (MAJOR FIX)

**Problem (Log Analizi Sonucu):**
- `x_daemon.log` incelemesinde **112 adet** `Post Click Exception: Message:` (boş mesaj) tespit edildi.
- Tüm tweet parçaları başarıyla yazılıp doğrulanmasına rağmen son adımda "Tweet At" butonuna tıklama sürekli başarısız oluyordu.
- `data-testid='addButton'` hiçbir zaman bulunamıyor, add button bulunamadığında kod yine de devam edip yanlış textbox index'ine yazıyordu.
- `driver.get("https://x.com/compose/tweet")` URL'i yeni X'de güvenilmez.

**Çözüm:**
- **`_find_add_button(driver)`** yeni bağımsız fonksiyon: 4 sıralı strateji (data-testid → aria-label tarama → SVG path JS scan → Ctrl+Enter fallback). Her strateji başarısız olursa gerçekten `abort` ediyor.
- **`_click_post_button(driver)`** yeni bağımsız fonksiyon: `tweetButton`, `sendReplies`, `tweetButtonInline` + label tabanlı arama. Butonu **4×3 saniye** bekleyerek React sync'i garantiliyor. JS click başarısız olursa ActionChains devreye giriyor.
- **`cmd_post_thread`** tamamen yeniden yazıldı: SideNav butonuyla compose açma (URL fallback ile), her parça için 3 deneme hakkı, textbox index'i sabit yerine her zaman `[-1]` (son kutu), kapsamlı exception loglama (`type(e).__name__`).
- **Compose açma:** URL yerine önce `SideNav_NewTweet_Button` selector'ı deneniyor.

**Değişen Dosyalar:**
- `Scripts/x_daemon.py` (v4.9.0 - `_find_add_button`, `_click_post_button`, `cmd_post_thread` yeniden yazıldı)

---

## � 18 Mart 2026

### 🚀 v4.7.11 - Thread Creation Flow Fixed (HOTFIX)

**Değişiklikler:**
- **X API / DOM Güncellemesi:** X'in "Aynı hesaptan yeni gönderi ekle" (Plus/Add) butonu üzerinde yaptığı test-id değişiklikleri yüzünden botun thread (zincir tweet) atamaması sorunu çözüldü.
- **Robust Clicking Engine:** `x_daemon.py` içerisine IdealSmartNotifier (`social_intel.py`) projesinde kullanılan, Türkçe "Tweet ekle" etiketlerini ve + (artı) ikonunun SVG yolunu arayan, çok daha agresif kaydırmalı (JS scrollIntoview) ve yedekli tıklama döngüsü eklendi.
- Artık AI 3000 parçalık analiz atsa dahi Twitter kutucuğu başarıyla açılıp doldurulacaktır. İletişim kopmaları önlendi.

---

## 📅 27 Subat 2026

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

---

## 04 Mart 2026

### v4.6.16 Release - Thread Bölme Optimizasyonu (|||)

**Değişiklikler:**
- **Thread Parça Sayısı Sabitlendi:** AI'nın thread'leri gereksiz yere uzatması (8-10 parça) engellendi. Manuel analizlerde tam 4, sinyal analizlerinde tam 3 tweet üretilmesi separatist (`|||`) kurallarıyla garantiye alındı.
- **Dolu Tweet Blokları:** Her bir tweet parçasının 240-270 karakter arası dolu içerikle hazırlanması sağlandı. Böylece "tek cümlelik" kısa tweetlerin önüne geçildi.
- **Sinyal Başlık Senkronizasyonu:** C# tarafından eklenen teknik analiz başlığı ile AI çıktısı arasındaki çakışma giderildi (AI artık kendi başlıklarını üretmiyor).
- **Syntax Fix (Hotfix):** PromptManager içindeki tırnak işareti uyumsuzlukları nedeniyle oluşan derleme hataları giderildi.

---

## 04 Mart 2026

### v4.6.17 Release - Manuel Analiz \u0026 Daemon Fixes

**Değişiklikler:**
- **x_daemon Medya Desteği:** Daemon artık `/post_thread` ve `/post_tweet` üzerinden grafik/medya dosyalarını başarıyla X'e yüklüyor. (Manuel analiz thread'lerindeki grafik sorunu çözüldü).
- **Kesin Başarı Doğrulaması:** Paylaşım sonrası tweet kutusunun kapandığı ve X tarafında hata oluşmadığı daemon düzeyinde 12 saniyelik timeout ile doğrulanıyor.
- **Eksik Endpoint Eklendi:** Tekil paylaşımlar (raporlar vb.) için eksik olan `/post_tweet` handler'ı daemon'a entegre edildi.
- **Buton Robustness:** X'in dinamik butonları için `social_intel.py`'daki gelişmiş XPATH ve SVG seçicileri daemon'a port edildi.

---

## 04 Mart 2026

### v4.6.18 Release

**Değişiklikler:**
- **Derin Analiz \u0026 Viral Kanca:** Haber thread promptu Gemini üzerinden 3-4 parçalık formata geçirildi. Haber sadece aktarılmıyor, Jeopolitik/Toplumsal/Ekonomik (Second-Order Effect) domino etkisi analiz ediliyor. İlk tweet "okuyucuyu durduracak" kanca (hook) yapısına büründü.
- **Akıllı Etiketleme (Auto-Mention):** Haber içinde ve analiz süreçlerinde adı geçen şirket, kurum ve kişilerin (@TCMB, @elonmusk vb.) resmi X hesapları yapay zeka tarafından tahmin edilip analiz içine doğalca entegre edilmeye başlandı. (Bkz: Smart Tagging)
- **FENOMEN RADARI (Dost Meclisi):** Manuel ve Sinyal Analizlerinde veritabanındaki fenomenlerin öngörüleri artık @mention şeklinde, "duayen persona" jargonuyla tweet akışına ekleniyor.
- **Visual Hooks (Görsel Kanca):** X algoritmasının medyalı paylaşımları %300 daha fazla desteklediği gerçeğiyle `NewsTrackerService` üzerinden haberlerin orijinal kapak görselleri URL üzerinden indirilip, `NewsEngine` aracı ile X Daemon'a ilk tweet grafiği olarak aktarılmaya başlandı. Python (`x_daemon.py`) tweet url'lerinden görsel okuma işlevine kavuştu.
- **Semantik X Keşfet Optimizasyonu:** Promptların tamamına "Borsa, Teknik Analiz, Ekonomi, Jeopolitik" gibi algoritma dostu semantik anahtar kelimelerin hashtag kullanılmadan cümle içine yedirilmesi kuralı eklendi.
- **Jeopolitik Kritik Filtre:** Spam korumasına takılan "Füze, Savaş, MSB" odaklı yüksek etkili haberler için özel geçiş izni (`IsHighValueNews`) tasarlandı.

---

## 05 Mart 2026

### v4.6.19 Release

**Değişiklikler:**
- **Gerçek Zamanlı Piyasa Verileri (yfinance Entegrasyonu):** `social_intel.py` içerisine `get_financial_summary` metodu eklendi; kapanış tabloları için kullanılan BIST100, USD/TRY ve Gram Altın mock verileri yerine anlık ve canlı fiyatlama çekilmeye başlandı. C# `OperationEngine`'in mock veriye düşme senaryosu düzeltildi.
- **Güvenli Akıllı Etiketleme (Safe Auto-Mention):** YZ haber promptu (`GeminiService.cs`), siyasetçi, bakan, parti lideri veya tartışmalı figürleri otomatik etiketlemeyi kısıtlayacak şekilde katı kurallarla güncellendi. Mention sadece TCMB, Aselsan, SSB gibi temiz ve resmi hesaplara daraltıldı.

---

## 05 Mart 2026

### v4.6.20 Release

**Değişiklikler:**
- **Onay Sekmesi Gece Koruması Düzeltildi:** Haberler için Sessiz Saatler (SpamProtection) filtresi, onaya düşen (7-9 puanlık) haberleri artık tamamen yok etmiyor. Gece gelen onaylı haberler uygulamanın "Onay Bekleyenler" havuzunda biriktirilip kullanıcının onayını bekliyor.
- **10 Puan Kuralı Katılaştırma (Makro Filtresi):** Yapay zekanın "Son Dakika" haberlerine bol keseden 10 puan (otomatik paylaşım hakkı) verip filtreleri delmesi engellendi. SADECE ülke veya dünya çapında şok etkisi yaratan (Savaş, Lider suikasti/istifası, Pandemi, Makro Afet vb.) olaylara 10 puan verebilecek şekilde AI promptu daraltıldı.

---

## 05 Mart 2026

### v4.7.0 Release

**Değişiklikler:**
- **Haber Modülü Bağımsızlığı (Auto-Start Çözümü):** Ana tarayıcı (`btnStart`) başlatıldığında izinsiz tetiklenen haber modülünün çalışması engellendi. Artık haber modülü tamamen bağımsız ve sadece kullanıcı isterse çalışır.
- **Kaliteli ve Küresel Haber Kaynakları (X İptali):** Twitter'da (X) dolaşan kalitesiz ve asılsız "haberlerin" YZ'yi yormasını durdurmak için "FetchXNews" (Twitter İstihbaratı) fonksiyonu tamamen iptal edildi.
- **Doğu Bloku ve Batı Ajansları:** Mevcut RSS havuzuna *Japonya (Kyodo), Çin (Xinhua), Rusya (TASS)* gibi ülkelerin devlete bağlı makroekonomik istihbarat ajansları; Batı'dan ise *WSJ (Wall Street Journal), BBC News* ile yerli lider *AA/TRT* dahil edildi.
- **Resmi Gazete Radarı:** Türkiye pazarındaki stratejik yasa/atama kararlarını gece yarısı yayınlandığı saniyede yakalamak için Google News tabanlı özel bir "Resmi Gazete" filtre bloğu eklendi.

---

## 06 Mart 2026

### v4.7.1 Release

> TODO: Release notes eklenecek.

---

## 06 Mart 2026

### v4.7.2 Release

**Degisiklikler:**
- **Mukerrer Haber Onleme Guclendirildi:** NewsTrackerService.cs icerisindeki duplication kontrolu 3 katmanli hale getirildi. (1) URL link eslesmesi. (2) Normalize edilmis tam baslik eslesmesi (noktalama + buyuk/kucuk harf duyarsiz). (3) Token-tabanli fuzzy match: Farkli kaynaklarin ayni haberin %70+ kelime ortusumu varsa mukerrer sayilir ve atlanir.
- **Basliklar Kalici Kaydediliyor:** _seenTitles listesi artik 
ews_seen_titles.json dosyasina kaydedilerek uygulama yeniden baslatildiginda da haber gecmisi korunuyor.

---

## 06 Mart 2026

### v4.7.7 Release

> TODO: Release notes eklenecek.

---

## 09 Mart 2026

### v4.7.8 Release

> TODO: Release notes eklenecek.

---

## 09 Mart 2026

### v4.7.9 Release

> TODO: Release notes eklenecek.

---

## 09 Mart 2026

### v4.7.10 Release

> TODO: Release notes eklenecek.

---

## 18 Mart 2026

### v4.7.11 Release

> TODO: Release notes eklenecek.

---

## 📅 18 Mart 2026

### 🚀 v4.8.0 Release

> TODO: Release notes eklenecek.

---

## 📅 19 Mart 2026

### 🚀 v4.8.1 - ChromeDriver Native Crash Fix & Chrome 146+ Compatibility

**Değişiklikler:**
- **Chrome 145+ Native Crash Fix:** Twitter gönderiminde `document.execCommand('insertText')` kullanımından kaynaklı native ChromeDriver çökmesi giderildi. `social_intel.py` ve `x_daemon.py` üzerindeki yazım yöntemi, kilitlenmeye yol açmayan **DOM Node Insertion (InputEvent + TextNode)** ve **Pano (Clipboard)** yöntemleriyle değiştirildi.
- **Otomatik Chrome Sürümü Algılama:** `x_daemon.py` ve `social_intel.py` artık sistemdeki Chrome sürümünü Windows Registry üzerinden otomatik algılıyor. Hardcoded `version_main=145` kısıtı kaldırılarak Chrome 146+ sürümleriyle tam uyum sağlandı.
- **Daemon Crash Recovery:** `SocialIntelService.cs` tarafında daemon'a istek gönderilmeden önce sağlık kontrolü (health check) eklendi. Daemon çökmüşse C# tarafı bunu algılayıp otomatik olarak yeniden başlatıyor.
- **Metrik & Log Düzenlemeleri:** `social_intel.py` üzerindeki duplicate print satırları temizlendi ve yazım yöntemleri `js_native` olarak isimlendirildi.

---

---

## 20 Mart 2026

### v4.8.2 Release

> TODO: Release notes eklenecek.

---

## 22 Mart 2026

### v4.9.0 Release

> TODO: Release notes eklenecek.

---

## 22 Mart 2026

### v4.9.1 Release

> TODO: Release notes eklenecek.

---

## 23 Mart 2026

### v4.9.3 Release

> TODO: Release notes eklenecek.

---

## 30 Mart 2026

### v4.9.5 Release

> TODO: Release notes eklenecek.

---

## 01 Nisan 2026

### v4.9.7 Release

> **Odak:** X (Twitter) gönderimlerinde karşılaşılan çerez (cookie) yolu hatasının giderilmesi ve Playwright entegrasyonunun kararlı hale getirilmesi.

**Değişiklikler:**
- **Cookie Path Düzeltmesi:** `playwright_daemon.py` üzerindeki `COOKIES_FILE` yerel yolu, uygulamanın dinamik `%LOCALAPPDATA%\XiDeAI` veya aranan fallback dizinleri `_discover_appdata()` fonksiyonu kullanılarak düzeltildi.
- **Cookie Load Optimizasyonu:** `x_cookies.json` beklentisi yerine, WebView2 ve önceki sistemlerle tam uyumlu olarak önce `twitter_cookies.json` taranacak; bulunamazsa `twitter_cookies.pkl` (eski format) okunarak Playwright context içine aktarılacaktır.

---

## 01 Nisan 2026

### 🚀 v4.9.8 - Viral Thread Koruması & Etkileşim Optimizasyonu
**Sorunlar:**
- AI tarafından `||| Tweet 1` vb. başlıklarla üretilen yanıtlar C# filtremesinde silinince geriye sadece boş bir string (empty string) kalıp X'e yollanıyor ve ilk tweetler metinsiz/hayalet olarak paylaşılan resimlere dönüyordu.
- Python X-Hive motoru parçalanmış gelen `tweets` dizisini tek bir uzun metne dönüştürüp `(\n\n.join)` yeniden X harf limitlerine göre körlemesine biliyor; bu da hashtag, hook, etkileşim sıralamalarını çökertip "tek cümlelik" yapay tweetler yaratıyordu.
**Çözümler:**
- `ThreadService.cs`: Filtreme işleminden sonra kalan "boş" stringler silindi ve diziye eklenmeleri önlendi. Zayıf (80 kar. altı) kalan "yatırım tavsiyesi değildir" gibi tekil cümleler bir önceki ana cümlenin sonuna yapıştırılacak şekilde düzenlendi.
- `playwright_daemon.py`: C#'tan gelen AI'ın onaylanmış ve ustaca örülmüş parça yapısı ASLA birleştirilmeden doğrudan kullanıldı. Sadece bir parça olağanüstü durumlarda 265'i geçerse bölünecek şekilde X-Hive engeli pasifize edildi.

---

## 02 Nisan 2026

### v4.9.9 Release

> TODO: Release notes eklenecek.

---

## 07 Nisan 2026

### 🔧 v4.10.2 - LM Studio Vision Fix (4K DPI)

**Sorun:**
- 4K monitörde 4x DPI ölçeğinde alınan ekran görüntüleri (2560×1440 fiziksel → 10240×5760 mantıksal px) LM Studio'ya gönderildiğinde `"Invalid image at index 0"` hatasına yol açıyordu.
- `detail` alanı v5.0.1'de kaldırılmış olmasına rağmen görüntü boyutu sorun olmaya devam ediyordu.

**Çözüm:**
- `LMStudioProvider.cs`: `PrepareImageForVision(imagePath, maxDimension = 1024)` adlı yeni yardımcı metot eklendi.
  - `System.Drawing` (net8.0-windows / WinForms yerleşik) kullanılarak görüntü yüklenir ve en-boy oranı korunarak max 1024px'e ölçeklenir.
  - `HighQualityBicubic` interpolasyon ile yeniden örneklenir ve JPEG %85 kalitesinde kodlanır (~50–150 KB).
  - Gönderim öncesinde `"📷 Görsel hazırlandı: XXkB JPEG → LM Studio"` logu yazılır.
- Artık tüm görsel isteklerde ham PNG yerine her zaman küçültülmüş JPEG gönderilir.
- Harici paket gerektirmez; `net8.0-windows` + `UseWindowsForms=true` yeterlidir.

**Değişen Dosyalar:**
- `Services/AI/LMStudioProvider.cs`

---

## 07 Nisan 2026

### 🔧 v4.10.3 - Twitter Charset & News Performance Fix

**Sorunlar:**
- Twitter gönderimlerinde TÃ¼rkÃ§e karakterlerin (Ã§, ÄŸ, Ä±, Ã¶, Ã¼, ÅŸ) bazÄ± sistemlerde `charmap` hatasÄ± vermesi veya hatalÄ± gÃ¶rÃ¼nmesi.
- Haber takip modÃ¼lÃ¼nÃ¼n bÃ¼yÃ¼k veri setlerinde AI iÅŸleme sÄ±rasÄ±nda arayÃ¼zÃ¼ yavaÅŸlatmasÄ±.

**Ã‡Ã¶zÃ¼mler:**
- **UTF-8 Enforcing:** `x_daemon.py` ve `SocialIntelService.cs` katmanlarÄ±nda tÃ¼m iletiÅŸim yollarÄ± (`StandardInput`, `StandardOutput`) kesin olarak UTF-8 moduna geÃ§irildi. C# tarafÄ±nda `ProcessStartInfo.StandardOutputEncoding` ayarlandÄ±.
- **News Engine Optimization:** `NewsTrackerService` iÃ§erisindeki polling mekanizmasÄ± optimize edildi, UI thread bloklanmasÄ± Ã¶nlendi.

**DeÄŸiÅŸen Dosyalar:**
- `Scripts/x_daemon.py`
- `Services/SocialIntelService.cs`
- `Services/NewsTrackerService.cs`

---

## 30 Mayıs 2026

### v5.1.0 Release

**Sinyal Modülü Tam Yeniden Yapılandırması**

* **Kaldırıldı:** Eski tarama robotları (King, Bomba, TeFo, ANKA, DIP, ZIRVE, Miner) için tüm altyapı — klasör izleme, parser metodları, UI checkbox'ları, ConfigManager alanları.
* **`LogFileWatcher`:** Klasör tabanlı `FileSystemWatcher` listesi tamamen kaldırıldı. `Start()` artık parametresiz; yalnızca `C:\iDeal\Sinyal_Log_Database.txt` tail-izleme yapıyor.
* **`SignalParser`:** `ParseKingFormat`, `ParseDipZirveFormat`, `ParseAnkaFormat` kaldırıldı. `SignalData` modelinden `Score/MaxScore/FinalScore/StrategyBonus/PeriodBonus/IsCommonScan` kaldırıldı; yerine `Durum` (AKTIF/PULLBACK_ADAY) ve `IsRoket` eklendi. Tier artık robotun kendi verdiği duruma göre.
* **`SignalEngine`:** `CheckSignalQualityWithAI` basitleştirildi — DB'ye yazılan her satır robotun eşiğinden zaten geçmiş (Alpha≥90, PreMove≥75); ek skor filtresi kaldırıldı. `OnlyCommonSignals` / `CommonStrategies` bloğu kaldırıldı.
* **`PromptManager`:** `GetStrategySpecificPrompt` artık yalnızca `ALPHA` ve `PREMOVE` dallarına sahip. Her strateji için robotun kendi scoring mantığına uygun özel persona ve bağlam içeren promptlar (`GetAlphaSignalPrompt`, `GetPreMoveSignalPrompt`) eklendi.
* **`ConfigManager`:** `WatchFolder*`, `MinScore*`, `Enable*` (eski robotlar), `Period*`, `OnlyCommonSignals`, `CommonStrategies` kaldırıldı. Yeni: `AlphaOnlyAktif`, `PreMoveOnlyAktif`.
* **`MainForm`:** Sinyal Merkezi sekmesi sadeleştirildi — eski 7 robot checkbox'ı, 4 periyot, ortak tarama seçimi, 3 eşik numericUpDown kaldırıldı; yerine 2 kaynak + 2 durum checkbox'ı eklendi.


