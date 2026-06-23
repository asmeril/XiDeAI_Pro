# 🤖 XiDeAI Pro - Proje Geliştirme Günlüğü

Bu günlük, proje üzerinde yapılan değişiklikleri, mimari kararları ve günlük ilerlemeyi takip etmek için tutulmaktadır.


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
