description: XiDeAI Pro — Ortam Bilgileri ve Kritik Yollar
---

# XiDeAI Pro - Çalışma Ortamı

> ⚠️ **NOT:** Sunucu kullanılmıyor. Her şey yerel PC'de çalışıyor (2026-07-30 itibarıyla).

## 📍 Kritik Yollar (Yerel PC)

| Açıklama | Yol |
|----------|-----|
| **AppData (Logs, Config, Data)** | `C:\Users\ttevf\AppData\Local\XiDeAI` |
| **Program Files (Kurulum)** | `C:\Program Files (x86)\XiDeAI Pro` |
| **Proje Kaynak Kodu** | `D:\Projects\XiDeAI_Pro` |
| **iDeal Log Dosyaları** | `C:\iDeal\TARAMA_LOG\` |
| **iDeal Sembol Listeleri** | `C:\iDeal\SembolListeleri\` |

## 📂 Önemli Alt Dizinler

### AppData İçeriği
- `Logs/` — Günlük log dosyaları (AI, News, System, Telegram, Twitter)
- `config.dat` — Uygulama ayarları
- `memory.json` — AI hafıza verileri
- `news_history.json` — Haber geçmişi
- `stats.json` — İstatistikler

### Log Dosyaları
Format: `Log_YYYY-MM-DD_{Category}.txt`
Kategoriler: AI, News, Signal, System, Telegram, Twitter, FanZone

## 🔍 Log Analizi İçin
```powershell
# Son AI loglarını göster
Get-Content "C:\Users\ttevf\AppData\Local\XiDeAI\Logs\Log_$(Get-Date -Format 'yyyy-MM-dd')_AI.txt" -Tail 50

# Bugünkü tüm hataları bul
Select-String -Path "C:\Users\ttevf\AppData\Local\XiDeAI\Logs\*.txt" -Pattern "dogrulanamadi|hata|error" | Select-Object -Last 20

# Thread sorunlarını bul
Get-Content "C:\Users\ttevf\AppData\Local\XiDeAI\Logs\Log_$(Get-Date -Format 'yyyy-MM-dd')_Twitter.txt" | Select-String "thread|paylas|dogrulanam|failed"
```

## 🚀 Script Hızlı Deploy (C# değişikliği yoksa)
```powershell
# Sadece Python scriptlerini güncelle (setup EXE gerekmez)
Copy-Item "D:\Projects\XiDeAI_Pro\Scripts\playwright_daemon.py" `
  "C:\Program Files (x86)\XiDeAI Pro\Scripts\playwright_daemon.py" -Force
Copy-Item "D:\Projects\XiDeAI_Pro\Scripts\social_intel.py" `
  "C:\Program Files (x86)\XiDeAI Pro\Scripts\social_intel.py" -Force
Write-Host "Scripts deployed."
```

> **NOT:** Yukarıdaki komutlar admin yetkisi gerektirir. Sağ tık → "Yönetici olarak çalıştır" ile PS açın.
> C# kodu değiştiyse Setup EXE kurulmalı: `D:\Projects\XiDeAI_Pro\Output\XiDeAI_vX.X.X_Setup.exe`
