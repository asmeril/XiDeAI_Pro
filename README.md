# XiDeAI_Pro (v2.7.0)
**AI Powered Algorithmic Trading Assistant**

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

## 📁 Proje Yapısı

*   `Services/Engine/`: İş mantığını yöneten yeni beyin takımı (`SignalEngine`, `StatsEngine`...).
*   `Scripts/social_intel.py`: X otomasyonunu sağlayan Python ajanı.
*   `Logs/`: İşlem ve hata kayıtları.

**Detaylı bilgi için:** [TEKNIK_RAPOR.md](TEKNIK_RAPOR.md) dosyasını inceleyin.
