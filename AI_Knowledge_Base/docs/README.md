# XiDeAI_Pro Knowledge Base
**Local AI Training and Reference Corpus**

Bu klasör canlı uygulamanın çalışma dizini değil; XiDeAI Pro için yerel model eğitimi, RAG, retrieval ve davranış referansı amacıyla tutulan bilgi tabanıdır.

Detaylı sınıflandırma ve kullanım kuralları için:
- TRAINING_MANIFEST.md
- DATASET_CATALOG.md
- CHUNKING_SCHEMA.md
- PROJECT_INDEX.md

XiDeAI, iDeal platformundan gelen sinyalleri sadece iletmekle kalmaz; onları **görür**, **hatırlar** ve **yorumlar**.

## 🚀 Yeni Özellikler (v2.7.0)

### 🧠 1. Tam AI Entegrasyonu (Phase 4)
*   **Grafik Okuma (Vision):** Bot artık grafiğin görüntüsünü analiz edip indikatör uyumsuzluklarını ve formasyonları kendi gözüyle tespit ediyor.
*   **Hafıza (MemoryEngine):** Geçmişte yaptığı analizleri hatırlıyor. "Geçen hafta yükseliş beklemiştim, gerçekleşti" diyerek kendi performansını takip ediyor.

### 👥 2. Influencer Sentezi (Social Intel)
*   Sadece teknik veriye değil, X (Twitter) üzerindeki güvenilir analistlerin yorumlarına da bakıyor.
*   **Kollektif Görüş:** Teknik analiz + Influencer yorumlarını harmanlayarak "Güçlü Al" veya "Riskli" kararı veriyor.

### ⚡ 3. Gelişmiş Motor (SignalEngine)
*   **Akıllı Kuyruk:** Piyasada panik/coşku anında gelen çoklu sinyalleri (10+ sinyal) tek tek atıp spam yapmak yerine, "Toplu Piyasa Raporu" olarak özetliyor.
*   **Hibrit Bot:** Twitter API limitlerine takılmadan, Selenium altyapısıyla (Web Modu) sınırsız paylaşım yapabiliyor.

---

## 🛠️ Kurulum

1.  Bu klasörde `dotnet run` komutunu çalıştırın veya `XiDeAI.exe`'yi açın.
2.  İlk açılışta `Settings` sekmesinden **Gemini API Key** girin.
3.  Twitter hesabınıza giriş yapmak için "Import Cookies" butonunu kullanın.

## 📁 Korpus Yapısı

*   `docs/`: Gold training data ve kavramsal referans belgeleri.
*   `codebase/`: Tarihsel kod snapshotları ve eğitim amaçlı kod referansı.
*   `docs/TRAINING_MANIFEST.md`: Veri sınıfları, bakım politikası ve önerilen metadata.
*   `docs/DATASET_CATALOG.md`: Dosya bazlı veri tier ve kullanım önerileri.
*   `docs/CHUNKING_SCHEMA.md`: Fine-tuning ve RAG için parçalama şeması.

## 🏷️ Veri Sınıfları

*   `Gold Training Data`: Rehberler, manifestler, indeksler, sembol listeleri.
*   `Reference Code Snapshot`: C# ve Python kod snapshotları.
*   `Auxiliary / Tooling Data`: RAG, temizleme ve debug scriptleri.

Bu klasör otomatik temizlikte silinmemeli ve disposable backup olarak görülmemelidir.
