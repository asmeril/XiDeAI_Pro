# XiDeAI Pro - v5.5.2 (Üstat Modülü Takas ve AKD Geliştirmeleri)

## Neler Değişti?
- **Takas/AKD Veri Entegrasyonu:** `ParseGuruTableFromImage` fonksiyonu, tablolardan (özellikle Matisay analizlerinden) sadece sembolü değil, Takas, AKD, BofA net lotları ve RSI gibi tüm sütun verilerini çekecek şekilde güncellendi.
- **Klişelerin Yasaklanması:** `GetGuruHonoringThreadPrompt` içindeki yasaklı sözcüklere "Smart Money açısından", "efsane", "nokta atışı", "yine konuştu" gibi tabirler eklendi. AI artık ezbere kalıplar yerine tablodaki gerçek verilerle konuşmaya zorlandı.
- **Twitter Handle Çözümü:** JavaScript parser, `twitter.com/` yönlendirmelerini okuyamadığı için fenomen isimlerini `X-User` olarak etiketliyordu. Bu sorun Regex ve string split mantığının `twitter.com/` formatını da kapsayacak şekilde düzenlenmesiyle çözüldü.
