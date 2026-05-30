# 📦 XiDeAI Pro — Project Manifest v5.0.1

**Release Date:** 2026-05-29  
**Version:** 5.0.1  
**Build:** Release / win-x64 / PublishSingleFile  
**Setup:** `Output/XiDeAI_v5.0.1_Setup.exe` (~64MB)

---

## 🎯 Bu Sürümde Ne Değişti?

### Düzeltme: Thread Reply — "Reply button disabled" Hatası

**Sorun:** v5.0.0'da Twitter thread gönderiminde part 2+ "Reply button disabled" hatasıyla başarısız oluyordu.

**Root Cause:**
- `fill()` metodu React state'ini tetiklemiyordu.
- Post butonu `aria-disabled="true"` kalıyordu.

**Çözüm (`Scripts/playwright_daemon.py` → `_post_reply_in_thread`):**
1. Reply butonu dene (`[data-testid="reply"]`)
2. Reply butonu yoksa → `compose/post?in_reply_to={tweet_id}` URL (XHive'dan alındı)
3. Compose box yoksa → prefilled URL ile son fallback
4. Metin yazma: `fill()` → başarısız olursa `type(delay=20)`
5. Post butonu: 10x0.5s retry döngüsü

---

## 📂 Değişen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `Scripts/playwright_daemon.py` | `_post_reply_in_thread` XHive-style rewrite |
| `XiDeAI_Pro.csproj` | Version: 5.0.1 |
| `setup.iss` | MyAppVersion: 5.0.1 |

---

## ✅ v5.0.0'dan Devralınan Özellikler

- `/no_think` prefix: Qwen3 reasoning token açlığı çözüldü
- Vision timeout: 600s (300s'den)
- Metin timeout: 300s (180s'den)
- `reasoning_content` fallback
- PublishSingleFile ~64MB setup

---

## 🧪 Test Kriterleri

- AI log: `[AI-LMStudio] 📸 ... /no_think aktif` + `✅ qwen/qwen3.6-27b vision analysis successful`
- Twitter log: Thread part 2, 3... hepsi `✅ success` — "Reply button disabled" yok
- Log path: `%LOCALAPPDATA%\XiDeAI\Logs\Log_YYYY-MM-DD_Twitter.txt`

