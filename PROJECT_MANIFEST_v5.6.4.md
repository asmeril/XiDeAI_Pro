# XiDeAI Pro - v5.6.4 Manifest

## Yapılan Güncellemeler ve Refaktör Çalışmaları

Bu sürümde, fenomen modülündeki spam kontrolü hatalarının giderilmesi, yerel veritabanı (crawled influencer tweets) önceliklendirmesi ve veritabanı dosyasının gereksiz büyümesini önlemek için otomatik budama (pruning) mantığı geliştirilmiştir.

---

### 1. Spam Cooldown Hatalarının Giderilmesi
- **Problem**: `MainForm.cs` içerisindeki 4 saatlik cooldown spam koruma kontrolü, robotun kendi attığı analizleri/threadleri kontrol etmek yerine yanlışlıkla taranan fenomen tweetlerini (`Memory.Recall`) sorguluyordu. Bu durum, eğer bir fenomen ilgili sembolden bahsetmişse robotun kendi analizini atlamasına ve boş listenin Gemini'a gönderilmesine neden oluyordu.
- **Çözüm**: 
  - `MemoryEngine.cs` sınıfına robotun son N saat içinde ilgili sembole analiz paylaşıp paylaşmadığını `_analysisMemory` üzerinden kontrol eden `HasRecentAnalysisPosted` metodu eklendi.
  - `MainForm.cs` içindeki cooldown kontrolü bu yeni metodu kullanacak şekilde güncellendi.

### 2. Yerel Veritabanı Arama Önceliği
- **Problem**: Her sembol taramasında doğrudan canlı X (Twitter) araması yapılıyor, bu da hem süreci yavaşlatıyor hem de X rate limitlerine takılma riskini artırıyordu.
- **Çözüm**:
  - `PerformInternalSearchAsync` metodu güncellenerek, arama işlemi başlamadan önce `Recall(symbol, 168)` ile son 1 haftaya ait verilerin yerel veritabanında (`KnowledgeBase.json`) bulunup bulunmadığı kontrol edilmektedir.
  - Eğer son 1 haftalık taze veri yerelde mevcutsa canlı arama yapılmadan doğrudan yerel veriler döndürülerek canlı X araması atlanmaktadır.

### 3. Otomatik Veritabanı Temizliği (Budama)
- **Problem**: `KnowledgeBase.json` dosyasındaki verilerin zamanla sonsuz büyüyerek sistemi yavaşlatması ve disk alanı kaplaması riski.
- **Çözüm**:
  - `SaveKnowledgeBase()` metodu içerisine otomatik budama filtresi eklendi.
  - Veritabanı kaydedilirken 10 günden eski (7 gün limit + 3 gün güvenlik marjı) tüm fenomen tweetleri otomatik olarak temizlenmekte ve veritabanı indeksi yeniden oluşturulmaktadır.

---

## Derleme ve Entegrasyon Durumu
- Yapılan değişiklikler sonrasında `dotnet build` komutu çalıştırılarak derleme başarıyla tamamlanmış ve 0 hata ile doğrulanmıştır.
