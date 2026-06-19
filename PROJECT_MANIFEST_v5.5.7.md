# XiDeAI Pro - v5.5.7 Manifest

## Kritik Düzeltme (Hotfix)

### SPOR Kategorisi Sınıflandırma Promptuna Eklendi
- v5.5.6'da `GetSporReplyPrompt` metodu ve switch yönlendirmesi (`"SPOR" => GetSporReplyPrompt(...)`) eklenmişti ancak **AI'ın tweeti sınıflandırması için kullandığı `GetCategoryDetectionPrompt` içindeki KATEGORİLER listesine SPOR satırı eklenmemişti.**
- Bu eksiklik nedeniyle AI, spor/transfer tweetlerini sınıflandırırken SPOR seçeneğini göremediği için en yakın kategori olan `KULTUR_EGLENCE`'yi seçiyordu.
- `KULTUR_EGLENCE` promptu "Ben dizi, film ve sanat asistanıyım" dediği için bot spor tweetlerine *"Bu benim alanım değil, lütfen sinema içerikli bir tweet paylaşın"* gibi absürt yanıtlar veriyordu.

### Düzeltme Detayı
- `PromptManager.cs` → `GetCategoryDetectionPrompt()` metodundaki KATEGORİLER listesine şu satır eklendi:
  ```
  - SPOR: Futbol, basketbol, spor kulüpleri, transfer haberleri, Fenerbahçe, Galatasaray, Beşiktaş, Trabzonspor, maç sonuçları, spor gündemi
  ```

### Akış (Düzeltme Sonrası)
1. Tweet gelir → `GetCategoryDetectionPrompt` çalışır → AI artık `SPOR` döndürebilir
2. Switch `"SPOR" => GetSporReplyPrompt(...)` yönlendirir
3. Bot taraftar/spor yorumcusu kişiliğiyle doğal yanıt üretir ✅
