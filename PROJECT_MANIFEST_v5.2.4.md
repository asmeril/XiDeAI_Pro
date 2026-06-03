# XiDeAI Pro - Project Manifest v5.2.4

**Release Date:** 2026-06-03
**Version:** 5.2.4
**Build:** Release / Full stabilization
**Setup:** Generated (XiDeAI_v5.2.4_Setup.exe in Output/)

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
