# XiDeAI Pro v4.8.0 Release Manifest

## 📅 Release Date
18.03.2026

## 🎯 Focus
**Bug Fix: ChromeDriver Native Crash Fix (Chrome 145+ Uyumu)**

## ⚠️ Critical Changes
- **Chrome 145+ Uyumu:** `x_daemon.py` içindeki `robust_type_and_verify` fonksiyonunda kullanılan `document.execCommand('insertText', ...)` API'si Chrome 130+ sürümlerinde kaldırıldı. Bu, tweet gönderiminde native ChromeDriver crash'e (Post loop fail at 1) yol açıyordu. Yöntem sırası yeniden düzenlendi: artık birincil yöntem **clipboard** (Ctrl+V), ikincil yöntem **DOM node insertion** (execCommand kullanmayan JS), son çare ise **send_keys**.
- **Sürüm Numaralama Kuralı:** PATCH 9'a ulaştığında MINOR artar, PATCH 0'a sıfırlanır (4.7.9 → 4.8.0). Bu kural `/publish` workflow'una belgelendi.

## 🔧 Changes

### Scripts/x_daemon.py
- `robust_type_and_verify` fonksiyonu güncellendi:
  - `js_insert` (execCommand) → **Kaldırıldı** (Chrome 145'te native crash üretiyor)
  - Yeni yöntem sırası: `clipboard` → `js_native` → `sendkeys`
  - Clipboard yöntemine React state güncellemesi için dummy space+backspace eklendi

### .agent/workflows/publish.md
- Sürüm numaralama kuralı belgelendi (PATCH 9 → MINOR artar)

## 📊 Test Results
- `/TWEETLE` komutu 18.03.2026 03:13'te başarıyla 6 parçalı thread gönderdi (Sent: True)


