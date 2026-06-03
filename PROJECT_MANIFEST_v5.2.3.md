# XiDeAI Pro - Project Manifest v5.2.3

**Release Date:** 2026-06-03
**Version:** 5.2.3
**Build:** Release / Full stabilization
**Setup:** Generated (XiDeAI_v5.2.3_Setup.exe in Output/)

---

## Bu Sürümde Ne Değişti? (v5.2.3)

### 1. LM Studio Reasoning / Boş İçerik Koruması
**Amaç:** Qwen reasoning modelinin `content` üretmeden tüm token bütçesini `reasoning_content` içinde tüketmesi nedeniyle boş analiz/thread oluşmasını engellemek.

- **`Services/AI/LMStudioProvider.cs`:** `finish_reason=length` artık publishable kabul edilmez; provider `null` döndürerek fallback yolunu tetikler.
- **`Services/AI/LMStudioProvider.cs`:** `content` boş, `reasoning_content` dolu ise eski “reasoning kurtarma” davranışı kaldırıldı. Chain-of-thought veya iç muhakeme hiçbir şekilde çıktı olarak kullanılmaz.
- **`Services/AI/LMStudioProvider.cs`:** OpenAI-compatible request gövdesine thinking kapatma parametreleri eklendi: `enable_thinking=false`, `reasoning_effort=none`, `chat_template_kwargs.enable_thinking=false`.
- **`Services/AI/LMStudioProvider.cs`:** Text isteklerinde token aralığı `800-4096`, vision isteklerinde `1200-4096` bandına çekildi; 8K/16K minimum token zorlaması kaldırıldı.

### 2. iDeal Sinyal DB Snapshot Parser
**Amaç:** `Sinyal_Log_Database.txt` canlı rewrite edildiğinde byte-tail okuyucunun satır ortasından başlayıp `ACSEL -> SEL`, `DEVA -> VA`, `AKGRT -> T` gibi bozuk semboller üretmesini engellemek.

- **`Services/LogFileWatcher.cs`:** Byte offset / tail mantığı kaldırıldı; her dosya değişiminde stabil snapshot okunuyor.
- **`Services/LogFileWatcher.cs`:** Başlangıçta mevcut açık sinyaller `LoadSeenKeys()` ile hafızaya alınır, eski satırlar tekrar tetiklenmez.
- **`Services/LogFileWatcher.cs`:** Yeni sinyal anahtarı `Symbol|Strategy|Period|Tarih|Durum`; dosya rewrite olsa bile aynı sinyal tekrar işlenmez.
- **`Services/LogFileWatcher.cs`:** Header, `KAPALI`, geçersiz strateji/durum ve bilinmeyen semboller watcher seviyesinde elenir.

### 3. Merkezi Sembol Normalizasyonu
**Amaç:** iDeal, TradingView, Yahoo ve X arama akışlarında aynı sembolün tek canonical biçimde kullanılması.

- **`Services/SymbolNormalizer.cs`:** Yeni merkezi servis eklendi.
- **`NormalizeSignalSymbol(rawSymbol)`:** `VIP'VIP-VAKBN`, `VIP-VIP-VAKBN`, `BIST:VAKBN` gibi varyantları canonical BIST sembolüne indirger.
- **`IsKnownBistSymbol(symbol)`:** `Config/symbols_bist.txt` üzerinden sembol doğrular; `SEL`, `VA`, `T` gibi kırpılmış hatalı sembolleri engeller.
- **`Services/SignalParser.cs`:** Strict parse artık `SymbolNormalizer` kullanır; sadece `ALPHA/PREMOVE` ve `AKTIF/PULLBACK_ADAY` satırları kabul edilir.
- **`Scripts/screenshot.py`:** Python tarafında da sembol normalizasyonu eklendi; bozuk VIP/prefix formatları Yahoo/TradingView’e taşınmaz.

### 4. SocialIntel Veri Kirliliği Temizliği
**Amaç:** Kendi bot çıktılarının, 404 sentinel kayıtlarının, duplicate tweet URL’lerinin ve yanlış engagement parse değerlerinin AI promptlarına girmesini engellemek.

- **`Scripts/social_intel.py`:** Türkçe X metrik parse düzeltildi: `B=bin`, `Mn=milyon`.
- **`Scripts/social_intel.py`:** `ERROR_404` / `ACC_NOT_FOUND` kayıtları artık veri olarak dönmez.
- **`Scripts/social_intel.py`:** Status URL canonical dedupe eklendi; aynı tweet aynı tarama içinde birden fazla kez kabul edilmez.
- **`Scripts/social_intel.py`:** Kendi hesap ve bot-output filtreleri eklendi: `Piyasa Görüşleri`, `Teknik Analizim`, `XiDeAI`, `Yatırım tavsiyesi değildir`.
- **`Services/SocialIntelService.cs`:** Subprocess çağrılarında `X_USER/X_PASS` ortam değişkenleri veriliyor; Python kendi hesabını filtreleyebiliyor.
- **`Services/SocialIntelService.cs`:** C# tarafında da `IsBadSocialResult()` ile kendi hesap, bot çıktısı, boş URL ve 404 sentinel kayıtları eleniyor.

### 5. X Publish Click Dayanıklılığı
**Amaç:** X composer/post butonu üstüne gelen overlay veya pointer-intercept hatalarında paylaşımın daha sağlam tamamlanması ve hata anında teşhis verisi bırakılması.

- **`Scripts/playwright_daemon.py`:** `_robust_click_publish()` eklendi.
- Click sırası: `Escape` ile overlay kapatma -> normal click -> `force=True` click -> JS click.
- Başarısız durumda `/tmp/xideai_*_click_fail.png` ekran görüntüsü bırakılır.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/AI/LMStudioProvider.cs` | Reasoning content publish engeli, `finish_reason=length` fail-fast, thinking kapatma parametreleri |
| `Services/LogFileWatcher.cs` | Snapshot tabanlı sinyal DB izleme, seen-key, strict watcher filtresi |
| `Services/SignalParser.cs` | Strict parse, `KAPALI`/header/bozuk sembol atlama |
| `Services/SymbolNormalizer.cs` | Yeni merkezi sembol normalizasyon ve BIST doğrulama servisi |
| `Services/SocialIntelService.cs` | Kötü sosyal sonuç filtreleme, Python'a X kullanıcı bilgisi geçme |
| `Scripts/social_intel.py` | Türkçe engagement parse, duplicate URL, own-account/bot-output, 404 sentinel filtreleri |
| `Scripts/screenshot.py` | Python tarafı sembol normalizasyonu |
| `Scripts/playwright_daemon.py` | Robust publish click fallback zinciri ve hata screenshot'ı |

---

## Doğrulama

- `python3 -m py_compile Scripts/social_intel.py Scripts/playwright_daemon.py Scripts/screenshot.py` geçti.
- `git diff --check` geçti.
- `.NET build` Linux ortamında `dotnet` bulunmadığı için çalıştırılamadı.
