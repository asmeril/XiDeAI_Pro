# XiDeAI Pro - Sürüm 5.5.8 Manifestosu

## 1. Versiyon Bilgileri
- **Sürüm:** 5.5.8
- **Tarih:** 21 Haziran 2026
- **Ana Odak:** Fenomen Modülü Veri Madenciliği Optimizasyonu ve Manuel Analiz Thread İyileştirmeleri.

## 2. Yapılan Değişiklikler

### Fenomen Modülü (Influencer Control)
- Fenomenlere atılan otomatik cevap (Reply/Like) motoru tamamen devre dışı bırakıldı. Modül artık "Salt-Okunur" (Read-Only) tarama modunda çalışıyor.
- `_deepScanTimer` (45 dakikalık periyodik derin tarama) yeniden aktif edildi ve ana ekrandaki butona bağlandı.
- Tarama limiti kaldırılarak (Take 10 yerine sıralı 15'li tarama ile) her 45 dakikada tüm veritabanının gezilmesi (Round-Robin) sağlandı.
- Arayüze doğrudan veritabanına etki eden "Puanları Sıfırla (50)" butonu eklendi.
- Rutin taramalara %30 ihtimalle çalışan yeni nesil "Fenomen Keşif (Discovery)" mekanizması dahil edildi.

### Manuel Analiz (ThreadPipeline & PromptManager)
- 8 Tweet (maxTweets) sınırı kaldırılarak detaylı analizlerin kapasiteye takılmadan limitsiz bir şekilde (25 tweete kadar) yayınlanması sağlandı.
- ThreadPipeline içerisine gömülü olan "Devamındaki detaylar Telegram/UI raporunda..." yapay kuyruk mesajı sökülüp atıldı.
- `1) KISA ÖZET`, `2/8`, `2) GRAFİK OKUMA` gibi Vision modelinden sızan numaralı/robotik başlıkları Twitter'a gitmeden saliseler önce silen Anti-Robotik Regex Kalkanı eklendi.
- Yapay zekaya ilk tweetin dikkat çekici (Hook) olması ve her tweetin anlamsız yerlerden kırpılmaması için en az 260 karakter doluluğunda paketlenmesi yönünde kesin prompt komutları eklendi.

## 3. Güncellenen Dosyalar
- `Services/InfluencerControlService.cs`
- `Services/SocialIntelService.cs`
- `Services/PromptManager.cs`
- `Services/ThreadPipeline.cs`
- `MainForm.cs`

## 4. Yayınlama Notları
Sorunsuz deploy için release scripti çalıştırılacak ve git push işlemi yapılacaktır. Yeni özelliklerin tamamı UI üzerinden test edilmiştir.
