# XiDeAI Pro - v5.5.6 Manifest

## Yapılan Değişiklikler ve İyileştirmeler

### 1. Etkileşim (Reply) Modülünde Headless Chrome Entegrasyonu
- `social_intel.py` içerisindeki etkileşim (reply) ve beğeni işlemleri için kullanılan `undetected_chromedriver` altyapısı `headless=True` olarak güncellendi.
- Bot artık kullanıcılara (fenomenlere) otomatik yanıt atarken ekranda rahatsız edici Chrome pencereleri açmıyor ve arka planda analiz modülleri gibi "hayalet" (stealth) olarak çalışıyor.
- `playwright_daemon.py` sadece sıfırdan tweet ve thread atma işlemleri için canonical kapı olmaya devam ediyor.

### 2. PromptManager: Yeni Kategori "SPOR"
- Sistemde "SPOR" kategorisi bulunmadığı için bot, transfer ve maç (futbol, basketbol) hakkındaki tweetleri "KULTUR_EGLENCE" (Kültür Eğlence) olarak sınıflandırıyordu. Bu da spor tweetlerine sinema eleştirmeni edasıyla absürt yanıtlar verilmesine yol açıyordu.
- Sisteme **SPOR** kategorisi eklendi.
- Yeni `GetSporReplyPrompt` metodu oluşturularak bota; "Maç analizi, oyuncu performansı veya transfer duyumları üzerine doğal bir yorum yap. Tatlı bir rekabet veya objektif spor yorumculuğu tadında konuş." talimatı verildi. 

### 3. PromptManager: Organik ve Kısa Yanıt Kuralı (EK KURALLAR)
- Tüm kategorileri kapsayan "EK KURALLAR" bölümündeki 3. kural revize edildi.
- Önceden her yanıtın sonuna "Peki ya sen?" gibi zorunlu bir soru cümlesi ekleniyor ve yanıtlar blog yazısı gibi çok uzun (2-3 paragraf) tutuluyordu.
- **Yeni Kural:** Yanıtların maksimum 2 kısa cümle olması zorunlu kılındı. Sürekli mülakat yapar gibi her cümlenin sonunda soru sorulması ("Peki sence?" vb.) **KESİNLİKLE** yasaklandı. Yalnızca tartışmaya çok uygun konularda nadiren soru sorulması esnekliği getirildi. Bu sayede botun insanımsı ve organik tavrı artırıldı.

## Yayınlama Öncesi Kontroller (Pre-flight)
- [x] Tüm projeler derlendi (`dotnet build` ile test edildi, HATA: 0).
- [x] `publish.md` bilinen sorunlar tablosu güncellendi.
- [x] Bu manifest dosyası eklendi.
