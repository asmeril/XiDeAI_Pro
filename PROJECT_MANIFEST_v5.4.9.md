# XiDeAI Pro - Project Manifest v5.4.9

**Release Date:** 2026-06-12
**Version:** 5.4.9
**Build:** Takas & AKD Analizi Entegrasyonu
**Setup:** `Output/XiDeAI_v5.4.9_Setup.exe` after Windows publish

---

## Bu Sürümde Ne Değişti? (v5.4.7 - v5.4.9)

### 1. BIST Takas ve AKD Analizi "Diğer" Kuralı (v5.4.9)
Borsa İstanbul dinamiklerine özel Takas ve AKD (Aracı Kurum Dağılımı) analiz yetenekleri sisteme kazandırıldı. `PromptManager.cs` içerisindeki fenomen promptlarına dinamik `takasRulesSection` eklendi.
- **T+2 Gecikmesi:** LLM artık takas verisinin 2 gün geriden geldiğini biliyor.
- **Diğer Kuralı:** AKD tablosundaki "Diğer Alıcı > Diğer Satıcı" (Dağıtım) ve "Diğer Satıcı > Diğer Alıcı" (Akümülasyon) kuralı eklendi.
- **Kurumsal/Bireysel Oran:** Yabancı takas (Citibank vb.) ve fon değişimlerini okuma yeteneği eklendi.

### 2. Haber RSS Kaynak Düzeltmeleri (v5.4.8)
Çalışmayan bazı global ve yerel RSS haber kaynakları güncellendi.
- Anadolu Ajansı, TRT Haber, CNBC, Kyodo News için URL'ler revize edildi.

### 3. Gelişmiş Piyasa Kapanış Senaryosu (v5.4.7)
- **EOD_SNAPSHOT Verisi:** iDeal'den gelen kapanış verisi genişletilerek `XGLD`, `USDTRY`, `BRENT` ve `XSLV` gibi global korelasyon varlıkları tabloya eklendi.
- **Hacim Karşılaştırması:** Günlük toplam işlem hacmi, son 10 günlük hareketli ortalama hacmiyle kıyaslanarak "Hacim Katı" olarak günlük rapora yansıtıldı.
- **Kompakt Thread Kontrolü:** Başarısız AI çıktılarını filtrelemek amacıyla 40 karakterden kısa tweet parçalarının elenmesi sağlandı.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/PromptManager.cs` | Takas ve AKD analizi için dinamik `takasRulesSection` eklendi |
| `Services/OperationEngine.cs` | EOD_SNAPSHOT üzerinden 22 alanlı global verilerin çekilmesi (v5.4.7) |
| `Services/ThreadPipeline.cs` | Kompakt thread kalite kontrolü (v5.4.7) |
| `PROJECT_DIARY.md` | v5.4.7 - v5.4.9 notları işlendi |
| `takas_akd_analizi_rehberi.md` | Yeni BIST araştırma/eğitim rehberi oluşturuldu |
