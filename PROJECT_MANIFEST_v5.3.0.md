# XiDeAI Pro - Project Manifest v5.3.0

**Release Date:** 2026-06-06
**Version:** 5.3.0
**Build:** Source stabilization / canonical posting architecture
**Setup:** `Output/XiDeAI_v5.3.0_Setup.exe` after Windows publish

---

## Bu Sürümde Ne Değişti? (v5.3.0)

### 1. Canonical PostingService Mimarisi
**Amaç:** Üstat paneli, sabah motivasyon, sinyal, manuel analiz, haber ve gün sonu modüllerinin farklı gönderim yolları kullanmasını engellemek.

- **`Services/PostingService.cs`:** Yeni merkezi gönderim facade'u eklendi.
- Tüm modüller içerik üretmeye devam eder; gönderim ve doğrulama tek yerde yapılır.
- `PostTweetAsync` ve `PostThreadAsync` yalnızca gerçek X `/status/<id>` URL doğrulaması ile success kabul eder.
- Thread success için `posted_count >= expectedCount` ve `total_chunks >= expectedCount` zorunludur.

### 2. Eski WebView2 Internal Bridge Debug-Only Hale Getirildi
**Amaç:** Modal kapandı veya buton click döndü diye gerçek post oluşmadan başarı loglanmasını engellemek.

- **`Services/SocialIntelService.cs`:** `PostTweet` ve `PostThreadAsync` artık canonical olarak Playwright subprocess hattını kullanır.
- Internal `OnPostTweetRequested` / `OnPostThreadRequested` bridge'i canonical gönderim yolundan çıkarıldı.
- Playwright sonucu doğrulanamazsa üst modüller başarısızlık görür; yanlış success logu basılmaz.

### 3. Üstat Paneli ve Otomatik Guru Paylaşımı
**Amaç:** Üstat panelinde "thread yayınlandı" denmesine rağmen gerçekte paylaşım olmaması sorununu çözmek.

- **`MainForm.ApproveSelectedThread`:** Artık `PostingService.PostThreadAsync(..., "GuruPanel")` kullanır.
- Başarı mesajında gerçek tweet URL'si ve parça sayısı gösterilir.
- **Guru automation:** Otomatik paylaşım sonucu kontrol edilir; başarısızlık loglanır.

### 4. Sabah Motivasyon Tweet Retry Mantığı
**Amaç:** Sabah motivasyon tweeti başarısızken günlük işaretin erken atılması ve retry'nin engellenmesi sorununu çözmek.

- **`MainForm.PostMorningMotivation`:** `bool` döndürür; başarı sadece doğrulanmış X postundan sonra kabul edilir.
- `_tweetedToday` artık önce `MORNING_MOTIVATION_PENDING` kullanır.
- `MORNING_MOTIVATION` sadece gerçek başarıdan sonra eklenir.
- Spam key `MOTIVATION/DAILY` olarak standartlaştırıldı.

### 5. Gün Sonu Özeti: Yükselen/Düşen/Hacim Veri Akışı
**Amaç:** Gün sonu thread'inde yükselen/düşen tablosu ve hacim bilgisinin eksik kalmasını engellemek.

- **`Scripts/social_intel.py`:** `Market_Movers.txt` parser regex ile güçlendirildi.
- `YÜKSELENLER` ve `DÜŞENLER` bölümlerinden `Symbol`, `ChangePercent`, `Volume` alanları çıkarılır.
- `get_top_volume()` önce `Market_Movers.txt` birleşik listesini hacme göre sıralar; BigPara sadece fallback'tir.
- **`Services/GeminiService.cs` / `PromptManager.cs`:** `topVolume` ayrı prompt bölümü olarak gönderilir.
- Prompt'a `HACIM LIDERLERI (EN COK ISLEM GORENLER)` bölümü ve hacim liderleri tweet'i eklendi.
- `BuildStockTable` fiyat yoksa `-`, hacim varsa kompakt `M/B` formatı kullanır.

### 6. Playwright Doğrulama Kontratı Sertleştirildi
**Amaç:** Partial thread veya URL yakalanamayan tekil tweet'in success sayılmasını engellemek.

- **`Scripts/playwright_daemon.py`:** Tekil tweette `/status/` URL alınamazsa `error` döner.
- Partial thread artık `success` değil `error` döner.
- Başarılı thread sonuçları `tweet_url`, `posted_count`, `total_chunks` döndürür.

### 7. FanZone, Interaction ve Fenomen Yönetimi Stabilizasyonu
**Amaç:** Fenerbahçe/FanZone ve etkileşim modüllerindeki false success ve veri güncelleme hatalarını gidermek.

- **`Services/FanZoneService.cs`:** Kritik hesap taramasında tweet URL'si işlem öncesi `seen` yapılmaz; `ProcessTweet` tek dedupe noktasıdır.
- Like/RT ikonları yalnızca `LikeTweet` / `Retweet` sonucu `status=success` ise işaretlenir.
- **`Services/InteractionEngine.cs`:** Toplu etkileşim hedefleri artık `Influencer.Handle` üzerinden gönderilir; class-name string üretme hatası giderildi.
- **`MainForm.cs`:** Fenomen silme UI'ı `InfluencerControlService.DeleteInfluencer()` kullanır; kopya liste üzerinden silme hatası giderildi.

### 8. Telegram, Guru ve History Güvenliği
**Amaç:** Onay sonrası başarısız işlemlerin sessizce kaybolmasını ve geçmiş sekmesinde eksik kategori görünmesini engellemek.

- **Telegram `/ONAY`:** Reply sonucu kontrol edilir; başarısızsa pending etkileşim kaydı silinmez ve Telegram'a hata döner.
- **Guru persistence:** Kaynak tweet yalnızca otomatik paylaşım başarılıysa veya onay gridine draft eklendiyse processed işaretlenir.
- **`GuruPersistenceService.cs`:** `processed_guru_tweets.json` LocalAppData/XiDeAI altına taşındı.
- **History tab:** `FanZone`, `Trend`, `Bot`, `Operation`, `Error`, `Warning` filtreleri eklendi; async filtre yarışları request-id ile engellendi.
- **Manual Analysis:** Tweet butonu yalnızca başarılı analizde aktif olur; hata metni paylaşımı engellendi.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/PostingService.cs` | Yeni canonical gönderim servisi |
| `Services/SocialIntelService.cs` | Playwright-only canonical posting, verification enforcement |
| `Services/ThreadService.cs` | Sinyal/rapor threadleri PostingService'e yönlendirildi |
| `Services/NewsEngine.cs` | Haber thread gönderimi PostingService'e yönlendirildi |
| `Services/SignalEngine.cs` | Batch/generic signal posting PostingService'e yönlendirildi |
| `Services/TrendEngagementService.cs` | Trend tweetleri PostingService'e yönlendirildi |
| `Services/FanZoneService.cs` | FanZone dedupe ve like/RT success kontrolü düzeltildi |
| `Services/InteractionEngine.cs` | Toplu etkileşimde gerçek handle listesi gönderimi |
| `Services/GuruPersistenceService.cs` | Processed guru dosyası LocalAppData'ya taşındı |
| `Services/SignalPersistenceService.cs` | Sinyal dedupe anahtarı strategy/status/tarih ile genişletildi |
| `Services/Logger.cs` | Log timestamp formatı yıl içerecek şekilde güncellendi |
| `Services/TwitterService.cs` | Doğrulanmamış API fallback success kapatıldı |
| `MainForm.cs` | Üstat, motivasyon, gün sonu, manuel/haber UI gönderimleri PostingService'e yönlendirildi |
| `Scripts/playwright_daemon.py` | Tekil tweet ve partial thread success doğrulaması sertleştirildi |
| `Scripts/social_intel.py` | `Market_Movers.txt` parser, hacim fallback ve C# uyumlu JSON output |
| `Services/GeminiService.cs` | Gün sonu `topVolume` ayrı prompt parametresi |
| `Services/PromptManager.cs` | Hacim liderleri prompt bölümü ve thread yapısı |
| `Services/OperationEngine.cs` | Rapor formatında hacim ve fiyat-yoksa `-` gösterimi |
| `XiDeAI_Pro.csproj`, `setup.iss`, `version.json` | Sürüm `5.3.0` |
| `release.ps1`, `build_cmd.ps1`, `.agent/workflows/publish.md` | Release workflow stale version ve paketleme riskleri giderildi |

---

## Doğrulama

- `python3 -m py_compile Scripts/social_intel.py Scripts/playwright_daemon.py` geçti.
- `git diff --check` geçti.
- `.NET build` Linux ortamında `dotnet` bulunmadığı için çalıştırılamadı; Windows build gereklidir.

---

## v5.3.1 Ek Stabilizasyon Notları

- Haber pending eşiği 9+ olarak ayarlandı; 7-8 skorlar `SKIPPED_LOW_SCORE` olarak history'ye düşer, onay kuyruğunu şişirmez.
- Son dakika / breaking haberler skor 9+ ise otomatik yayınlanır; skor 10 çok kritik haberler otomatik yayınlanır.
- Farklı kaynaklardan gelen aynı haberler için token tabanlı başlık benzerliği eklendi.
- Manuel analizde UI artık thread olarak yayınlanacak metni gösterir; ThreadService ekstra header/footer eklemeden PostingService ile paylaşır.
- Manuel analiz mention'ları sadece doğrulanmış X postlarından gelir; mention yapılan hesabın kaynak tweet URL'si prompt'a eklenir.
- LM Studio native vision input formatı `type=text` + `type=image` olarak düzeltildi.
- Sabah motivasyon prompt'u 120-220 karaktere çekildi ve Playwright single-post için Ctrl+Enter fallback eklendi.
- AutoBenchmark varsayılan olarak kapatıldı (`EnableAutoBenchmark=false`) ve üretim Gemini quota gürültüsü azaltıldı.
- Telegram `/STATUS` kuyruk sayılarını gösterir, `/HELP` komutu eklendi, `/TWEETLE` canonical PostingService kullanır.

---

## v5.3.2 Etkileşim Modülü Notları

- Bot Etkileşim tabı yalnız checkbox'a bağlı pasif moddan çıkarıldı; manuel `Şimdi Tara`, `BIST Fenomen`, `Kripto Fenomen`, `Durum` kontrolleri eklendi.
- Timer checkbox'a bağlı kalır; manuel UI/Telegram taraması checkbox kapalıyken de tek seferlik çalıştırılabilir.
- Filtreleme gevşetildi: adaylar takipçi veya etkileşim veya relevance veya içerik derinliği kriterlerinden biriyle listeye girebilir.
- Aynı taramada en iyi 3 viral aday için AI yanıt önerisi üretilip Telegram ve UI grid'e düşer.
- Telegram komutları eklendi: `/BOTDURUM`, `/ETKILESIMTARA`, `/ETKILESIMTEST @handle`.
- Etkileşim memory artık öneri aşamasında değil, yanıt gerçekten gönderildikten sonra işaretlenir.

---

## v5.3.3 Etkileşim Güvenlik Notları

- Otomatik bot döngüsünden direkt Like/RT kaldırıldı; zamanlayıcı artık sadece onaylı reply adayı üretir.
- Manuel hedef fenomen Like/RT akışı en fazla 3 hedefle sınırlıdır.
- `interact_with_targets` yalnız son 6 saat içindeki, hedef hesaba ait, özgün tweetlerde aksiyon dener.
- Manuel hedef etkileşim yavaş moda alındı; varsayılan aksiyon sadece Like, otomatik RT kapalıdır.
- Reply adaylarında `X-User`, tarihsiz/eski tweet, status URL'siz sonuç ve bahis/casino/boosting benzeri spam içerikler elenir.
- Eski config değerleri yüklenirken bot tweet yaşı 12 saat, min etkileşim 100 olacak şekilde normalize edilir.

---

## v5.3.4 İçerik Kalite Notları

- Manuel analiz X paylaşımı artık sadece 4 parçalık `ShortThread` ile yapılır; detay rapor fallback'i kapatıldı. 20 parçalık manuel analiz thread'i engellendi.
- Sinyal threadleri en fazla 4 parçaya indirildi; promptlar kısa, seviye/teyit/risk odaklı hale getirildi.
- Sinyal sonuç tweetlerinde `Beğen + RT` çağrısı kaldırıldı.
- Gün sonu özeti 7 tweetlik dramatik format yerine 4 tweetlik factual kapanış formatına çekildi.
- Gün sonu özeti paylaşımında 4 parçalık üst sınır ve YTD/hashtag güvenliği eklendi.

---

## v5.3.5 Analiz Dili ve Telegram Parite Notları

- Analiz kimliği `usta trader / piyasa kurdu / finans fenomeni` personasından çıkarılıp sade `seviye + teyit + risk` diline çekildi.
- Yerel fenomen örneklerinden çıkarılan gözleme göre kısa cümle, sayı/seviye, düşük emoji ve az hashtag önceliklendirildi.
- Manuel kısa thread promptunda FOMO, gizem, clickbait ve rol yapma dili yasaklandı.
- Telegram `/ANALIZ SYMBOL [PERIOD] [BASIS]` artık UI ile aynı `TradingViewChartId` akışını kullanır; üçüncü argüman baz seçimi olarak çalışır.
- Telegram dinamik analiz mesajlarında Markdown parse hatası olursa mesaj otomatik plain-text olarak tekrar gönderilir.

---

## v5.3.6 Haber ve Etkileşim Cevap Kalitesi Notları

- Haber promptlarının üstüne sade haber editörü guardrail'i eklendi: olay, kaynak, olası etki; clickbait/FOMO/takip/RT çağrısı yok.
- Haber threadleri en fazla 3 tweet ile sınırlandı ve son tweet haber özeti/YTD güvenliği taşır.
- Flaş haber promptundaki takip/bildirim/RT çağrısı kaldırıldı.
- Etkileşim botu kategoriye göre `gönül elçisi`, `dert ortağı`, `kafa dengi` gibi personas kullanmaz; tüm cevaplar nötr, kısa ve ölçülü editör tonuna çekildi.
- Reply üretimi `SKIP` dönerse öneri/aksiyon oluşturulmaz; hassas, alakasız, küfürlü veya belirsiz tweetlere cevap verilmez.

---

## v5.3.7 Telegram Haber Onay Bildirimi Notları

- Haber onay mesajları uzun Markdown formatından çıkarıldı; kısa, düz metin ve karar odaklı formata alındı.
- Başlık, kaynak, özet, gerekçe ve link alanları kırpılıp Markdown bozabilecek karakterlerden temizlenir.
- `/ONAYHABER ID` ve `/REDHABER ID` komutları düz metin olarak gösterilir; Telegram parse hatası riski azaltıldı.

---

## v5.3.8 Haber Onay Gürültüsü Notları

- Telegram haber onay spamini kesmek için normal skor 9 haberler artık onaya düşmez.
- Normal skor 9 haberler `SKIPPED_REVIEW` olarak history'ye yazılır; istenirse sonradan incelenebilir.
- Sadece skor 10 veya breaking/son dakika skor 9+ haberler otomatik paylaşım yoluna girer.
- Eski config `MinNewsImportance < 10` ise yüklemede 10'a normalize edilir.

---

## v5.3.9 Manuel Paylaşım Güvenliği Notları

- Tek tweet paylaşımı 280+ karakterde artık otomatik thread'e çevrilmez.
- Manuel analizde uzun detay rapor tek tweet butonundan 19-20 parçalık thread'e dönüşemez.
- Manuel analiz paylaşım butonu sadece geçerli `ShortThread` üretildiyse aktif kalır.
- Thread paylaşımı yalnız kullanıcı açıkça `Zincir (Thread)` seçerse yapılır ve 4 parça güvenlik kontrolünden geçer.

---

## v5.4.0 Manuel Thread Format Notları

- IdealSmartNotifier analiz yapısından alınan `Fiyat Durumu / Teknik Röntgen / Destek-Direnç / Oyun Planı` yaklaşımı manuel short-thread promptuna uyarlandı.
- Manuel analiz paylaşımı artık 4-8 tweet arası olabilir; 4 tweet'e zorlanmaz.
- İlk 2 tweet kısa özet ve devam rehberi olarak çalışır; sonraki tweetler seviye, teyit, risk ve kaynak sentezini sıkı paketler.
- 120 karakterden kısa, yarım cümle veya Markdown başlığı taşıyan parçalar geçersiz sayılır.
- Amaç: 19 parçalık ham rapor yerine 4-8 parçalık okunabilir X thread'i üretmek.

---

## v5.4.1 Sinyal Durum ve Tekrar Akışı Notları

- Sinyal threadlerinde ham `AKTIF` ve `PULLBACK_ADAY` ifadeleri takipçiye gösterilmez.
- `AKTIF` → `Sinyal canlı, teyit aranıyor`; `PULLBACK_ADAY` → `Geri çekilme takibi, acele yok` olarak yazılır.
- Sinyal promptlarında `patlama yakında`, `likidite avı`, `duyum` ve iç durum kodları yasaklandı.
- Aynı sembol için son 7 günde thread varsa tekrar gelen sinyal detaylı analiz açmaz; önceki analize atıf yapan 1-2 tweetlik pekiştirme paylaşır.
- Tekrar sinyal spam kontrolü sembol cooldown yerine genel paylaşım temposuna bakar, böylece kısa pekiştirme yapılabilir ama hesap spamlenmez.

---

## v5.4.2 Etkileşim Aday Kalitesi Notları

- Otomatik etkileşim taraması yalnız `FINANS` kategorisinde çalışır; gündelik/mizah/kültür/milli/motivasyon tweetleri otomatik reply dışına alındı.
- Aday tweetlerde finans niyeti zorunlu hale geldi; yalnız uzun içerik veya relevance skoru yeterli değildir.
- Promo, giveaway, airdrop, ödül, RT/like çağrısı, bonus ve kampanya içerikleri hard-block edilir.
- Takipçi sayısı parse edilemezse Telegram'da `0 takipçi` yerine `takipçi sayısı okunamadı` gösterilir.
- Etkileşim onay komutları tıklanınca ID kaybolmasın diye `/ONAY_ID` ve `/RED_ID` formatına alındı; eski `/ONAY ID` formatı da desteklenir.

---

## v5.4.3 Üstat Paneli Guardrail Notları

- Üstat panelinde hocaya ve taramasına saygı ölçülü biçimde dile getirilir; analiz bağımsız seviye/teyit/risk planı olarak kurulur.
- Thread içinde sadece `ConfigManager.GuruHandle` mention edilebilir; diğer tüm `@mention` ifadeleri temizlenir.
- Kaynak tarama tweet URL'si thread içinde zorunludur; eksikse otomatik eklenir.
- Üstat otomatik paylaşımı 3-6 parça ve kaynak URL kontrolünden geçmezse iptal edilir.
- `Smart Money`, `likidite avı`, `muazzam`, `efsane`, `nokta atışı`, `usta işi` gibi abartılı/persona ifadeleri temizlenir.

---

## v5.4.4 Sinyal Tablosu ve Üstat Paneli UX Notları

- Sinyal takip tablosunda `Saat` kolonu `Tarih/Saat` olarak değiştirildi.
- Canlı ve geçmiş sinyaller artık `dd.MM HH:mm` formatında görünür.
- Sinyal tablosundaki `Durum` kolonu gerçek sinyal durumunu takipçi dostu metinle gösterir; eski kayıtlarda durum yoksa `Durum kaydı yok` yazılır.
- Performance history kayıtlarına `Durum` alanı eklendi.
- Üstat panelinde canlı akış alanı küçültülerek onay/önizleme bölümü büyütüldü; onay listesi üstte, geniş görsel+metin önizleme altta gösterilir.
- Üstat paneli taslak/yayın/red geçmişi `guru_history.json` dosyasına kaydedilir ve `Geçmiş` butonundan görüntülenebilir.

---

## v5.4.5 Çoklu Üstat ve Takas Analizi Notları

- Üstat panelinde çoklu hoca seçimi eklendi: `@EFELERiiNEFESi3` ve `@matisay67` varsayılan kaynaklardır.
- Mehmet Atışay `@matisay67` takas/yabancı payı/AKD/BOFA analizleri için ayrı üstat kaynağı olarak eklendi.
- Görsel tablo parse promptu artık teknik tarama ile takas/yabancı payı tablolarını ayırır.
- Takas tablolarında analiz adayı seçimi yabancı payı, fiili dolaşım oranı/lotu, BOFA son 2 AKD farkı ve belirgin ayrışma gerekçesine göre yapılır.
- Üstat analizi seçilen hocaya göre mention yapar; yalnız seçili hoca mention edilebilir ve kaynak tarama tweet URL'si zorunludur.
- Takas verisinin tek başına al/sat sinyali olmadığı, fiyat/hacim/kapanış teyidi gerektiği prompt seviyesinde zorunlu hale getirildi.
