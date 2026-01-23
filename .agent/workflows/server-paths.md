---
description: Server paths and environment information for XiDeAI Pro
---

# XiDeAI Pro - Server Environment

## 📍 Critical Paths (Sunucu)

| Açıklama | Yol |
|----------|-----|
| **AppData (Logs, Config, Data)** | `G:\Diğer bilgisayarlar\Sunucu\XiDeAI (AppData)` |
| **Program Files (Kurulum)** | `G:\Diğer bilgisayarlar\Sunucu\XiDeAI Pro (Program Files)` |

## 📂 Important Subdirectories

### AppData İçeriği
- `Logs/` - Günlük log dosyaları (AI, News, System, Telegram, Twitter)
- `config.dat` - Uygulama ayarları
- `memory.json` - AI hafıza verileri
- `news_history.json` - Haber geçmişi
- `stats.json` - İstatistikler

### Log Dosyaları
Format: `Log_YYYY-MM-DD_{Category}.txt`
Kategoriler: AI, News, System, Telegram, Twitter, FanZone

## 🔍 Log Analizi İçin
```powershell
# Son AI loglarını göster
Get-Content "G:\Diğer bilgisayarlar\Sunucu\XiDeAI (AppData)\Logs\Log_$(Get-Date -Format 'yyyy-MM-dd')_AI.txt" -Tail 50

# Bugünkü tüm hataları bul
Select-String -Path "G:\Diğer bilgisayarlar\Sunucu\XiDeAI (AppData)\Logs\*.txt" -Pattern "❌|error|hata" | Select-Object -Last 20
```
