# 🤖 XiDeAI Pro - Proje Geliştirme Günlüğü

Bu günlük, proje üzerinde yapılan değişiklikleri, mimari kararları ve günlük ilerlemeyi takip etmek için tutulmaktadır.

## 📅 19 Ağustos 2026

### 🔧 v5.7.4 Release (WebView2 Re-init Siyah Ekran — Kök Neden Düzeltmesi)

**Kök Neden Analizi:**
v5.7.2'deki `--disable-gpu` fix'i GPU crash frekansını azaltıyordu, ancak `BrowserProcessExited` tetiklenmeye devam ettiğinde çalışan re-initialization kodu yanlış parent container'a WebView2 ekliyordu:

```
tpChart (TabPage)
  └── pnlChartContainer (Panel, Dock=Fill)  ← tüm alanı kaplıyor, üstte
        ├── pnlChartHeader
        └── _webViewChart (orijinal WebView)

ProcessFailed → re-init (HATALI):
tpChart (TabPage)
  ├── pnlChartContainer  ← hâlâ orada, görünür alanın tamamını kaplıyor
  └── _webViewChart (YENİ, tpChart'a eklendi)  ← pnlChartContainer'ın ARKASINDA → SİYAH EKRAN!
```

**Düzeltme (`MainForm.cs`):**
- `_pnlChartContainer` ve `_pnlTwitterContainer` **class field** olarak tanımlandı (önceden sadece `InitializeComponent` içinde yerel `var` değişkeniydi).
- `ProcessFailed` re-init kodu güncellendi:
  - `tpChart.Controls.Remove` / `tpChart.Controls.Add` → `_pnlChartContainer.Controls.Remove` / `_pnlChartContainer.Controls.Add`
  - `tpTwitter.Controls.Remove` / `tpTwitter.Controls.Add` → `_pnlTwitterContainer.Controls.Remove` / `_pnlTwitterContainer.Controls.Add`
- Artık yeni WebView2 doğru parent panel içine ekleniyor ve `SetupManualLayout` bound'ları içinde görünür oluyor.

---

### 🔧 v5.7.3 Release (pip Install Timeout Düzeltmesi)

**Sorun:** `DependencyManager.cs` içinde `EnsurePythonPackageAsync` metodu `WaitForExit(60000)` (60 saniye) ile pip kurulumunu bekliyordu. `undetected-chromedriver` gibi wheel build gerektiren paketler yavaş internet bağlantısında veya ilk kurulumda bu süreyi aşarak `⚠️ yüklenemedi` loglanıp atlanıyordu. Sonraki uygulama çalıştırmalarında `[Daemon Log] ERROR: selenium or undetected_chromedriver not installed` hatası alınıyordu.

**Düzeltme (`DependencyManager.cs`):**
- `WaitForExit(60000)` → `WaitForExit(120000)` — timeout **60s'den 120s'ye** artırıldı.
- `undetected-chromedriver` bu PC'ye manuel olarak elle `pip install` ile yüklendi (yükleme 13 saniye sürdü — ilk seferinde ağ veya cache sorunu nedeniyle timeout'a düşmüştü).

---

### 🔧 v5.7.2 Release (WebView2 GPU Crash — Siyah Ekran Düzeltmesi)

**Kök Neden:** Yeni PC kurulumlarında GPU sürücüsü uyumsuzluğu, WebView2'nin altındaki Chromium render sürecini başlangıçta çökertiyor (`BrowserProcessExited`). Süreç yeniden başlatılıyor ancak GPU donanım hızlandırması yüzünden ekran siyah kalıyordu.

**Düzeltme (`MainForm.cs` — `InitializeChart` + `InitializeTwitterWebView`):**
- `CoreWebView2EnvironmentOptions` nesnesi eklendi: `--disable-gpu --disable-gpu-compositing --disable-software-rasterizer --in-process-gpu`
- Bu bayraklar Chromium'u CPU tabanlı software rendering moduna geçirir; GPU sürücüsü uyumsuzluğunu bypass eder.
- Her iki WebView2 (Chart + Twitter) için aynı fix uygulandı.
- Etkilenen cihazlar: Yeni/farklı PC'ye ilk kurulum, özellikle farklı GPU modeli/sürücüsüne sahip sistemler.

---

### 🔧 v5.7.1 Release (Yeni PC Kurulum & Cookie Hata Düzeltmeleri)

**TradingView Cookie IOException Düzeltmesi (`MainForm.cs`):**
- `btnImportTvCookies.Click` handler'ı `(s, ev) =>` (sync) → `async (s, ev) =>` olarak dönüştürüldü.
- `File.Copy(ofd.FileName, dest, true)` kaldırıldı. Uygulama çalışırken WebView2 chart veya `screenshot.py` tarafından kilitlenen `tradingview_cookies.json` dosyasına `File.Copy` yapılmaya çalışılması `IOException` fırlatıyordu.
- Çözüm: `ReadAllTextAsync` ile kaynak içerik okunduktan sonra `WriteAllTextAsync` ile hedefe yazılıyor. Bu yöntem dosya kilidini bypass eder.
- Hata durumunda kullanıcıya açıklayıcı `MessageBox` gösteriliyor.

**Twitter/X Sosyal Sekmesi Hesap Görüntülememe Düzeltmesi (`MainForm.cs`):**
- `btnImportCookies.Click` handler'ı genişletildi: Import başarılı olduğunda (`res.Success`) `_webViewTwitter` WebView2'ye cookie'lar yeniden enjekte ediliyor (`InjectTwitterCookiesAsync`) ve `https://x.com/home` adresine navigate ediliyor.
- **Kök neden:** Uygulama başlangıcında `InitializeTwitterWebView()` çalışır; yeni bir PC kurulumunda `twitter_cookies.pkl` henüz mevcut olmadığından cookie enjeksiyonu başarısız olur ve WebView2 oturumsuz `x.com/home` gösterir. Kullanıcı sonradan cookie içe aktardığında WebView2 güncellenmiyordu.
- Bu düzeltme ile uygulamayı yeniden başlatmaya gerek kalmadan sosyal sekme hesabı gösterecek.

---

## 📅 30 Temmuz 2026


### 🚀 v5.7.0 Release (Fenomen & Thread Düzeltmeleri)

**Fenomen Analizlerinin Akışa Dahil Edilmesi:**
- `SocialIntelService.cs` — `FetchInfluencerPostsFromPython` içindeki cache mekanizması sembol-aware yapıldı. Önceden bir handle için genel tweet sayısı yeterince büyükse (≥5) canlı X araması atlanıyordu; fakat bu tweetlerin söz konusu sembolle ilgisi kontrol edilmiyordu. Artık cache, `#SEMBOL / $SEMBOL / \bSEMBOL\b` regex ile filtreleniyor, en az 3 eşleşme yoksa canlı X aramasına gidiliyor.
- `ManualAnalysisService.cs` + `SignalEngine.cs` — VIP canlı aramasından **önce** `MemoryEngine.GetKnowledgeBase()` üzerinden sembol bazlı "Adım 0" KB araması eklendi. Deep Scan'de toplanan VIP fenomen tweetleri artık her analizde proaktif olarak kullanılıyor; canlı X araması yalnızca KB'de yeterli veri yoksa fallback olarak devreye giriyor.

**Thread Son Tweet Güvenilirlik Artırımı:**
- `playwright_daemon.py` — Loglardan tespit edilen kalıp: `ManualAnalysisThread (11/12)`, `(7/8)` → her seferinde **son tweet** başarısız. Kök neden: X büyük thread'lerde yavaş yanıt verirken eski timeout değerleri yetersiz kalıyordu.
  - Tweet arası bekleme: `2s → 4s`
  - Reply navigate timeout: `10s (20 döngü) → 18s (36 döngü)`  
  - `Ctrl+Enter` fallback: Yalnızca `_post_single_tweet`'teydi, `_post_reply_in_thread`'e de eklendi
  - `with_replies` profil fallback bekleme: `5s/8s/11s → 8s/12s/16s`
  - Profile fetch settle: `2s → 3s`

---

## 📅 16 Temmuz 2026

### 🚀 v5.7.0 Release

**Prompt Overhaul ve Etkileşim İyileştirmesi:**
- Tüm LLM istemleri (PromptManager) modüler olarak `#region` tagları altında 10 ana modüle ayrıldı.
- `GetAntiClicheGuard()` merkezi metodu ile tüm analizlerden ezberlenmiş kalıplar, klişeler ve "Hürmüz boğazı", "büyük resim", "yatırımcı psikolojisi" gibi sürekli tekrarlanan kelimeler temizlendi.
- `GetVariedVoice()` ve `GetVariedHookDirective()` ile analizlerin giriş cümleleri ve tonlamaları rotasyona bağlandı (Aynı sembol 3 kez analiz edilse bile farklı tonda çıkması sağlandı).
- Haber Analizi (News) modülündeki zorunlu ekonomik analiz yorumu kaldırıldı; her haberin (Spor, Teknoloji, Yaşam, Siyaset, vb.) kendi bağlamında (zorlama finansal eklemeler yapılmadan) analiz edilmesi sağlandı.
- Reply (Etkileşim) promptları baştan yazılarak robotik editör dili yerine "sokak jargonu, esprili ve doğal X kullanıcısı" hissiyatı veren kısa yanıtlar formatına çevrildi.
- Üstat paneli analizinde tablo tipi tanımlama mantığı geliştirilerek, Takas dışı tabloların (örn: HMA) Takas analizine maruz kalma halüsinasyonu giderildi.

## 📅 15 Temmuz 2026

### 🚀 v5.6.7 Release

**Telegram Komut Almama (Polling) Sorunu:**
- `MainForm` başlatılırken Telegram Polling timer'ının `Invoke` metodu ile tetiklenmesi, pencere handle'ı henüz oluşmamışsa `InvalidOperationException` fırlatıp arka plan görevini sessizce çökertiyordu (timer hiç başlamıyordu).
- Hata düzeltildi: `this.IsHandleCreated` kontrolü eklenip `BeginInvoke` veya `HandleCreated` event'ine bağlanarak timer'ın her koşulda sağlıklı bir şekilde UI thread'inde başlatılması sağlandı.

### 🚀 v5.6.6 Release

**Beyaz Pencere Flash Düzeltmesi & Telegram Sorunu:**
- `undetected_chromedriver` kütüphanesi yamalandı. Arka planda açılan komut pencerelerini engellemek için `CREATE_NO_WINDOW` bayrağı eklendi.
- Zararlı olabilecek genel `Popen` yama blokları `social_intel.py` ve `x_daemon.py` betiklerinden temizlendi.
- XHive tarafında yetki süresi dolan Telethon session'ının sürekli SMS kodu atmasına neden olan `client.start()` kullanımı, daha güvenli olan `client.connect()` metoduna geçirildi ve hata yakalama mekanizması eklendi.
- XHive `.env` üzerinde `TELEGRAM_HUB_ENABLED=true` yapılarak Telegram etkileşimleri geri açıldı.

## 📅 03 Temmuz 2026

### 🔧 v5.6.5 Release

**Üstat Paneli Tweet Limiti Düzeltmesi — Daemon Scroll Desteği:**
- `Scripts/x_daemon.py` içindeki `/timeline` komutu (`cmd_timeline`) güncellendi. Önceden sayfa yüklendiğinde sadece ilk 4-5 tweeti alıyordu (scroll yoktu). Artık istenen `limit` değerine ulaşana kadar sayfa aşağı kaydırılıyor.
- `max_scrolls = min(15, max(3, limit // 3))` formülüyle limit 30 iken 10 scroll yapılıyor.
- Her scroll turunda mevcut `article` elementleri parse edilip URL bazlı deduplikasyon ile sonuç listesine ekleniyor.
- Sonuçlar tarih sırasıyla (yeniden eskiye) sıralanarak ilk `limit` kaydı döndürülüyor.
- Üstat paneli artık `@EFELERiiNEFESi3` gibi bir gün içinde çok tweet atan hesaplarda dünkü tarama tweetlerini kaçırmıyor.

## 📅 29 Haziran 2026

### 🔧 v5.6.4 Release

**Spam Cooldown Hatalarının Giderilmesi, Yerel Veritabanı Önceliği ve Otomatik Temizlik:**
- `MemoryEngine.cs` sınıfına `HasRecentAnalysisPosted` metodu eklendi. Robotun son 4 saat içinde bir sembole analiz paylaşıp paylaşmadığı kontrolü bu metot üzerinden yapılacak şekilde güncellendi. Eski hatalı `Recall` (crawled tweetleri kontrol eden) cooldown kontrolü düzeltildi.
- `MainForm.cs` içindeki `PerformInternalSearchAsync` metodu güncellendi. Arama öncesinde `Recall(symbol, 168)` ile son 1 haftaya ait fenomen tweetlerinin yerelde olup olmadığı kontrol ediliyor. Eğer kayıt varsa canlı X araması (WebView2/Selenium) tamamen atlanıyor ve yereldeki veriler kullanılıyor.
- `MemoryEngine.SaveKnowledgeBase()` metodu güncellendi. Veritabanı kaydedilirken 10 günden eski (7 gün limit + 3 gün güvenlik marjı) fenomen tweetleri otomatik olarak temizlenecek (prune) şekilde optimize edildi.

### 🔧 v5.6.3 Release

**Gemini Haber Görevlerinin TaskType Tercihleriyle Uyumlu Hale Getirilmesi:**
- `GeminiService.cs` içindeki `SendRequest`, `SendGeminiRestApiRequest` ve `SendMultimodalRequest` metotlarına varsayılanı `GeneralAnalysis` olan `taskType` parametresi eklendi.
- `DetectNewsCategory`, `AnalyzeNewsUnified`, `GenerateNewsCategoryAnalysis` ve `AnalyzeNewsForThread` haber metotları güncellenerek istek gönderirken `TaskType.NewsAnalysis` ve `TaskType.NewsThreadGeneration` tiplerini iletmeleri sağlandı.
- Böylece `ModelManager.cs` içindeki v5.6.2 ile tanımlanmış olan Gemini öncelikli haber görev tercihleri aktif hale getirildi; Gemini API başarısız olduğunda yerel modele (LMStudio) fallback yapılması sağlandı.

### 🔧 v5.6.2 Release

**Yapay Zeka Modül İzolasyonu (Gemini & LM Studio):**
- v5.0.0 ile tamamen yerel modele (LM Studio) çekilen sistemde, **Haber Analizi** ve **Haber Thread Üretimi** (NewsEngine) kısımları tekrar `Gemini` (gemini-2.5-flash) kullanımına açıldı.
- `OperationManager.cs` içerisinde `SyncGeminiProviders` fonksiyonu revize edilerek, Config'de Gemini API anahtarı varsa sisteme tekrar entegre edilmesi sağlandı.
- `ModelManager.cs` içerisinde `InitializeTaskPreferences` düzenlenerek; haber dışındaki tüm görevler (DeepScan, Sinyal, Trend vs.) `lm-studio` modeline sabitlenirken, sadece haber tabanlı görevler için birinci öncelik Gemini olacak şekilde (`gemini-2.5-flash`, ardından fallback olarak `lm-studio`) izolasyon sağlandı.


## 📅 01 Haziran 2026

### 🔧 v5.1.9 — AI Zaman Aşımı & Zamanlanmış Görev Hata Yakalama (Stabilizasyon)

**Yapay Zeka Hata ve Kesilme (Timeout) İyileştirmeleri:**
- LMStudioProvider içerisindeki max_tokens değeri 4096'dan 16384'e çıkarıldı.
- HttpClient.Timeout değeri 300s (5 dk) yerine 900s (15 dk) olarak ayarlandı, uzun süren Qwen 3.6 27b reasoning işlemleri güvence altına alındı.

**Kritik Zamanlanmış Görev Çökmelerinin Giderilmesi:**
- MainForm.cs içerisindeki PostMorningMotivation ve PostMarketCloseSummary fonksiyonlarının tamamı kapsamlı 	ry-catch bloklarına sarıldı.
- Yapay zeka boş veya 
ull döndüğünde sessizce çökmek yerine artık System/Twitter loglarına hata mesajı düşürülüyor.
- Motivasyon tweetlerindeki (WebView) log mesajı (Playwright/Daemon) olarak düzeltildi, zira sistem artık asıl paylaşımları X-Hive Engine üzerinden yapıyor.

**Sinyal Analiz Tablosu UI ve Daemon Optimizasyonları:**
- Efe HMA veya kısa metinli, görsel içeren tweetlerin Python Daemon tarafından yoksayılması hatası düzeltildi (x_daemon.py).
- Screenshot alma 	imeout değeri 120s'den 180s'ye çıkarıldı.
- Sinyal tablosundaki yanıltıcı Yayınlandı statüsü, arka planda henüz sadece sıraya alındığını göstermek için İşleme Alındı (Cyan renk) olarak değiştirildi.

---


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

## 02 Haziran 2026

### 🔧 v5.2.0 — Thread Stabilizasyonu ve Katı URL Doğrulama

**Manuel ve Otomatik Thread Stabilizasyonu:**
- `ThreadService.cs` içerisindeki `PostSignalThread` metodu revize edildi.
- Sistemin kendi oluşturduğu sabit 1. tweet (Fiyat/Başlık) ve 4. tweet (Footer) iptal edildi.
- Fiyat ve TradingView linki, doğrudan yapay zekanın ürettiği ilk "Hook" parçasıyla birleştirildi.
- Grafik (Chart Image), birleştirilmiş gerçek 1. tweete eklenerek "Başlık + Hook + Grafik" bütünlüğü sağlandı.

**Prompt ve Sınır Optimizasyonları:**
- `PromptManager.cs` içerisinde `GetAlphaSignalPrompt` ve diğer stratejiler için 1. tweet (Hook) sınırı maksimum 200 karakter olarak kısıtlandı.
- Kalan parçalar 240-278 karakter aralığında korunarak Twitter (X) 280 karakter limitinin aşılması engellendi.

**X-Hive Daemon Katı Doğrulama (Strict Validation):**
- `playwright_daemon.py` içindeki `_post_single_tweet` fonksiyonuna katı doğrulama eklendi.
- Tweet gönderimi (Post) butonuna basıldıktan sonra, hata veren toast mesajları yakalanacak.
- Ekranda Compose (Oluştur) penceresi kapanmadan asılı kalırsa işlem anında sonlandırılacak, eski tweet URL'si kopyalanmayacak.
- Bu sayede thread'lerin yanlışlıkla geçmiş eski bir tweete (Reply) bağlanarak zincir oluşturması engellendi.

---

## 02 Haziran 2026

### 🔧 v5.2.1 — AI Prompt Zehirlenmesi ve React State Uyum Yaması

**Prompt İyileştirmeleri (PromptManager):**
- Haber ve Manuel Analiz (`GetDeepManualAnalysisPrompt`) promptlarındaki `[Tweet 1: ...]` gibi köşeli parantezli yapı zorunlulukları kaldırıldı.
- AI'ın robotik başlıklar atması (örn: `**Tweet 1 - KANCA:**`) engellenerek tamamen fenomen akıcılığına geçildi.
- Manuel Analiz ilk tweet uzunluğu Twitter'ın sınırlarına takılmaması için maksimum 180 karaktere indirildi.

**Güvenlik Filtresi (SanitizeXContent):**
- Sistemin "Tweet 1" içeren **bütün satırı komple silme** hatası giderildi. Artık regex yardımıyla metne dokunmadan sadece istenmeyen etiketler (Tweet 1, KANCA vs.) ayıklanıyor. (İlk tweetin boş çıkma hatası çözüldü).

**Playwright Motoru (React Çakışma Düzeltmesi):**
- `playwright_daemon.py` artık KESİNLİKLE tweet metninden *önce* haber/analiz görselini (image) yüklüyor.
- Metin yazımında, React tabanlı (Draft.js) kutularla tam uyum sağlamak için Javascript `innerText` hilesi bırakılıp `keyboard.insert_text()` klavye simülasyonuna geçildi. Böylece görseller yüklenirken ekranın yenilenip metinleri silmesi problemi ortadan kalktı.

---

## 02 Haziran 2026

### v5.2.2 Release

> TODO: Release notes eklenecek.

---

## 03 Haziran 2026

### v5.2.6 Release

**Thread Gönderim Motoru Yeniden Yazıldı (`playwright_daemon.py`)**

- **`_click_publish` düzeltmesi:** `Escape` tuşu publish öncesinden kaldırıldı. X compose'da Escape "Gönderiyi sil?" modalını açıyor ve tweet gönderilememesine neden oluyordu. Escape artık yalnızca son çare JS click başarısız olursa overlay kaldırmak için kullanılıyor.
- **`keyboard.insert_text()` geçişi:** `compose_box.fill()` React synthetic event'lerini tetiklemediğinden post butonu `aria-disabled=true` kalıyordu. `_post_single_tweet` ve `_post_reply_in_thread` artık `keyboard.insert_text()` kullanıyor.
- **Compose-cleared doğrulaması:** Her iki gönderim fonksiyonunda da `_click_publish` sonrası 10 saniye içinde sayfanın compose URL'den ayrılıp ayrılmadığı kontrol ediliyor. Ayrılmadıysa hata fırlatılıp retry yapılıyor.
- **`_last_known_tweet_id` baseline filtresi:** `_extract_latest_tweet_url` her post öncesi `min_id` alıyor; bu değerden küçük ya da eşit status ID'li URL'ler skip ediliyor. Eski tweetlerin "yeni tweet" olarak raporlanması (false-positive) engellendi.
- **DOM-first URL tespiti (XHive pattern):** Toast bekleme (20s → 0s) kaldırıldı. Önce mevcut sayfanın DOM'u taranarak `/status/` linki arandığından çoğu durumda profil sayfasına gidilmesine gerek kalmıyor.

---

## 03 Haziran 2026

### v5.2.7 Release

**Thread Son Tweet Sorunu Giderildi (`playwright_daemon.py`)**

- **Compose-cleared hatası artık retry kapsıyor:** "Compose box still has text after 10s" `Exception` fırlatıyor ve anında fail döndürüyordu. Artık `PlaywrightTimeoutError` olarak fırlatılıyor — 3 deneme hakkı devreye giriyor.
- **Tüm Exception'lar retry alıyor:** `_post_single_tweet` ve `_post_reply_in_thread`'de genel `except Exception` bloğu artık anında `return error` yerine attempt=3'e kadar retry yapıyor. Bu sayede X'in yavaş tepki verdiği durumlarda (rate limit, geçici gecikme) son tweet kaybolmuyor.


---

## 12 Haziran 2026

### v5.4.7 - v5.4.9 Release

**Takas ve AKD Analizi Entegrasyonu ile RSS Düzeltmeleri**
- **v5.4.7:** Piyasa kapanış (Market Close) senaryosu baştan aşağı revize edildi. iDeal EOD_SNAPSHOT üzerinden artık hacim karşılaştırmaları, XGLD, USDTRY, BRENT ve XSLV gibi global varlık kurları günlük kapanış tablosuna yansıtılıyor. Kompakt thread kalite kontrolü eklendi (40 karakter altı metinler yoksayıldı).
- **v5.4.8:** Haber kaynaklarındaki bozuk RSS yayınları düzeltildi. Anadolu Ajansı, TRT Haber, CNBC ve Kyodo News için URL'ler aktif uçnoktalara güncellendi.
- **v5.4.9:** BIST Takas ve Aracı Kurum Dağılımı (AKD) analizi PromptManager'a eklendi. "Diğer" kuralı, T+2 gecikmesi ve kurumsal/bireysel oranlama mantığı, @matisay67 gibi Takas stratejili üstat taramalarında dinamik olarak LLM'e enjekte edilerek yorum kalitesi profesyonel düzeye çekildi.

---

## 16 Haziran 2026

### v5.5.0 Release

**Mükerrer Sinyallerin Zamana Duyarlı Revizyonu & Fenomen Etiketleme Düzeltmeleri**
- Mükerrer sinyal geldiğinde, eğer önceki analiz 2 günden eskiyse sistem artık kısa geçmiyor. Eski analizi okuyup başarısına göre atıfta bulunarak ("Daha önce belirttiğimiz gibi hedefe ilerliyor") sıfırdan, bağlamlı bir tam analiz üretiyor.
- Analiz 2 günden yeniyse Gemini Multimodal Vision ile anlık grafiğe bakarak tek cümlelik destek/direnç özeti çıkartıyor ve bunu kısa pekiştirme thread'ine ekliyor.
- Yapay zekanın fenomen analizlerini özetlerken kendi uydurduğu "Dost meclisi X-User" gibi hitaplar tamamen engellendi. Artık zorunlu olarak gerçek `@handle` kullanarak doğrudan ilgili fenomenin hesabını etiketliyor.
- Twitter'da Python daemon'ı tarafından bölünen "hayalet 5. tweet" (phantom tweet) hatası, limitten 255 karaktere esneme payı bırakılarak kalıcı olarak çözüldü. Global verilerin (Hacim katı, USD, BRENT vb.) hatalı okunması düzeltildi.

---

## 📅 22 Haziran 2026

### 🔧 v5.5.9 Release

**Telegram Yanıt Geri Bildirimi Düzeltildi:**
- `social_intel.py` ve `x_daemon_current.py` başarılı yanıt gönderimlerinde artık JSON içerisinde `"tweet_url"` döndürüyor.
- Telegram'da "Yanıt gönderildi: [BOŞLUK]" yerine tweet'in gerçek linkinin görünmesi sağlandı.

**x_daemon_current.py Timeout Hatası Çözüldü:**
- Daemon modunda yanıt işlemi, yavaş ve istikrarsız olan `intent/tweet` API'si yerine doğrudan orijinal tweet sayfasına gidilerek Javascript etkileşimleri ile yorum kutusu kullanılarak yapılacak şekilde iyileştirildi.
- Bu sayede `[DAEMON] Reply Hatasi: Message:` (boş Message hatası fırlatan TimeoutException) tamamen giderildi.

**Derleme (Build) Sorunları Giderildi:**
- `MainForm.cs`'de `tpChart` ve `tpTwitter` local değişkenleri class field seviyesine çıkarılarak, `ProcessFailed` lambda blokları içerisinden `tpChart.Controls`'e erişimde çıkan CS0103 hataları ortadan kaldırıldı.

---

## 📅 23 Haziran 2026

### 🔧 v5.6.0 Release

**Thread (Zincir) Metin Kesilme ve Eksik Parça Sorunu Çözüldü:**
- `ThreadPipeline.cs` içerisindeki agresif çalışan "Robotik numara temizliği" regex'i (ör. `1) KISA ÖZET`) iptal edildi. Bu regex'in son parçayı tamamen sildiği ve 5/5 olarak beklenen tweetin boş içerik nedeniyle gönderilememesine (Duplicate/Timeout) yol açtığı tespit edildi.
- `playwright_daemon.py` hata raporlaması geliştirildi: Kısmi başarılı (partially posted) durumlarda hata JSON içerisine `Failed parts` ile birlikte tam hata nedeninin tespiti için daha açık log detayı eklendi.
- `SocialIntelService.cs` içindeki `SocialIntelResult` kullanımı düzeltildi, olmayan `url` özelliği yerine `tweet_url` kullanımı sağlandı ve derleme hatası (CS1061) giderildi.

### [v5.7.5] - 2026-08-19 11:54
**Enhancements & Fixes:**
- **Signal Engine Fix:** LogFileWatcher.cs içerisindeki iDeal veritabanı okuma mekanizması iyileştirildi. Sinyal yazma işlemi sırasında dosyanın kısa süreli kilitli kalması durumunda okumanın tamamen başarısız olmasına yol açan sorun, okuma deneme sayısı 3'ten 10'a, bekleme süresi ise 250ms'den 500ms'ye çıkarılarak çözüldü (toplam tolerans 5 saniye).
- **Fallback Timer:** Windows FileSystemWatcher etkinlik düşürme ihtimaline karşı her 2 saniyede bir çalışan yedek bir zamanlayıcı (_fallbackTimer) eklendi.
