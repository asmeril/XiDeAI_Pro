# 📔 XiDeAI Pro - Proje Geliştirme Günlüğü

Bu günlük, proje üzerinde yapılan değişiklikleri, mimari kararları ve günlük ilerlemeyi takip etmek için tutulmaktadır.


## 📅 27 Şubat 2026

## 📅 27 Şubat 2026

### 🚀 v4.6.3 - Global 280-Character Limit Enforcer (HOTFIX)

**Değişiklikler:**
- **Merkezi X Karakter Sınırı:** `ThreadService.SplitText` metodu `SocialIntelService` aktarım katmanına entegre edildi. Artık Haber, Sinyal, veya Manuel Analiz fark etmeksizin gönderilen her tweet parçası 280 karakteri aşıyorsa akıllıca (cümle bütünlüğü korunarak) alt zincirlere bölünür. Mavi tiksiz hesaplarda yaşanan "Tweet karakter sınırını aştı" hataları tamamen önlendi.
- **AI Prompt Revizyonları:** `PromptManager.cs` içerisindeki tüm promptlara AI'ın 280 karakter altında içerik üretmesini zorunlu kılan katı yönergeler eklendi.

---

### 🚀 v4.6.2 - Cookie Format & Telegram Fixes (HOTFIX)

**Değişiklikler:**
- **undetected-chromedriver Cookie Senkronizasyonu:** WebView2'den kaydedilen JSON çerezlerindeki `isSecure`, `isHttpOnly` ve `expires` anahtarlarının Selenium'un beklediği `secure`, `httpOnly`, `expiry` anahtarlarına dönüştürülmesi sağlandı. Bu sayede `Failed to load cookies` hatası çözüldü (`social_intel.py` & `x_daemon.py`).
- **Telegram Bağlantı Testi:** `/start` eksikliğinden kaynaklı "chat not found" (HTTP 400) hataları UI'da mesaj kutusu şeklinde gösterilerek daha belirgin uyarı mekanizması yazıldı (`TelegramService.cs`).

---

## 📅 21 Ocak 2026

### 🚀 v4.4.0 - X Daemon Architecture (MAJOR)

**Problem:** Her X işlemi için yeni Python/Chrome başlatılıyordu. Bu:
- 5-15 sn startup overhead
- Zombi Chrome prosesleri
- Lock dosyası çatışmaları
- Sürekli 90s timeout hataları

**Çözüm:** HTTP Daemon mimarisi - tek Chrome instance, sürekli açık.

**Yeni Dosyalar:**
- `Scripts/x_daemon.py` - HTTP sunucu (localhost:5580)
  - 8 endpoint: `/search`, `/timeline`, `/find_handle`, `/like`, `/post_thread`, `/reply`, `/fetch_news`, `/health`
  
**Değişen Dosyalar:**
- `SocialIntelService.cs`:
  - `StartDaemonAsync()`, `StopDaemon()`, `DaemonRequestAsync()` eklendi
  - 4 kritik metod daemon-first güncellendi
- `OperationManager.cs`: App başlangıcında daemon otomatik başlatma

**Performans İyileştirmesi:**
- Eski: Her işlem ~30-60 saniye (Chrome başlatma dahil)
- Yeni: Her işlem ~5-20 saniye (Chrome hep açık)

---

## 📅 20 Ocak 2026

### 🚀 v4.2.x - Two-Step Intelligence Update

Bu güncelleme dizisi, hem Bot Etkileşim hem de Haber modüllerine "iki aşamalı zeka" sistemini getirmektedir.

#### v4.2.0 - Bot Interaction Two-Step Logic
- **Amaç:** Tek promptlu sistemden, önce kategori tespit + sonra kategoriye özel yanıt üreten sisteme geçiş.
- **6 Kategori:** FINANS, ESREF_TEK, MILLI_TOPLUM, BILGE_KULTUR, INSAN_RUH, GUNLUK_MIZAH
- **Değişen Dosyalar:** `PromptManager.cs`, `GeminiService.cs`, `MainForm.cs`, `InteractionEngine.cs`

#### v4.2.1 - Round-Robin Category Search
- **Amaç:** Her taramada tüm konuları aramak yerine, döngüsel olarak tek bir kategoriye odaklanma.
- **Değişen Dosyalar:** `ConfigManager.cs`, `MainForm.cs`

#### v4.2.2 - News Two-Step Logic (MAJOR)
- **Amaç:** Haberleri kategorize edip, önem skoruna (1-10) göre farklı akışlara yönlendirmek.
- **Yeni Skorlama:**
  - 10: Otomatik paylaş (haber + analiz)
  - 9: Onay bekle (haber + analiz)
  - 7-8: Onay bekle (sadece haber)
  - <7: Reddet
- **7 Haber Kategorisi:** EKONOMI, SIYASET, TEKNOLOJI, GLOBAL, KRIPTO, SPOR, YASAM
- **Özel Özellikler:**
  - SON DAKIKA boost: "Son dakika" içeren haberler minimum skor 8
  - Fenerbahçe bypass: FB haberleri minimum skor 7
  - Kategoriye özel AI promptları (Bot etkileşim gibi)
- **Değişen Dosyalar:**
  - `PromptManager.cs` (10 yeni metod)
  - `GeminiService.cs` (3 yeni metod: DetectNewsCategory, AnalyzeNewsImpactTwoStep, GenerateNewsCategoryAnalysis)
  - `NewsEngine.cs` (ProcessNews tamamen yenilendi)
  - `MainForm.cs` (Event handler'lar güncellendi)

---

### 🦁 FanZone 2.0 (v4.0.0 Güncellemesi)
Kullanıcı geri bildirimiyle FanZone modülü, "sadece tweet atma" aracı olmaktan çıkıp tam teşekküllü bir **Etkileşim Merkezi** haline getirildi.

**1. Yeni "Kadro" Paneli (UI)**
*   Arayüzün sağına **Takip Listesi** eklendi.
*   Resmi Hesaplar, Sporcular ve Muhabirler ayrıştırıldı.
*   Anlık güncelleme yeteneği eklendi.

**2. Otomatik Sporcu Keşfi (Auto-Discovery)**
*   `AthleteDiscoveryService` geliştirildi. Sistem, Google araması yaparak Fenerbahçe kadrosunu tarar ve Twitter adreslerini bulup havuza ekler.

**3. Polyglot (Çok Dilli) Yanıt**
*   Yabancı oyuncuların tweetlerine (İngilizce, Portekizce vb.) hem kendi dillerinde hem de Türkçe yanıt verme yeteneği kazandırıldı.
*   Örn: *"Great job! / Harika iş!"*

**4. Etkileşim Garantisi**
*   Resmi hesaplara **%100 Like & RT** garantisi.
*   Sporcu hesaplarına **%100 Like** garantisi.

## 📅 19 Ocak 2026 (Devam)

### 🚀 v3.9.4 - "Phoenix" Stability & UI Recovery
Twitter posting tıkanıklığı, kilitlenme sorunları ve boş arayüz problemleri tamamen giderildi.

**1. Twitter & Kilit Mekanizması (Fixed)**
*   **Sorun:** `social_intel.py` sürecinin kilit mekanizmasında asılı kalması tüm sosyal medya akışını durduruyordu.
*   **Çözüm:** `lock_manager.py` optimize edildi, bekleme süreleri kısaltıldı ve stale lock temizleme mantığı güçlendirildi.
*   **İyileştirme:** `screenshot.py` ve `MainForm.cs` içinde `VIP-` sembol temizleme mantığı standardize edildi.

**2. Sinyal Analiz & KPI Recover (Fixed)**
*   **Sorun:** "Sinyal Analiz" tabı boş görünüyor, istatistikler güncellenmiyordu.
*   **Çözüm:** `LoadSignalHistory` ve `UpdateKPICards` metodları hayata geçirildi. Sinyal kayıt (`RecordSignal`) mantığı veri kaybını önlemek için paylaşım öncesine çekildi.
*   **Görünürlük:** `SignalEngine.OnLog` olayları UI'ya bağlandı, robot sessizliği giderildi.

**3. Mimarî İyileştirmeler**
*   `OperationManager` servis yükleme sırası bağımlılıklara göre yeniden düzenlendi.
*   Loglama sistemi şeffaflaştırıldı.

## 📅 16 Ocak 2026

### 🚑 ACİL DÜZELTME: v3.7.6 Sunucu Sorunları
Sunucu tarafında tespit edilen (v3.7.6) kritik hatalar analiz edildi ve düzeltildi.

**1. "Fenomenler" Sekmesi Çökmesi (Fixed)**
*   **Sorun:** Sekme açılışında `InitializeInfluencerPanel` çalışırken, kategori filtresi `SelectedIndex` değişimi `Refresh` tetikliyordu. Ancak liste (`ListView`) henüz oluşturulmadığı için `NullReferenceException` oluşuyor ve uygulama kapanıyordu.
*   **Çözüm:** Filtre tetikleyicisi (Event Handler), liste oluşturulduktan sonraya taşındı.

**2. Üstat Analizi Screenshot Hatası (Fixed)**
*   **Sorun:** Sunucuda `chromedriver.exe` AppData klasöründe bulunamıyordu (Kurulum Program Files'a yapılıyor). Bu nedenle `screenshot.py` sürücüyü bulamayıp hata veriyordu.
*   **Çözüm:** `ScreenshotService.cs` içine akıllı yol tespiti eklendi. Artık sürücü şu sırayla aranıyor:
    1.  `AppData/XiDeAI/drivers` (Eski yöntem)
    2.  `AppDirectory/drivers` (Portable/Server kurulumu için)
    3.  `AppDirectory` (Kök dizin)

### 🎨 UI/UX Modernizasyon (v2.0)
Kullanıcı geri bildirimleri ve "Finansal Analiz Odaklı" vizyon doğrultusunda arayüz baştan aşağı yenilendi.

**Yapılan Değişiklikler:**
1.  **🧭 Sidebar (Navigasyon) Düzenlendi:**
    *   Butonlar **ANALİZ**, **ZEKA** ve **SİSTEM** başlıkları altında gruplandı.
    *   Görsel karmaşa giderildi, daha temiz bir hiyerarşi kuruldu.
2.  **📉 Sinyal Merkezi 2.0:**
    *   **KPI Paneli:** Günlük Sinyal, Başarı Oranı ve Trend göstergeleri eklendi.
    *   **Akıllı Filtreler:** Checkbox yığını yerine modern "Chip" butonlar kullanıldı.
    *   **Smart Grid:** Tablo renklendirildi, skorlara göre yeşil/sarı/gri renk kodları tanımlandı.
3.  **🏛️ HIVE Hub (Birleşme):**
    *   "Haberler" ve "Fenomenler" modülleri, HIVE Intel çatısı altına taşındı.
4.  **💬 İletişim Merkezi (Consolidation):**
    *   `Bot Etkileşim` ve `Etkileşim Merkezi` tek çatı altında birleştirildi.
5.  **⚙️ Ayarlar 2.0:**
    *   Kategori bazlı (Sol Menü - Sağ Detay) yapıya geçildi.

### 🕵️ Derin İnceleme ve İndeksleme
Proje üzerinde çalışan AI asistanın (ben) kod tabanına tam hakimiyet sağlaması için detaylı bir "Deep Inspection" (Derin İnceleme) yapıldı.

## 📅 16 Ocak 2026 (Bugün)

### 🛠️ v3.7.8 - Stability Update
Kullanıcı geri bildirimlerine dayalı kritik düzeltmeler ve iyileştirmeler yapıldı.

**1. "Haberler" Tabı Restorasyonu**
*   **Durum:** v3.7.7'de "HIVE Intel" altına taşınan haberler modülü, kullanıcı alışkanlığı nedeniyle sol menüde eksik hissediliyordu.
*   **İşlem:** `MainForm.cs` içinde `btnNavNews` butonu geri getirilerek sol menüye, "ZEKA (HIVE)" başlığının altına eklendi.

**2. Screenshot Service (Kök Neden Çözümü)**
*   **Sorun:** Sunucu ortamında `chromedriver.exe` yolunun bulunamaması veya sürüm uyumsuzluğu.
*   **İşlem:** `ScreenshotService.cs`'e özel sunucu yolu (`C:\Users\asmeril\AppData\...`) eklendi. `screenshot.py` dosyasına `SessionNotCreatedException` yakalama ve otomatik driver güncelleme (fallback) yeteneği kazandırıldı.

**3. Gelişmiş Hata Raporlama (Apex/Omni/Oracle)**
*   **Sorun:** Analiz servisleri hata verdiğinde sadece "Sentez başarısız" yazıyordu.
*   **İşlem:** Tüm servislerin (`ApexService`, `OmniScoutService`, `OracleService`) hata yakalama blokları detaylandırıldı.

**4. Model İsimleri Düzeltmesi (Kritik)**
*   **Sorun:** `ModelManager.cs` içinde yanlış Gemini model isimleri (`gemini-flash`, `gemini-pro-2.0`) kullanılıyordu. API bu isimleri tanımıyordu ve boş yanıt dönüyordu.
*   **İşlem:** Tüm model isimleri doğru API identifierlarıyla değiştirildi (`gemini-2.0-flash-exp`, `gemini-1.5-flash`, `gemini-1.5-pro`).
*   **⚠️ NOT:** Bu model isimleri daha sonra (v3.8.5) deprecate edilip `gemini-2.5-flash`, `gemini-2.5-pro` ile değiştirildi.

### 🎨 v3.7.9 - Ayarlar Sayfası Yeniden Tasarımı

**1. Eksik Butonların Geri Getirilmesi**
*   **🧪 Test** butonu: Seçili modeli test edip yanıt süresini gösterir.
*   **📋 Modeller** butonu: Tüm kullanılabilir Gemini modellerini listeler.

**2. Akıllı Model Seçimi (Benchmark)**
*   **Yeni Servis:** `ModelBenchmarkService.cs` - Tüm modelleri paralel test eder.
*   **Benchmark Grid:** Model adı, tier, yanıt süresi ve durum tablosu.
*   **Otomatik Seç:** Benchmark sonuçlarına göre en uygun modeli seçer.

**3. UI/UX İyileştirmeleri**
*   Kompakt tek satır düzeni (label + input + button aynı satırda).
*   Sol panel küçültüldü (180px), daha fazla içerik alanı.
*   Renk kodlaması: Gemini=LimeGreen, Perplexity=Cyan, TradingView=Cyan, Telegram=Orange.

**4. Dinamik Model Listesi (Canlı API)**
*   **📋 Modeller** butonu artık API'den gerçek zamanlı model listesi çeker.
*   `ModelBenchmarkService.FetchAvailableModelsAsync()` metodu eklendi.
*   Embedding, TTS ve image modellerini otomatik filtreler.
*   ComboBox'ı güncel modellerle günceller.
*   Fallback: API başarısız olursa önceden tanımlı modeller kullanılır.

---

## 📅 16 Ocak 2026

### v3.8.1 Release Notes
- **Tarih:** 16.01.2026
- **Odak:** HIVE Phase 3 - Cortex Integration
- **Değişiklikler:**
  - `CortexService.cs` implemente edildi.
  - OperatorForm Sentez sekmesine Cortex entegrasyonu yapıldı.
  - `OmniScout` ve `Oracle` servislerine `LastReport` özelliği eklendi.
  - `SentinelService` duplicate kod temizliği yapıldı.
  - Cross-Reference (Çapraz Referans) mantığı kuruldu.

### 🚀 v3.8.0 Release - HIVE Protocol Phase 1 & 2

> **Odak:** HIVE Modülü entegrasyonunun tamamlanması (OmniScout, Oracle) ve Operasyon Merkezi (OperatorForm) iyileştirmeleri.

#### 🌟 Yeni Özellikler (HIVE Modülü)
*   **Omni-Scout (Viral) Tab:** HIVE modülüne eklendi. Global viral trendleri ve Reddit akışını tarayıp raporluyor.
*   **Oracle (Kahin) Tab:** HIVE modülüne eklendi. Polymarket verileriyle gelecek senaryoları (scenario generation) üretiyor.
*   **Canlı Sentez Raporu:** OperatorForm "Sentez" sekmesi artık Sentinel'den gelen gerçek etkileşim verileriyle (toplam yanıt, duygu durumu, öne çıkan geri bildirimler) doluyor.

#### 🛠️ İyileştirmeler & Düzeltmeler
*   **Sentinel Analysis History:** Sentinel servisine geçmiş analizleri bellekte tutma yeteneği eklendi (`_analysisHistory`).
*   **İcra (Execution) Güvenliği:** Tweet gönderim butonu (`BtnLaunchNext`) artık API hatalarını yutmuyor, kullanıcıya net hata mesajı gösteriyor ve logluyor.
*   **UI Tutarlılığı:** Tüm HIVE sekmeleri için standart `UpdateUI` metodları eklendi.

#### ⚠️ Bilinen Eksikler (HIVE Phase 3)
*   **Cortex Engine:** Çapraz referans motoru henüz aktif değil.
*   **Vision-Grid:** Görsel analiz modülü beklemede.

### 🟢 v3.8.2 - Resurrection Update (Diriliş)

**Odak:** Kullanıcı geri bildirimlerine dayalı kritik sistem onarımları ve modüllerin (Influencer, Haber, Motivasyon) yeniden devreye alınması.

**1. "Fenomen Veritabanı" Restorasyonu**
*   **Sorun:** Sidebar menüsünden "Fenomenler" butonu kaybolmuştu, panele erişilemiyordu.
*   **Çözüm:** `MainForm.cs` içinde `btnNavInfluencers` butonu HIVE grubuna yeniden eklendi.

**2. Motivasyon Modülü Onarımı**
*   **Sorun:** Sabah motivasyon tweetleri atılmıyordu. Kod incelemesinde ilgili metodun (`PostMorningMotivation`) silindiği tespit edildi.
*   **Çözüm:** Metot yeniden yazılarak sisteme entegre edildi. Zamanlama mantığı "tam 09:00" yerine "09:00-10:00 aralığı" olarak esnetildi, böylece bilgisayarın o dakika kapalı olması durumunda bile açıldığında telafi etmesi sağlandı.

**3. Haber Filtresi Gevşetme (Anti-Silent Mode)**
*   **Sorun:** Haber modülü, katı filtreler (24 saat sınırı ve dar keyword listesi) nedeniyle birçok önemli haberi "sessizce" yutuyordu.
*   **Çözüm:**
    *   **Süre:** 24 saat sınırı **48 saate** çıkarıldı (Hafta sonu akışı için).
    *   **Keyword:** Finansal terim listesi ("temettü", "bilanço", "halka arz" vb.) 3 katına çıkarıldı.
    *   **Test Modu:** `ConfigManager`'a `NewsTestMode` eklendi. Aktif edildiğinde tüm filtreleri bypass eder.

**4. Bot İletişim Analizi**
*   **Durum:** Botun cevap vermemesi sorunu incelendi. Sorunun bot kodunda değil, kaynak tweet (haber/motivasyon) eksikliğinde olduğu anlaşıldı. Tweet akışı başladığında botun doğal olarak etkileşime gireceği teyit edildi.

---

## 16 Ocak 2026

### v3.8.3 Release

> **Odak:** Günlük Rapor istatistiklerinin düzeltilmesi, Guru Taramaları için performans takibinin etkinleştirilmesi ve rapor içeriğinin "reklam dostu" hale getirilmesi.

#### ✨ Yeni Özellikler & İyileştirmeler

**1. "Dürüst İstatistik, Şık Vitrin" Raporlama**
*   **Sorun:** Günlük raporda kazanan/kaybeden oranları gösterilmiyordu çünkü Guru modülünden gelen sinyallerin kapanış fiyatları takip edilmiyordu.
*   **Çözüm (Dürüstlük):** `PerformanceTracker` artık tüm sinyalleri (pozitif/negatif) dahil ederek "Başarı Oranı" ve "Kar Faktörü" hesaplıyor.
*   **Çözüm (Vitrin):** Ancak raporun görsel kısmında (liste) **sadece en çok kazandıran ilk 3 sinyal** listeleniyor. Kaybettirenler gizleniyor ancak istatistiğe dahil ediliyor.

**2. Guru Scan Performans Takibi**
*   **Eksik:** "Efe HMA", "Trend Temelli" gibi görüntü işlemeyle alınan sinyaller `PerformanceTracker`'a kaydedilmiyor, bu yüzden başarı oranları "0%" kalıyordu.
*   **Çözüm:** `MainForm.cs` içinde tarama sonuçları artık `RecordSignal` ile veritabanına işleniyor.

**3. Kapanış Öncesi Fiyat Güncellemesi (PnL Fix)**
*   **Sorun:** Gün sonu raporu hazırlanırken sinyallerin "o anki" fiyatı güncellenmediği için PnL hesaplanamıyordu.
*   **Çözüm:** `PostMarketCloseSummary` çalışmadan hemen önce, o günün tüm açık sinyalleri için `PriceFetchService` ile güncel fiyat kontrolü yapılıyor.

**4. Versiyon Yönetimi**
*   Tüm proje (Installer, Assembly, Docs) v3.8.3 sürümüne senkronize edildi.



## 📅 18 Ocak 2026

### 🛠️ v3.8.3 Hotfix & Infrastructure Upgrade

**1. Haber Takip Hattı Onarımı (News Pipeline Fix)**
*   **Sorun:** RSS ve Twitter'dan haberler çekiliyor ancak ana sayfaya düşmüyor veya işlenmiyordu.
*   **Kök Neden:** `MainForm.cs` içinde `InitializeNewsPanel` metodunda, `NewsTracker` servisinin olay tetikleyicisi (`OnNewsDetected`) dinlenmiyordu. Servis boşluğa bağırıyordu.
*   **Çözüm:** Eksik olan `+= OnNewsReceived` bağlantısı eklendi.

**2. Atomic Lock Manager (Dosya Kilit Sistemi)**
*   **Amaç:** `social_intel.py` scriptinin aynı anda birden fazla kez çalışıp X oturumunu bozmasını (cookies çakışması) engellemek.
*   **Uygulama:** `Scripts/lock_manager.py` modülü yazıldı. Platform bağımsız (Windows/Linux) dosya kilitleme ve "stale lock" temizleme mantığı kuruldu.

---

## 18 Ocak 2026

### v3.8.4 Release - Fenomenler Tab Fix

**Fenomenler Sekmesi Çökme Düzeltmesi**
*   **Sorun:** "Fenomenler" sekmesine tıklandığında uygulama hiçbir hata vermeden kapanıyordu.
*   **Kök Neden:** `RefreshInfluencerListView` metodu ComboBox'ı temizlerken `SelectedIndexChanged` eventini tetikliyor, bu da yeniden aynı metodu çağırarak sonsuz döngü oluşturuyordu. Döngü sırasında `null` referans hatası oluşuyordu.
*   **Çözüm:** 
    1. `_suppressEvents` bayrağı eklenerek event handler susturuldu.
    2. `if (selected == null) return;` guard eklendi.
    3. `SelectedIndex == -1` kontrolü eklendi.

### v3.8.5 Release - AI Model Migration

**1. Gemini Model İsimleri Güncellendi (KRİTİK)**
*   **Sorun:** `gemini-1.5-flash` ve `gemini-1.5-pro` modelleri API tarafından `NotFound` hatası döndürüyordu (deprecate edilmiş).
*   **Çözüm:** Tüm model referansları güncellendi:
    *   `gemini-1.5-flash` → `gemini-2.5-flash` (Stabil, Üretim)
    *   `gemini-1.5-pro` → `gemini-2.5-pro` (Stabil, Üretim)
*   **Etkilenen Dosyalar:** `ModelManager.cs`, `OperationManager.cs`, `ModelBenchmarkService.cs`

**2. NewsEngine Rate Limiting**
*   **Sorun:** 13 haber aynı anda işlenmeye çalışılınca `TooManyRequests` hatası alınıyordu.
*   **Çözüm:** `ProcessNews` metoduna 2 saniyelik bekleme (`await Task.Delay(2000)`) eklendi.

**3. Otomatik Model Optimizasyonu (YENİ)**
*   **Özellik:** Sistem artık her açılışta ve gece 03:00'te Gemini API modellerini benchmark testine sokuyor.
*   **Fayda:** O anki en hızlı ve maliyet-etkin model (örn: `2.0-flash` vs `2.5-flash`) otomatik seçiliyor.

**4. Sosyal Veri Stabilizasyonu (Hotfix)**
*   **Sorun:** Eda Erdem vb. hesaplarda `malformed result item` hatası alınıyordu.
*   **Çözüm:** `SocialIntelService` JSON ayrıştırıcısına defansif kod eklendi. Eksik alan geldiğinde hata vermek yerine varsayılan değer atanıyor.

---

## 18 Ocak 2026

### v3.9.0 Release - Visionary Guru Analysis (18.01.2026)
**Odak:** Üstat Analizi (Guru) modülüne AI Vision (Görsel Analiz) ve Smart Money mantığı eklenmesi.
- **AI Vision:** `GeminiService.AnalyzeChartImage` ile grafikler artık teknik olarak yorumlanıyor.
- **Smart Money:** MSB, FVG ve Likidite kavramları analizlere dahil edildi.
- **Kalite:** Guru analizleri artık Sinyal kalitesine yükseltildi, "Piyasa Görüşleri" kaldırıldı.

### v3.9.1 Release - Prompt & Stability Polish (18.01.2026)
**Odak:** Kullanıcı deneyimi iyileştirmesi ve sistem kararlılığı.
- **Prompt Fix:** Tweet çıktılarındaki gereksiz başlıklar (`(Birinci Tweet Metni)`) temizlendi.
- **Build Fix:** C# tarafındaki tırnak işareti ve değişken ismi hataları giderildi.
- **Auto-Publish:** Yayın süreci v3.9.1 için otonom olarak tamamlandı.

---

## 19 Ocak 2026

### v3.9.2 Release - Guru Tagging & Strategy Focus (19.01.2026)
**Odak:** Üstat analizlerinde hoca etiketleme hatalarının giderilmesi ve tarama adı vurgusu.

**1. Dinamik Hoca Etiketleme (Handle-Based):**
*   **Düzeltme:** Artık `@Efelerin Efesi` (hatalı) yerine doğrudan hocanın handle'ı (`@EFELERiiNEFESi3`) dinamik olarak kullanılıyor.
*   **İyileştirme:** AI'nın hocayı yanlış etiketlemesini veya boşluklu isim kullanmasını engelleyen handle-based yapıya geçildi.

**2. Tarama Adı ve Vurgu Revizyonu:**
*   **Terminoloji:** "Radar" kelimesi yerine kullanıcı isteği doğrultusunda "Tarama" vurgusu tercih edildi.
*   **Vurgu:** İlk tweetlerde `Efe HMA` veya `Trend Temelli` gibi tarama tablosunun adı (strategy) ön plana çıkarıldı.
*   **Dinamik İntro:** 5 farklı giriş tarzı bu yeni metadata ile uyumlu hale getirildi.

**3. Kritik Build & Syntax Onarımları:**
*   `PromptManager.cs` üzerindeki verbatim string sonlandırma hatası (`";` syntax hatası) giderildi.
*   Unicode/Emoji kaynaklı derleme risklerini önlemek için teknik prompt içeriği sadeleştirildi.

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

### 🚀 v4.0.0 Release - "Clean Slate" (Temiz Sayfa)

> **Odak:** HIVE Protocol'ün tamamen temizlenmesi, kod tabanının AI eğitimine hazırlanması ve yerel RAG sistemi altyapısının kurulması.

#### 🧹 HIVE Protocol Temizliği (Major Refactor)

**1. Tamamen Kaldırılan Modüller:**
*   `SentinelService.cs` - Etkileşim izleme servisi
*   `ApexService.cs` - Ar-Ge analiz servisi
*   `OmniScoutService.cs` - Viral trend servisi
*   `OracleService.cs` - Senaryo üretim servisi
*   `WisdomService.cs` - Bilgelik servisi
*   `CortexService.cs` - Çapraz referans motoru
*   `MetaTeacherService.cs` - Meta-öğrenme servisi
*   `OperatorForm.cs` - HIVE operasyon formu (Backup: `HiveProjesi/Forms/`)

**2. Temizlenen Kod Artıkları:**
*   `MainForm.cs`: HIVE Hub paneli, Sentinel Inbox, navigasyon butonları ve Telegram komutları silindi.
*   `GeminiService.cs`: `GenerateMetaAnalysisV2` ve `GenerateMetaAnalysis` metodları kaldırıldı.
*   `OperationManager.cs`: HIVE servis referansları ve yorumları temizlendi.
*   Tüm `[HIVE REMOVED]` etiketli yorum satırları silindi.

**3. Yedekleme:**
*   Tüm HIVE kodu `d:\Projects\HiveProjesi` klasörüne taşındı.
*   `KURULUM_REHBERI.md` ile yeniden entegrasyon talimatları belgelendi.

#### 🧠 Yerel AI Eğitim Sistemi (YENİ)

**1. Kod Export Script'i (`Scripts/export_for_ai.ps1`):**
*   Tüm `.cs`, `.py` ve `.md` dosyalarını `AI_Knowledge_Base/` klasörüne toplar.
*   49 C# + 13 Python + 13 dokümantasyon dosyası export edildi.

**2. RAG Sistemi (`Scripts/setup_rag.py`):**
*   LangChain + ChromaDB kullanarak kod tabanını vektör veritabanına indeksler.
*   DeepSeek Coder V2 ile uyumlu.

**3. İnteraktif Chat (`Scripts/xideai_rag.py`):**
*   Kod tabanı hakkında soru-cevap yapabilen CLI arayüzü.
*   Örnek: "MainForm.cs ne iş yapar?" → AI detaylı açıklama verir.

#### 🛠️ Teknik Düzeltmeler

**1. Duplicate Build Hatası Çözümü:**
*   **Sorun:** `AI_Knowledge_Base/codebase/` içindeki .cs dosyaları derlemeye dahil oluyordu.
*   **Çözüm:** `.csproj` dosyasına `<Compile Remove="AI_Knowledge_Base\**\*.cs" />` eklendi.

**2. SendTweetAsync API Uyumluluğu:**
*   **Sorun:** Metod artık `bool` yerine `string?` (tweet URL) döndürüyor.
*   **Çözüm:** 4 adet string→bool dönüşüm hatası düzeltildi.

---

---

## 20 Ocak 2026

### v4.3.0 Release

> **Odak:** Sinyal sisteminin "Two-Step Intelligence" ile güçlendirilmesi. Hibrit puanlama, strateji odaklı personalar ve içerik tiering sistemi.

#### 🧠 Hybrid Signal Intelligence (Hibrit Sinyal Zekası)

**1. İki Kademeli Puanlama Sistemi:**
*   **Normalized Score:** Teknik göstergelerden gelen ham puan (0-100).
*   **Final Score (Hybrid):** AI onayı, görsel formasyon ve fenomen desteği ile zenginleştirilmiş nihai karar puanı.
*   **Content Tiering:** Puana göre içerik derinliği belirlenir:
    *   **Premium (85+):** Detaylı analiz, formasyon grafiği, fenomen alıntıları.
    *   **Standard (65-84):** Temel analiz ve hedef fiyatlar.
    *   **Summary (50-64):** Kısa özet.

**2. Strateji Bazlı Persona Yönetimi (Prompt Dispatcher):**
*   **KING/BOMBA:** "Agresif ve Hype Odaklı" - Emoji ve coşku ağırlıklı dil.
*   **TEFO:** "Teknik ve Formasyon Odaklı" - Seviyeler ve kırılımlar.
*   **ANKA:** "Diriliş ve Dönüş Odaklı" - Dip dönüş sinyalleri.
*   **DIP/ZIRVE:** "RSI ve Aşırılık Odaklı" - Tepki bölgeleri.
*   **Standart:** Dengeli profesyonel dil.

**3. Otomatik İçerik Zenginleştirme:**
*   **Gemini 2.0 Vision:** Grafiklerdeki formasyonları (Flama, Tobo vb.) otomatik okur ve analize dahil eder.
*   **Influencer Intelligence:** İlgili hisse hakkında konuşan fenomenlerin tweetlerini (Smart Money) analize ekler.

#### 🛠️ Teknik İyileştirmeler
*   `SignalEngine`: İş akışı `ProcessStructuredSignal` ile modernize edildi.
*   `PromptManager`: `GetStrategySpecificPrompt` ile stratejiye özel prompt seçimi eklendi.
*   `ThreadService`: `PostAIGeneratedThread` ile AI tarafından üretilen hazır thread formatı (||| ayracı) desteklendi.
*   **Zirve Sinyali Fix:** Eşik değeri 12'den 10'a düşürülerek algılama hassasiyeti artırıldı.

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

## 27 Şubat 2026

### ✨ v4.6.1 (2026-02-27)
- **Anti-Bot Güvenliği:** Standart Selenium yerine donanımsal düzeyde Chrome parmak izlerini gizleyen `undetected-chromedriver` kütüphanesi entegre edildi. Cloudflare ve X bot duvarları aşıldı.
- **Güvenli Başlangıç (Safe Boot):** Program açılır açılmaz arka planda tetiklenen X Daemon, Haber Taraması, Fenerbahçe Modülü, DeepScan ve Trend Etkileşim servislerinin otomatik başlaması **devre dışı bırakıldı**. Tüm modüller artık sadece ana ekrandaki "START" butonuna manuel basıldığında güvenle çalışmaya başlar.
- **İyileştirme:** `MainForm.cs`, `OperationManager.cs`, `x_daemon.py` ve `social_intel.py` dosyalarında anti-bot senkronizasyonları tamamlandı.cek.

---

## 27 Şubat 2026

### v4.6.4 (2026-02-27) - Twitter Threading & Session Fix
- **Fix:** `MainForm.cs` ve `OperationEngine.cs` üzerindeki kapanış raporu (Market Close) döngüsü düzeltildi. Artık raporlar ayrı ayrı tweetler yerine tek bir zincir (thread) olarak paylaşılacak.
- **İyileştirme:** `x_daemon.py` üzerindeki zincirleme mantığı güçlendirildi. Çoklu buton bulma ve "Add another post" butonu doğrulama adımları eklendi.
- **Bug Fix:** Cookie yükleme sonrası yaşanan giriş ekranına yönlenme problemi, `refresh()` yerine doğrudan home navigasyonu (`x.com/home`) yapılarak çözüldü.
- **Teknik:** `update-version.ps1` kullanılarak tüm proje dosyaları ve manifest v4.6.4 sürümüne yükseltildi.

---

## 27 Şubat 2026

### v4.6.5 (2026-02-27) - Hotfix: JSON Parsing Error
- **Fix:** Influencer search sırasında oluşan "'L' is an invalid start of a value" hatası giderildi.
- **Detay:** `social_intel.py` içindeki `log_debug` fonksiyonunda bulunan hardcoded (`d:\Projects`) log yolu ve bu yol hata verdiğinde `stdout`'a basılan "Log error" mesajı temizlendi.
- **İyileştirme:** Tüm debug logları `APPDATA_DIR` altına taşındı ve Python `print` çıktıları JSON akışını bozmaması için `stderr`'e yönlendirildi.
- **Büyük Fix:** v4.6.4 ile gelen thread ve cookie senkronizasyonu düzeltmeleri korunarak sistem stabilize edildi.


