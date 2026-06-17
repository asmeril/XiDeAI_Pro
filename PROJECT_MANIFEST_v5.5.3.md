# XiDeAI Pro - v5.5.3 (Üstat Modülü Vizyon Bağlam Geliştirmesi)

## Neler Değişti?
- **Tweet Metni İle Tablo Doğrulama:** Görsel tablolarda yer alan İngilizce başlıklar (örneğin "Volume") nedeniyle Vision yapay zekasının yanlış varsayımlara (Hacim yerine Yabancı Payı vs.) kapılmasının önüne geçildi. `ParseGuruTableFromImage` fonksiyonuna tweetin içeriği (`post.Content`) de parametre olarak aktarıldı.
- **Vision Prompt İyileştirmesi:** Görsel okuma esnasında yapay zekanın tablo türünü sadece görsele göre değil, eklenen tweet metnine göre sınıflandırması sağlandı. "Örneğin tweet metninde 'yabancı payı' deniyorsa, tabloda 'Volume' yazsa bile bu bir Yabancı Payı tablosudur" talimatı eklendi.
- **CS0136 Derleme Hatası Çözümü:** `SignalEngine.cs` içindeki isim çakışması (`tweetUrl` vs `postUrl`) giderilerek hata düzeltildi.
