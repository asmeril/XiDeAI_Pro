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


