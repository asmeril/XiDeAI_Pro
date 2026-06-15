# XiDeAI Pro - Project Manifest v5.5.0

**Release Date:** 2026-06-16
**Version:** 5.5.0
**Build:** Tekrar Sinyal Mantığı & Fenomen Etiketleme Düzeltmeleri
**Setup:** `Output/XiDeAI_v5.5.0_Setup.exe` after Windows publish

---

## Bu Sürümde Ne Değişti? (v5.5.0)

### 1. Kısa Pekiştirme ve Atıflı Detaylı Analiz (Mükerrer Sinyal Revizyonu)
Mükerrer sinyal (Repeated Signal) mantığı zamana duyarlı hale getirildi:
- **Kısa Pekiştirme:** Önceki analiz son 2 gün içerisindeyse detaylı analize girilmeden yalnızca anlık destek/direnç tespiti (Gemini Vision ile chart üzerinden) yapılıp önceki analiz linkiyle kısa tweet atılır.
- **Atıflı Detaylı Analiz:** Önceki analiz 2 günden daha eskiyse, eski analizin içeriği ve tarihi yeni analizde Gemini promptuna yedirilir. Yapay zekaya "Eğer önceki analiz başarılı olmuşsa, hedefi veya desteği vurmuşsa bunu vurgula" kuralı verilerek hikaye sürekliliği sağlandı.

### 2. "Dost Meclisi X-User" Fenomen Etiketleme Hatası Çözümü
- Prompt içerisindeki "DOST MECLİSİ" ifadesi yapay zeka tarafından yanlış yorumlanıp fenomenleri etiketlemek yerine "Dost meclisindeki X-User" gibi genel ifadeler kullanılmasına neden oluyordu.
- Bölüm başlığı `FENOMEN GÖRÜŞLERİ (DOĞRULANMIŞ)` olarak değiştirildi.
- Yapay zekaya, fenomenlere değinirken doğrudan gerçek `@kullaniciadi` (handle) etiketini kullanması ve anlamsız isimler takmaması kuralı kesin bir dille eklendi.

### 3. Tweet Karakter Sınırı Güvenliği
- `OperationEngine.cs` içindeki thread paylaşımlarında tweet sınırlarını zorlayan taşmalara karşı karakter limiti 280'den 255'e düşürüldü. Python tarafında oluşan "phantom 5th tweet" (boşluk içeren son gereksiz tweet) problemi çözüldü.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/SignalEngine.cs` | 2 günlük mükerrer sinyal filtresi ve AI Vision ile anlık destek/direnç tespiti eklendi |
| `Services/PromptManager.cs` | Fenomen etiketleme kuralının (X-User / Dost Meclisi) düzeltilmesi sağlandı |
| `Services/OperationEngine.cs` | Karakter limiti güvenlik marjı düzeltmesi (255 chars) yapıldı |
| `Services/MemoryEngine.cs` | Geçmiş analizlerde `Url` alanının JSON'a kaydedilip okunması eklendi |
| `Services/ThreadService.cs` | Thread gönderildiğinde sonucun URL'sini geri döndürme (`tweet_url`) eklendi |
