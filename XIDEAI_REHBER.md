# X'iDeAI - Kapsamlı Geliştirici Rehberi

> **Versiyon**: 2.7.0  
> **Son Güncelleme**: 18 Aralık 2025  
> **Platform**: .NET 8.0 Windows Forms (Self-Contained Single-File)

---

## 📋 İçindekiler

1. [Proje Genel Bakış](#proje-genel-bakış)
2. [Versiyon 2.6.0 Değişiklikleri](#versiyon-260-değişiklikleri)
3. [Klasör Yapısı](#klasör-yapısı)
4. [Build & Publish](#build--publish)
5. [Kurulum Dosyası (Installer)](#kurulum-dosyası-installer)
6. [Python Scriptleri](#python-scriptleri)
7. [UI Bileşenleri](#ui-bileşenleri)
8. [Servisler](#servisler)
9. [Konfigürasyon](#konfigürasyon)
10. [Gelecek Planları](#gelecek-planları)

---

## Proje Genel Bakış

**X'iDeAI** (eski adı: iDeal Smart Notifier), iDeal trading robotlarının ürettiği sinyalleri izleyen, yapay zeka ile yorumlayan ve X/Twitter'da paylaşan bir masaüstü uygulamasıdır.

### Temel Özellikler

| Özellik | Açıklama |
|---------|----------|
| **Sinyal Takibi** | King, Dip/Zirve, ANKA robotlarının loglarını canlı izler |
| **AI Tweet Oluşturma** | Gemini API ile heyecan verici tweet metinleri yazar |
| **Influencer Tarama** | X/Twitter'da belirlenen sorguyla influencer postlarını tarar |
| **Filtreleme** | Takipçi sayısı ve tarih bazlı filtreleme |
| **Spam Koruma** | Quiet hours, cooldown, günlük/saatlik limitler |
| **TradingView** | Kişiselleştirilmiş grafik linkleri |

---

## Versiyon 2.7.0 Değişiklikleri - AI ANALİZ DEVRİMİ 🚀

### 🆕 Büyük Özellikler

1. **Gelişmiş AI Analiz Sistemi**
   - Grafik okuma ve gösterge analizi (TradingView ekran görüntüleri üzerinden Gemini Vision)
   - Influencer veri sentezi (veritabanı + X araması)
   - Geçmiş analiz takibi (30 günlük hafıza, tutarlılık kontrolü)
   - Kişisel analist sesi (tutarlı tahmin tonu)

2. **Thread Formatı Standardizasyonu**
   - Tweet 1: Başlık + Grafik
   - Tweet 2: AI Analiz (Bölüm 1)
   - Tweet 3: AI Analiz (Bölüm 2)
   - Tweet 4: Influencer Alıntıları + Sonuç

3. **Analiz Hafıza Sistemi**
   - `analysis_history.json` (%LocalAppData%\XiDeAI\)
   - Sembol başına 10 kayıt, 30 gün tutma
   - Otomatik geçmiş karşılaştırma ve tutarlılık değerlendirme

4. **Premium Haber Formatı**
   - Mavi tik hesapları için 2 tweet formatı
   - `|||` ayracı ile bölümleme
   - 25k karakter limitinden tam faydalanma

### 🧠 AI Prompt İyileştirmeleri

- **Signal Analysis**: Artık grafik + influencer + geçmiş analiz kullanıyor
- **Past Analysis Context**: "Daha önce X tahmini yaptın, şimdi nasıl değerlendiriyorsun?"
- **Influencer Quotes**: Gerçek @handle'lar ile top 2 post alıntılama
- **Personal Voice**: "Sen deneyimli bir analistsin" ton kalibrasyonu

### 🔧 Teknik İyileştirmeler

- `GeminiService.SynthesizeInfluencerAnalyses()` - Tam veri sentezi
- `ThreadService.LoadAnalysisHistory()` / `SaveAnalysisHistory()` - JSON persistence
- `ThreadService.GetPreviousAnalysisContext()` - 7 gün içinde benzer fiyat aralığı kontrol
- `ThreadService.GetInfluencerQuotes()` - @handle etiketleme

---

## Versiyon 2.6.0 Değişiklikleri (Geçmiş)

### 🆕 Özellikler

1. **Single-File Publish** - 241 DLL → tek EXE (~72 MB)
2. **Python social_intel.py İyileştirmeleri** - Takipçi/tarih parse, base64 query
3. **Installer İyileştirmeleri** - Otomatik uninstall, Türkçe dil

### 🔧 Düzeltmeler

- UI filtreleri çalışıyor, kurulum klasörü temizlendi

---

## Klasör Yapısı

### Proje Klasörü (`d:\MEGA\IdealSmartNotifier`)

```
IdealSmartNotifier/
├── 📄 MainForm.cs              # Ana form (UI + iş mantığı)
├── 📄 MainForm.Designer.cs     # Form tasarımı (auto-generated)
├── 📄 Program.cs               # Entry point
├── 📄 ConfigManager.cs         # Ayar yönetimi
├── 📄 TextProgressBar.cs       # Özel progress bar kontrolü
│
├── 📁 Services/                # İş mantığı servisleri
│   ├── GeminiService.cs        # Gemini AI entegrasyonu
│   ├── TwitterService.cs       # X/Twitter API
│   ├── SignalWatcherService.cs # Log dosyası izleme
│   └── ...
│
├── 📁 Scripts/                 # Python automation scriptleri
│   ├── social_intel.py         # X tarama (Selenium)
│   ├── x_poster.py             # Tweet gönderme
│   └── helpers/
│
├── 📁 Config/                  # Varsayılan config şablonları
│
├── 📄 setup.iss                # Inno Setup script
├── 📄 version.json             # Versiyon bilgisi
├── 📄 IdealSmartNotifier.csproj
└── 📄 xideai_icon.ico          # Uygulama simgesi
```

### Kurulum Sonrası (`Program Files\X'iDeAI`)

```
X'iDeAI/
├── 🚀 XiDeAI.exe           # Ana uygulama (72 MB, single-file)
├── 📁 Config/              # Yapılandırma şablonları
├── 📁 Scripts/             # Python scriptleri
├── 📁 selenium-manager/    # Chrome driver yöneticisi
├── 📄 version.json
└── 🖼️ xideai_icon.ico
```

### Kullanıcı Verileri (`%AppData%\XiDeAI`)

```
XiDeAI/
├── 📄 config.dat           # Şifreli kullanıcı ayarları
├── 📄 cookies.json         # X oturum bilgileri
├── 📄 signal_history.json  # Sinyal geçmişi
└── 📄 InfluencerData.json  # Influencer önbelleği
```

---

## Build & Publish

### Gereksinimler

- .NET 8.0 SDK
- Visual Studio 2022 veya VS Code
- Inno Setup 6 (installer için)
- Python 3.10+ (scriptler için)

### Debug Build

```powershell
cd d:\MEGA\IdealSmartNotifier
dotnet build -c Debug
```

### Release Publish (Single-File)

```powershell
cd d:\MEGA\IdealSmartNotifier
dotnet publish -c Release -o "bin\Release\publish"
```

**csproj Ayarları** (otomatik uygulanır):
```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

### Tam Build + Setup Oluşturma

```powershell
# 1. Clean publish
cd d:\MEGA\IdealSmartNotifier
Remove-Item "bin\Release\publish\*" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish -c Release -o "bin\Release\publish"

# 2. Setup oluştur
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "setup.iss"

# Çıktı: Output\XiDeAI_v2.6.0_Setup.exe
```

---

## Kurulum Dosyası (Installer)

### setup.iss Özellikleri

| Özellik | Değer |
|---------|-------|
| **AppId** | `{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}` |
| **Publisher** | iDeAI Labs |
| **Diller** | İngilizce, Türkçe |
| **Sıkıştırma** | LZMA (solid) |

### Otomatik Uninstall

Installer, önceki sürümü tespit edip kullanıcıya sorar:
- **Evet** → Sessiz kaldırma, temiz kurulum
- **Hayır** → Üzerine yazma
- **İptal** → Kurulum iptal

**Korunan veriler**: `%AppData%\XiDeAI\` klasörü uninstall sırasında silinmez.

### Setup Dosyaları

```
Output/
├── XiDeAI_v2.6.0_Setup.exe  # Güncel kurulum (~72 MB)
├── XiDeAI_v2.5.5_Setup.exe  # Eski versiyon
└── IdealSmartNotifierSetup.exe  # Legacy
```

---

## Python Scriptleri

### social_intel.py

**Konum**: `Scripts/social_intel.py`

**Komutlar**:

```bash
# Influencer tarama (temel)
python social_intel.py search_influencer "BIST100 hisse"

# Base64 encoded query (Türkçe karakterler için)
python social_intel.py search_influencer "QklTVDEwMCBoaXNzZQ==" --base64

# Oturum kontrolü
python social_intel.py check_login
```

**Dönen JSON Formatı**:
```json
{
  "success": true,
  "posts": [
    {
      "username": "@trader123",
      "displayName": "Trader Pro",
      "content": "ASELS hedef 50 TL...",
      "followerCount": 15000,
      "postDate": "2025-12-17T10:30:00",
      "url": "https://x.com/trader123/status/..."
    }
  ]
}
```

### x_poster.py

**Konum**: `Scripts/x_poster.py`

**Komutlar**:

```bash
# Tweet gönder
python x_poster.py post "Tweet metni burada"

# Medya ile tweet
python x_poster.py post "Tweet" --media "resim.png"
```

### Cookie Yönetimi

Scriptler `%AppData%\XiDeAI\cookies.json` dosyasını kullanır.

**Oturum yenileme**: Uygulama içinden "X Giriş" butonuyla veya manuel cookie export.

---

## UI Bileşenleri

### MainForm.cs Ana Bölümler

| Tab | Açıklama |
|-----|----------|
| **Dashboard** | Anlık sinyal izleme, Auto Tweet toggle |
| **X Search** | Influencer tarama, filtreleme |
| **Settings** | API anahtarları, spam kuralları |
| **Logs** | Uygulama logları |

### Influencer Filtreleri

```csharp
// Minimum takipçi filtresi
int minFollowers = (int)numMinFollowers.Value; // Default: 1000

// Tarih filtresi
DateTime minDate = dtpMinDate.Value; // Default: bugün - 7 gün
```

**Not**: Bu filtreler `social_intel.py`'dan dönen `followerCount` ve `postDate` alanlarını kullanır.

---

## Servisler

### GeminiService.cs

Gemini API ile AI içerik üretimi.

**Temel Fonksiyonlar**:

```csharp
// Tweet metni oluşturma
var tweet = await GeminiService.GenerateTweetAsync(signal, context);

// Premium haber analizi (2 tweet format)
var analysis = await GeminiService.AnalyzeNewsImpact(article, isVerified);
// İçinde ||| ayracı varsa split et

// Influencer sentezi (v2.7.0 DEVRİM!)
var fullAnalysis = await GeminiService.SynthesizeInfluencerAnalyses(
    symbol, 
    influencerPosts,     // X araması + veritabanı
    grafikPath,          // TradingView screenshot
    priceContext         // Geçmiş analiz varsa
);
```

**v2.7.0 Yenilikleri**:
- `SynthesizeInfluencerAnalyses()` - Grafik Vision + influencer verisi + geçmiş analiz
- Past analysis parsing: `"Önceki tahmin: Yükseliş (25 TL)" → Tutarlılık kontrolü
- Premium news prompt: `|||` separator ile 2 tweet

### ThreadService.cs

Thread oluşturma ve Twitter'a gönderme.

**v2.7.0 Yapısı** (4 Tweet Format):

```csharp
// Tweet 1: Başlık + Grafik
var tweet1 = $"📊 {symbol} Teknik Analiz\n#BIST100 #{symbol}";
await PostWithMedia(tweet1, chartImage);

// Tweet 2-3: AI Analiz (GeminiService'ten dönen metin)
var analysis = await GenerateTechnicalAnalysis(symbol, price);
// AI analizi 2 tweete böl

// Tweet 4: Influencer Alıntıları + Sonuç
var influencerQuotes = GetInfluencerQuotes(influencerData);
// @handle'lar ile top 2 post

await SaveAnalysisHistory(symbol, price, analysis, prediction);
```

**Analiz Hafıza Sistemi**:

```csharp
// History yükle (uygulama başlangıcında)
_analysisHistory = LoadAnalysisHistory();

// Geçmiş analiz kontrol (7 gün + ±5% fiyat toleransı)
var previousContext = GetPreviousAnalysisContext(symbol, currentPrice);
if (previousContext != null) {
    // AI'a şu context ile gönder:
    // "Önceki analiz: [tarih] [fiyat] [tahmin] → şimdi ne diyor?"
}

// Yeni analizi kaydet
var entry = new AnalysisHistoryEntry {
    Date = DateTime.Now,
    Price = currentPrice,
    Analysis = aiOutput,
    Prediction = ExtractPrediction(aiOutput) // "Yükseliş" / "Düşüş"
};
```

**Dosya Konumu**: `%LocalAppData%\XiDeAI\analysis_history.json`

**JSON Yapısı**:
```json
{
  "ASELS": [
    {
      "Date": "2025-12-18T10:00:00",
      "Price": 48.50,
      "Analysis": "MACD pozitif, RSI 65...",
      "Prediction": "Yükseliş"
    }
  ]
}
```

### SocialIntelService.cs

X araması ve influencer veri toplama.

**v2.7.0 İyileştirmeleri**:
- Veritabanı öncelikli arama (InfluencerData.json)
- Bulunamazsa X'te gerçek arama (social_intel.py)
- Follower/date filtreleme

### TwitterService.cs

X API v2 entegrasyonu (OAuth 1.0a).

### SignalWatcherService.cs

iDeal robot log dosyalarını izler:
- `C:\iDeal\TradeLog\King_*.log`
- `C:\iDeal\TradeLog\DipZirve_*.log`
- `C:\iDeal\TradeLog\ANKA_*.log`

---

## Konfigürasyon

### version.json

```json
{
  "version": "2.6.0",
  "releaseDate": "2025-12-17",
  "features": [
    "Single-file publish",
    "Improved installer",
    "Follower/date filters"
  ]
}
```

### config.dat (Şifreli)

Kullanıcı ayarları AES-256 ile şifrelenir:
- API anahtarları (Gemini, Twitter)
- TradingView kullanıcı adı
- Spam kuralları
- UI tercihleri

---

## Gelecek Planları

### XiDeAI_Pro (d:\MEGA\XiDeAI_Pro)

**v2.7.0 senkronizasyonu tamamlandı!** Pro versiyon için hazır.

**Planlanan özellikler**:

1. **Sidebar Navigation** - Sol tarafta modern menü
2. **Dashboard Cards** - Metrik kartları (toplam sinyal, başarı oranı, analiz tutarlılığı)
3. **Fluent Design** - WinUI 3 benzeri görünüm
4. **Dark/Light Theme** - Tema desteği
5. **Real-time Charts** - Canlı grafik widgets
6. **Analysis History Viewer** - Geçmiş tahminleri görselleştirme
7. **Influencer Database Manager** - UI üzerinden kategorilere ekleme/düzenleme

---

## Hızlı Referans

### Sık Kullanılan Komutlar

```powershell
# Build
dotnet build -c Release

# Publish (single-file)
dotnet publish -c Release -o "bin\Release\publish"

# Setup oluştur
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "setup.iss"

# Python script test
cd Scripts
python social_intel.py check_login
```

### Önemli Dosyalar

| Dosya | Amaç |
|-------|------|
| `MainForm.cs` | Ana UI ve iş mantığı |
| `setup.iss` | Installer script |
| `IdealSmartNotifier.csproj` | Proje ayarları |
| `Scripts/social_intel.py` | X tarama |
| `version.json` | Versiyon bilgisi |

### Versiyon Güncelleme Checklist

✅ **v2.7.0 Tamamlandı**:
- [x] GeminiService - SynthesizeInfluencerAnalyses() ve premium news
- [x] ThreadService - Analysis history system (JSON persistence)
- [x] MainForm - Premium news posting logic (||| split)
- [x] version.json → 2.7.0
- [x] IdealSmartNotifier.csproj → 2.7.0
- [x] setup.iss → 2.7.0
- [x] XIDEAI_REHBER.md → 2.7.0 (bu dosya)
- [x] Build + Publish + Installer
- [x] XiDeAI_Pro senkronizasyonu

**Yeni Versiyon İçin Adımlar**:
1. Kod değişikliklerini yap
2. version.json güncelle (versiyon + changelog)
3. .csproj dosyasında `<Version>` güncelle
4. setup.iss'de `MyAppVersion` güncelle
5. MainForm başlığında versiyon numarasını güncelle
6. XIDEAI_REHBER.md'ye değişiklikleri ekle
7. `dotnet publish -c Release`
8. Inno Setup ile installer oluştur
9. XiDeAI_Pro'ya senkronize et (robocopy)

1. [ ] `version.json` - version alanı
2. [ ] `IdealSmartNotifier.csproj` - `<Version>` tag
3. [ ] `setup.iss` - `#define MyAppVersion`
4. [ ] `MainForm.cs` - Title bar (opsiyonel, version.json'dan okunur)
5. [ ] Publish + Setup oluştur
6. [ ] Test et

---

## Sorun Giderme

### "Follower count görünmüyor"
→ `social_intel.py` güncel mi kontrol et, `followerCount` alanı dönüyor mu?

### "Setup icon görünmüyor"
→ `setup.iss`'te `UninstallDisplayIcon={app}\xideai_icon.ico` olduğundan emin ol

### "DLL'ler hala görünüyor"
→ csproj'da `<PublishSingleFile>true</PublishSingleFile>` var mı kontrol et

### "Oturum süresi doldu"
→ Uygulama içinden "X Giriş" ile yeniden oturum aç

---

*Bu belge X'iDeAI v2.6.0 için hazırlanmıştır.*
