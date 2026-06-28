# XiDeAI Pro - v5.6.3 Manifest

## Yapılan Güncellemeler ve Refaktör Çalışmaları

Bu sürümde, v5.6.2 sürümünde yapılan Yapay Zeka Haber Modülü İzolasyonu geliştirilmesinin, `GeminiService.cs` içerisindeki isteklerin doğru `TaskType` ile yönlendirilmesini sağlayacak düzeltmeler ve entegrasyonlar gerçekleştirilmiştir.

---

### 1. Haber Görevlerinin ModelManager Task Tercihleri ile Uyumlu Hale Getirilmesi
- **Problem**: `v5.6.2` sürümünde `ModelManager.cs` içerisinde `TaskType.NewsAnalysis` ve `TaskType.NewsThreadGeneration` görevleri için Gemini modeline birinci öncelik verilmişti. Ancak `GeminiService.cs` tarafındaki tüm haber metotları hardcoded olarak `TaskType.GeneralAnalysis` görev tipini çağıran parametresiz `SendRequest(...)` fonksiyonunu kullanıyordu. Bu nedenle istekler her zaman yerel modele (`lm-studio`) gidiyordu ve Gemini öncelik kuralı devre dışı kalıyordu.
- **Çözüm**: 
  - `GeminiService.cs` içerisindeki `SendRequest`, `SendGeminiRestApiRequest` ve `SendMultimodalRequest` metotlarına `taskType` parametresi eklenerek varsayılan değeri `TaskType.GeneralAnalysis` yapıldı.
  - Haber takip ve analiz fonksiyonları güncellenerek ilgili istekleri doğru `TaskType` ile göndermeleri sağlandı:
    - `DetectNewsCategory` -> `TaskType.NewsAnalysis`
    - `AnalyzeNewsUnified` -> `TaskType.NewsAnalysis`
    - `GenerateNewsCategoryAnalysis` -> `TaskType.NewsThreadGeneration`
    - `AnalyzeNewsForThread` -> `TaskType.NewsThreadGeneration`
  - Bu sayede sistem, haber isteklerinde önce Gemini API'yi kullanıp, bir hata veya kota sınırı durumunda otomatik olarak yerel modele (LMStudio) fallback yapacak şekilde tasarlandı.

---

## Derleme ve Entegrasyon Durumu
- Yapılan değişiklikler sonrasında `release.ps1` scripti çalıştırılarak derleme başarıyla tamamlanmış ve Inno Setup kurulum dosyası üretilmiştir.
- `C:\Program Files (x86)\XiDeAI Pro\XiDeAI_Pro.exe` başarıyla güncellenmiş ve log kayıtları doğrulanmıştır.
