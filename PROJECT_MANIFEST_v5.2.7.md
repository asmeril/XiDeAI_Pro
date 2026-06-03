# XiDeAI Pro - Project Manifest v5.2.7

**Release Date:** 2026-06-03
**Version:** 5.2.7
**Build:** Release / Thread Last-Tweet Fix
**Setup:** Generated (XiDeAI_v5.2.7_Setup.exe in Output/)

---

## Bu Sürümde Ne Değişti? (v5.2.7)

### 1. Thread Son Tweet Sorunu (10/10 Eksikliği)
`_post_reply_in_thread`'de "compose box still has text" kontrolü `Exception` fırlatıyordu. Genel `except Exception` bloğu ise attempt sayısına bakmadan anında `return {"status": "error"}` yapıyordu. Sonuç: son tweet hiç retry görmeden başarısız oluyordu.

**Fix:**
- "Compose box still has text" artık `PlaywrightTimeoutError` olarak fırlatılıyor → `except PlaywrightTimeoutError` bloğu yakaliyor → 3 deneme hakki.
- Her iki fonksiyonda da (`_post_single_tweet`, `_post_reply_in_thread`) `except Exception` artık `attempt == 3` kontrolü yapıyor.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Scripts/playwright_daemon.py` | Compose-cleared exception retry + genel exception retry |

---

## Doğrulama
- Syntax: `ast.parse()` OK
- Production kopyalandı: `C:\Program Files (x86)\XiDeAI Pro\Scripts\playwright_daemon.py`

**Release Date:** 2026-06-03
**Version:** 5.2.6
**Build:** Release / Thread Posting Engine Overhaul
**Setup:** Generated (XiDeAI_v5.2.6_Setup.exe in Output/)

---

## Bu Sürümde Ne Değişti? (v5.2.6)

### 1. `_click_publish` — Escape Kaldırıldı (Ana Düzeltme)
X compose ekranında Escape tuşuna basmak "Gönderiyi sil?" modalını açıyor ve post butonunu engelliyor. `_click_publish` artık doğrudan click → force click → JS click sırasını izliyor; Escape yalnızca tüm yöntemler başarısız olursa son çare olarak kullanılıyor.

### 2. `keyboard.insert_text()` Geçişi
`compose_box.fill()` React state'ini güncellemiyordu — bu yüzden post butonu `aria-disabled=true` kalıyor ve tıklansa bile tweet gönderilmiyordu. `_post_single_tweet` ve `_post_reply_in_thread` artık `keyboard.insert_text()` kullanıyor.

### 3. Compose-Cleared Doğrulaması
`_click_publish` sonrasında X'in compose sayfasından gerçekten ayrılıp ayrılmadığı kontrol ediliyor (10 saniye / 0.5s adımlar). Ayrılmadıysa hata fırlatılıp retry yapılıyor.

### 4. False-Positive URL Tespiti Engellendi (`min_id` Baseline)
`self._last_known_tweet_id` tracker eklendi. Her post öncesi mevcut en yüksek bilinen tweet ID'si kaydediliyor; `_extract_latest_tweet_url` yalnızca bu değerden büyük status ID'li URL'leri kabul ediyor.

### 5. DOM-First URL Tespiti (XHive Pattern)
Toast bekleme süresi sıfırlandı. Post sonrası önce mevcut sayfanın DOM'u `/status/` linki için taranarak profil sayfasına gidiş ihtiyacı minimuma indirildi.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Scripts/playwright_daemon.py` | Escape kaldırma, insert_text, compose-cleared check, min_id baseline, DOM-first URL tespiti |

---

## Doğrulama
- Syntax: `ast.parse()` ile doğrulandı (OK).
- Production kopyalandı: `C:\Program Files (x86)\XiDeAI Pro\Scripts\playwright_daemon.py`

---

## Bu Sürümde Ne Değişti? (v5.2.4)

### 1. Playwright Click Overlay Koruması (v5.2.4 Eklentisi)
**Amaç:** X üzerinde tweet gönderildikten sonra çıkan şeffaf veya görünür bildirim barlarının (toast/overlay) ve modal pencerelerin sonraki tweet'lerin (özellikle thread cevaplarının) atılmasını pointer-intercept hatasıyla engellemesini önlemek.

- **`Scripts/playwright_daemon.py`:** `_click_publish` fonksiyonu robust click mantığıyla güncellendi:
  1. `Escape` tuşuna basarak olası modal/bildirim pencerelerini kapatır.
  2. Normal click dener.
  3. Pointer engeli durumunda `force=True` (Playwright bypass) parametresiyle click dener.
  4. Hâlâ engelleniyorsa JS üzerinden doğrudan tıklama tetikler (`button.evaluate("el => el.click()")`).
- **`Scripts/playwright_daemon.py`:** `_post_reply_in_thread` metodundaki direct `post_btn.click` çağrısı robust `_click_publish` ile değiştirildi.

### 2. LM Studio Reasoning / Boş İçerik Koruması (v5.2.3)
- **`Services/AI/LMStudioProvider.cs`:** `finish_reason=length` artık publishable kabul edilmez; provider `null` döndürerek fallback yolunu tetikler.
- Thinking kapatma parametreleri eklendi: `enable_thinking=false`, `reasoning_effort=none`.

### 3. iDeal Sinyal DB Snapshot Parser (v5.2.3)
- **`Services/LogFileWatcher.cs`:** Byte offset / tail mantığı kaldırıldı; her dosya değişiminde stabil snapshot okunuyor ve deduplication yapılıyor.

### 4. Merkezi Sembol Normalizasyonu (v5.2.3)
- **`Services/SymbolNormalizer.cs`:** `VIP-VAKBN`, `BIST:VAKBN` gibi varyantları canonical BIST sembolüne indirger ve `Config/symbols_bist.txt` üzerinden doğrular.

### 5. SocialIntel Veri Kirliliği Temizliği (v5.2.3)
- **`Scripts/social_intel.py`:** Kendi hesap/bot çıktıları, 404 kayıtları elendi. Türkçe X metrik parse düzeltildi.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `Scripts/playwright_daemon.py` | Robust click yayınlama fallbacks, reply click stabilizasyonu |
| `Services/AI/LMStudioProvider.cs` | Reasoning suppress, token limits |
| `Services/LogFileWatcher.cs` | Snapshot tabanlı sinyal DB izleme, seen-key |
| `Services/SymbolNormalizer.cs` | Merkezi sembol normalizasyonu |
| `Services/ThreadService.cs` | Non-destructive regex sanitization |

---

## Doğrulama
- `.NET build` 0 Hata / 0 Uyarı ile Windows üzerinde derlendi.
- `ISCC.exe` ile `Output\XiDeAI_v5.2.4_Setup.exe` kurulum paketi başarıyla paketlendi.


