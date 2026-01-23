# 🧠 XiDeAI Pro Project Manifest v3.9.2 (Metadata & Tagging Focus)
## "The Refined Intelligence"
> **AI Context Reference Document**
> **Last Updated:** 2026-01-19
> **Architecture:** HIVE OPERATOR (Vision-Integrated)

Bu belge, projenin mevcut durumunu, mimari kararlarını ve gelecekteki yol haritasını diğer Yapay Zeka modellerinin (Claude, GPT-4, Gemini vb.) anlayacağı teknik derinlikte özetler.

---

## 🏗️ Mimari Genel Bakış: HIVE OPERATOR Protocol
XiDeAI Pro, geleneksel bir bot olmaktan çıkıp, "Human-in-the-Loop" (İnsan Döngüde) prensibiyle çalışan otonom bir **HIVE (Kovan)** yapısına evrilmiştir. v3.9.x serisi ile bu yapıya **AI Vision (Görsel Zeka)** katmanı tam entegre edilmiştir.

### 🆕 v3.9.2 ile Gelenler (Metadata & Formatting Refinement)
Bu sürümde Üstat Analizi modülündeki etiketleme hataları giderilmiş ve kullanıcı deneyimi odaklı format iyileştirmeleri yapılmıştır.

1.  **Dinamik Hoca Etiketleme:**
    - `@Efelerin Efesi` (hatalı) yerine doğrudan hocanın handle'ı (`@EFELERiiNEFESi3`) dinamik olarak threadlere ekleniyor.
    - `GeminiService` ve `PromptManager` bu handle verisini senkronize bir şekilde işliyor.

2.  **Tarama & Strateji Odaklı Dil:**
    - "Radar" terminolojisi "Tarama" veya doğrudan "StrategyName" (Efe HMA vb.) ile değiştirildi.
    - İlk tweetlerde strateji adı vurgusu artırılarak kurumsal bir görünüm sağlandı.

3.  **Sağlamlık (Robustness):**
    - `PromptManager.cs` üzerindeki verbatim string sonlandırma ve tırnak işareti kaçırma hataları giderildi.
    - Multimodal prompt akışı hata toleransı artırıldı.

---

### Phase 4: AUTONOMOUS VISION (Görsel Otonomi)
**Durum: 🏗️ DEVAM EDİYOR (v3.9.0)**
- **Amaç:** Sadece metni değil, grafiği de "okuyan" ve buna göre işlem/paylaşım kararı alan bir sistem.
- **Components:**
    - `GeminiService.AnalyzeChartImage`: Görsel okuma motoru.
    - `PromptManager.GetGuruHonoringThreadPrompt`: Vision-integrated prompt şablonu.
    - `MainForm.AnalyzeSelectedGuruTableAsync`: Görsel otonomi iş akışı.

---

### Phase 1: EXECUTION & BROADCAST (İcra ve Yayın)
**Durum: ✅ TAMAMLANDI**

- **Amaç:** Sinyalleri yakalamak, analiz etmek ve stratejik bir anlatıyla (narrative) yayınlamak.
- **Components:**
    - `OperationManager`: Merkezi operasyon yöneticisi.
    - `OperatorForm`: İcra paneli. Kullanıcıya her adımı (Tweet zinciri) onaylatır veya otomatik gönderir. "Meta-Teacher" -> "Apex Context" strateji akışını yönetir.
    - `SignalEngine`: Sinyalleri işleyen, filtreleyen ve formatlayan motor.

### Phase 2: LISTENING & SENTINEL (Dinleme ve Nöbetçi)
**Durum: ✅ TAMAMLANDI (v3.7.5)**
- **Amaç:** Yayınlanan içeriklere gelen tepkileri dinlemek, analiz etmek ve etkileşimi yönetmek.
- **Components:**
    - `SentinelService`: Tüm X (Twitter) bildirimlerini ve yanıtlarını dinleyen servis.
    - **Engagement Hub (Etkileşim Merkezi):** Tüm etkileşimlerin toplandığı, AI tarafından sınıflandırıldığı (Destek/Soru/Talep) ve yanıt önerilerinin sunulduğu UI.
    - **Telegram Remote Control:** Dışarıdayken sistemi yönetmek için kullanılan "Uzaktan Kumanda". Push bildirimleri ve `/ONAY`, `/RED`, `/ANALIZ` komutları.
    - **Otomatik RT Teşekkürü:** İçerikleri RT edenlere otomatik (spam korumalı) teşekkür eden sadakat motoru.

### Phase 3: SYNTHESIS & RECYCLE (Sentez ve Geri Dönüşüm)
**Durum: ✅ TAMAMLANDI (v3.8.1)**
- **Amaç:** Toplanan etkileşim verilerini ve harici istihbaratı yeni bir stratejiye dönüştürmek.
- **Components:**
    - `CortexService`: Tüm modülleri yöneten üst akıl.
    - `Cross-Reference Engine`: Veri ilişkilendirme motoru.
    - `OperatorForm - Synthesis Tab`: Sonuçların sunulduğu arayüz.
- **Hedef:** Veriden anlam ve eylem üretmek.

---

## 🧩 Kritik Servisler ve Görevleri

| Servis Adı | Açıklama |
| :--- | :--- |
| `Services\GeminiService.cs` | Yapay Zeka motoru. `AnalyzeChartImage` (Vision) ve `GenerateGuruHonoringThread` (Multimodal) metotlarıyla görsel analizi yönetir. |
| `Services\SentinelService.cs` | "Nöbetçi". Reply ve RT'leri yakalar. `ReplyAnalysis` nesnesi oluşturur. Sinyal, Manuel Analiz ve HIVE contextlerini ayırt eder. |
| `Services\SocialIntelService.cs` | Dış dünya ile köprü. Python scriptleri (`social_intel.py`) üzerinden X verilerini çeker ve yazar. |
| `Services\OperationManager.cs` | Orkestra şefi. Tüm servisleri (Twitter, Gemini, Telegram, Sentinel) yönetir ve birbirine bağlar. |
| `Services\TelegramService.cs` | Kullanıcı ile iletişim. Logları basar ve Remote Control komutlarını işler. |
| `MainForm.cs` | Ana UI ve Event Handler. `Engagement Hub` ve `_sentinelTimer` burada barınır. |

---

## 🛠️ Son Yapılan Majör Geliştirmeler (v3.7.5)

### 1. Unified Engagement Hub (Etkileşim Merkezi)
- `MainForm` üzerine yeni bir sekme eklendi.
- Sinyal botu, Manuel analizler ve Guru takibi gibi farklı kaynaklardan gelen tüm yorumlar tek havuzda toplanıyor.
- `RichTextBox` üzerinden renkli, log tabanlı bir akış sunuluyor.
- **Özellik:** Tıklanabilir linkler (`analyze:SYMBOL`) eklendi.

### 2. Telegram Uzaktan Yönetim (Remote Control)
- **Push Notification:** Yeni bir etkileşim veya analiz talebi geldiğinde Telegram'a anlık bildirim düşer.
- **Actionable Commands:**
    - `/ONAY [ID]`: AI'nın önerdiği yanıtı onaylar ve X'e gönderir.
    - `/REPLY [ID] [TEXT]`: Özel bir yanıt yazar.
    - `/ANALIZ [SYMBOL]`: PC başında değilken analiz motorunu tetikler ve raporu Telegram'a geri basar.

### 3. Analiz Talebi Otomasyonu (Request Detection)
- Sentinel AI prompt'una `TALEP` kategorisi eklendi.
- "XRP bakar mısınız?" gibi soruları algılar, `$XRP` sembolünü ayıklar.
- Operatöre (User) "Analiz İsteği Geldi -> [İşlem Yap]" şeklinde aksiyon butonu sunar.

---

## �️ Son Düzeltmeler (v3.7.8 - Stability Update)

### 1. Haberler Tabı Restorasyonu
- Kullanıcı alışkanlıkları gözetilerek "Haberler" sekmesi sol menüye geri getirildi.
- "HIVE Intel" altındaki yeni yapı korunurken, eski erişim yolu da aktif edildi.

### 2. Screenshot Service Robustness
- `chromedriver.exe` tespiti için akıllı tarama mekanizması güçlendirildi. (AppData, Program Files, Project Dir).
- Sunucu ortamındaki driver uyumsuzluklarına karşı çoklu path kontrolü eklendi.

### 3. Hata Raporlama (Apex & Omni)
- Servislerin "Analiz oluşturulamadı" hatası verdiği durumlarda, sorunun kök nedenini (API hatası, Timeout vb.) gösteren detaylı loglama eklendi.

---

## 🎨 v3.7.9 - Ayarlar Sayfası Yeniden Tasarımı

### 1. Yeni Ayarlar Arayüzü
- **API Test Butonu:** Gemini API key'inin çalışıp çalışmadığını tek tıkla test eder.
- **Modeller Butonu:** API'den canlı model listesi çeker (statik liste yerine).
- **Kompakt UI:** FlowLayoutPanel ile tek satır düzeni (label + input + button aynı satırda).

### 2. Akıllı Model Benchmark Sistemi
- **ModelBenchmarkService.cs:** Tüm Gemini modellerini paralel test eder.
- **Benchmark Grid:** Model adı, tier (Hızlı/Pro/Deneysel), yanıt süresi, durum tablosu.
- **Otomatik Seç:** Benchmark sonuçlarına göre en uygun modeli seçer.

### 3. Dinamik Model Listesi
- `FetchAvailableModelsAsync()`: API'den gerçek zamanlı model listesi çeker.
- Embedding, TTS, image modellerini otomatik filtreler.
- ComboBox'ı güncel modellerle günceller.
- Fallback: API başarısız olursa önceden tanımlı modeller kullanılır.

### 4. Mevcut ModelManager Yapısı (Değişmedi)
- 12 farklı TaskType için önceliklendirilmiş model listeleri korundu.
- Otomatik fallback mekanizması aktif.
- İş saatleri öncelik sistemi (düşük öncelikli görevlere gecikme).

---


## v3.8.1 - HIVE Phase 3: Cortex Integration (The Brain)

### 1. Cortex Service (The Mastermind)
- **Role:** HIVE ekosisteminin "beyni" olarak konumlandırıldı.
- **Function:** Omni-Scout (Viral), Oracle (Kahin), Apex (Ar-Ge) ve Sentinel (Geri Bildirim) verilerini toplar.
- **Output:** Bu verilerden *Grand Strategy* (Büyük Strateji) oluşturur.
- **Cross-Reference:** Farklı kaynaklar arasındaki korelasyonları (Örn: Reddit hype'ı + Polymarket tahmini) bulur.

### 2. Sentez (Synthesis) Tabı Entegrasyonu
- **Cortex Analizi Başlat:** OperatorForm üzerinden tek tıkla tüm istihbarat ağını çalıştıran buton.
- **Raporlama:** Cortex sonuçları renkli ve yapılandırılmış formatta Sentez sekmesinde sunulur.

### 3. LastReport Mimarisi
- `OmniScoutService` ve `OracleService` sınıflarına son analiz sonuçlarını tutan bellek (`LastReport`) eklendi.
- Bu sayede Cortex, verileri anlık olarak çekmek yerine mevcut en taze veriyi kullanabilir (Performance Optimization).

---

## 📊 v3.8.3 - Reporting Intelligence Update (Dürüst Raporlama)

Bu güncelleme, sistemin kendi performansını nasıl analiz ettiği ve sunduğu üzerine odaklanır. "Human-in-the-Loop" prensibine "Marketing-Aware Honesty" (Pazarlama Farkındalıklı Dürüstlük) katmanı eklendi.

### 1. Performance Tracking 2.0 (Guru & Scans)
- **Sorun:** Görüntü işleme tabanlı sinyaller (Guru Scan) veritabanına girmediği için istatistiklerde yer almıyordu.
- **Çözüm:** `Integration Layer` güçlendirildi. `MainForm` -> `RecordSignal` akışı ile artık tüm görsel taramalar PnL takibinde.

### 2. Auto-PnL Verifikasyonu
- **Mekanizma:** Günlük rapor oluşturulmadan hemen önce, o gün açılan tüm pozisyonlar için `PriceFetchService` tetiklenir.
- **Akış:** `BIST/Crypto` ayrımı yapılarak güncel fiyat çekilir ve `DailyPnL` hesaplanır.
- **Sonuç:** Rapor artık "Tahmini" değil "Gerçekleşen" PnL üzerinden oluşturulur.

### 3. "Honest Stats, Curated Display" Algoritması
- **Back-end:** Başarı oranı ve Kar Faktörü hesaplanırken **tüm** sinyaller (Negatifler dahil) havuza alınır. (Dürüstlük).
- **Front-end:** Kullanıcıya sunulan liste (`Signals` listesi) filtrelenir ve **sadece en iyi 3** sonuç gösterilir. (Vitrin).
- **Etki:** Güvenilir istatistik + Moral bozmayan rapor.

---

## 🗺️ Gelecek Yol Haritası (Next Steps)

1. **Phase 4 Başlatılması (Autonomous Execution Loop):**
    - Cortex'in ürettiği stratejinin *doğrudan* operasyona dönüştürülmesi (Draft Operation oluşturma).
    - İnsan onayına sunma.

2. **Self-Healing & Maintenance:**
    - X (Twitter) arayüz değişikliklerine karşı `social_intel.py` scriptinin sürekli güncel tutulması.

---

## ⚠️ Dikkat Edilmesi Gerekenler (Technical Constraints)

- **WebView2 & Selenium:** X otomasyonu için hibrit bir yapı kullanılıyor. Bazı işlemler (Reply, Tweet) API yerine tarayıcı emülasyonu ile yapılıyor. Bu nedenle `social_intel.py` kritik önem taşır, UI değişikliklerinden etkilenir.
- **Thread Safety:** `SentinelService` ve `OperationManager` çoklu thread çalışır. UI güncellemeleri için `Invoke` kullanılmalıdır.
- **Telegram Polling:** `MainForm` içinde ayrı bir `System.Windows.Forms.Timer` ile (3 sn) polling yapılır. Long-polling performansı için ID takibi (`_lastProcessedUpdateId`) kritiktir.

---

**NOT:** Bu dosya, projeyi devralacak veya katkıda bulunacak diğer AI asistanlar için "Single Source of Truth" (Tek Doğruluk Kaynağı) niteliğindedir.













