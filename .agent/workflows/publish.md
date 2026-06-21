---
description: XiDeAI Pro — Build, Publish, Deploy & Git Workflow
---

## 0. Pre-Flight Checklist (ZORUNLU — yayınlamadan önce tik at)

### Kod Kalitesi
- [ ] Derleme hatası yok: `get_errors` ile tüm değiştirilen dosyalar kontrol edildi
- [ ] Yeni `.cs` dosyası varsa `PROJECT_INDEX.md` → Services Map tablosuna eklendi
- [ ] Yeni metot/özellik varsa `PROJECT_INDEX.md` → Key Classes & Methods güncellendi
- [ ] Yeni sürüm için `PROJECT_MANIFEST_vNEW_VER.md` oluşturuldu veya mevcut manifestte neden ek bölüm kullanıldığı açıklandı

### iDeal Entegrasyon Kontrolleri
- [ ] `C:\iDeal\TARAMA_LOG\Market_Status.txt` → format: `datetime|MOD|YON|GunlukDeg%|Score|XU030%|XU050%|XU100_Fiyat|VolKat`
- [ ] `C:\iDeal\TARAMA_LOG\Market_Pulse_Alarm.txt` → EOD_SNAPSHOT satırı 18:10-18:20 arasında yazılıyor
- [ ] `C:\iDeal\TARAMA_LOG\Market_Movers.txt` → bugüne ait tarihli, her 30 dk güncelleniyor
- [ ] `C:\iDeal\SembolListeleri\TeFo.txt` → mevcut (tüm robotlar bu yolu kullanıyor)

### Versiyon Kuralı
Format: `MAJOR.MINOR.PATCH`
- Normal güncelleme → sadece PATCH artar: `5.2.7` → `5.2.8`
- PATCH 9'a ulaşınca → MINOR artar, PATCH sıfırlanır: `5.2.9` → `5.3.0`
- MINOR ve PATCH **hiçbir zaman çift haneye (10+) çıkmaz**
- Büyük mimari değişiklik → MAJOR artar: `5.9.9` → `6.0.0`

### Script Kontrolleri
- [ ] `social_intel.py` → `get_top_gainers/losers` önce `Market_Movers.txt` deniyor, fallback bigpara
- [ ] `playwright_daemon.py` → `THREAD_EMOJI = "\U0001F9F5"` (mojibake değil)
- [ ] PromptManager.cs `GetMarketClosePrompt` → `nabizUyarilari` parametresi, encoding bozukluğu yok
- [ ] `PostingService.cs` → tüm production tweet/thread gönderimleri için canonical kapı; `/status/` URL ve `posted_count/total_chunks` doğrulaması zorunlu

---

## 1. Versiyon Güncelle

```powershell
# csproj'u güncelle (release.ps1 yoksa manuel)
Set-Location "d:\MEGA\XiDeAI_Pro"
(Get-Content XiDeAI_Pro.csproj -Raw) `
  -replace '<Version>.*?</Version>', '<Version>NEW_VER</Version>' `
  -replace '<AssemblyVersion>.*?</AssemblyVersion>', '<AssemblyVersion>NEW_VER.0</AssemblyVersion>' `
  -replace '<FileVersion>.*?</FileVersion>', '<FileVersion>NEW_VER.0</FileVersion>' |
  Set-Content XiDeAI_Pro.csproj -Encoding UTF8
```

---

## 2. Build, Setup & Deploy (release.ps1)

```powershell
Set-Location "d:\MEGA\XiDeAI_Pro"
.\release.ps1 -Version "NEW_VER" -Changelog "vNEW_VER: <degisiklik ozeti>"
```

Bu script şunları yapar:
1. csproj + setup.iss versiyon güncelleme
2. `dotnet publish` (win-x64, self-contained, single-file)
3. Scripts\ ve Config\ klasörlerini publish dizinine kopyalama
4. Inno Setup ile `Output\XiDeAI_vNEW_VER_Setup.exe` oluşturma
5. `Output\version.json` güncelleme

> **NOT:** Build çıktısı `Dist\publish\` altında toplanır, setup EXE `Output\` klasörüne yazılır.
> Inno Setup kurulu olmalı: `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`

### Production deploy (setup sonrası)

```powershell
Copy-Item "d:\MEGA\XiDeAI_Pro\Scripts\social_intel.py" `
  "C:\Program Files (x86)\XiDeAI Pro\Scripts\social_intel.py" -Force
Copy-Item "d:\MEGA\XiDeAI_Pro\Scripts\playwright_daemon.py" `
  "C:\Program Files (x86)\XiDeAI Pro\Scripts\playwright_daemon.py" -Force
```

---

## 4. Verify

```powershell
# EXE timestamp ve boyut
Get-Item "C:\Program Files (x86)\XiDeAI Pro\XiDeAI_Pro.exe" |
  Select-Object FullName, LastWriteTime, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}

# Versiyon bilgisi
(Get-Content "d:\MEGA\XiDeAI_Pro\XiDeAI_Pro.csproj" | Select-String "Version")[0]
```

---

## 5. Git Commit & Push

```powershell
Set-Location "d:\MEGA\XiDeAI_Pro"
git add .
git commit -m "feat: vNEW_VER - <degisiklik ozeti>"
git push origin master
```

---

## 6. Bilinen Sorunlar & Notlar

| Konu | Durum | Sürüm |
|---|---|---|
| Thread emoji mojibake (`Ã°Å¸Â§Âµ`) | ✅ Düzeltildi — `\U0001F9F5` unicode escape | v5.2.6 |
| `except Exception` → anında return (son tweet gitmiyor) | ✅ Düzeltildi — retry mekanizması | v5.2.7 |
| Compose-cleared `Exception` değil `PlaywrightTimeoutError` fırlatmalı | ✅ Düzeltildi | v5.2.7 |
| OperationEngine scheduler'da pulseAnomalies geçilmiyordu | ✅ Düzeltildi — nabizUyarilari | v5.2.8 |
| TeFo.txt yolu `D:\Projects\...` hardcoded | ✅ Düzeltildi — `C:\iDeal\SembolListeleri\TeFo.txt` | v5.2.8 |
| Market_Movers.txt NABIZ\ alt klasöründeydi | ✅ Düzeltildi — TARAMA_LOG\ ana klasörü | v5.2.8 |
| EOD penceresi 18:00-18:15 (veriler tamamlanmamış) | ✅ Düzeltildi — 18:10-18:20 | v5.2.8 |
| Kapanış verisi thread'de görünmüyordu (sadece internet kaynağı) | ✅ Düzeltildi — iDeal birincil kaynak + EOD_SNAPSHOT | v5.2.8 |
| Prompt encoding bozukluğu (? karakterleri) | ✅ Düzeltildi — unicode escape | v5.2.8 |
| "Pulse" ngilizce terim | ✅ Türkçeleştirildi — "Nabız Uyarısı / Anlık Kırılım" | v5.2.8 |
| Kanca (hook) tweet'i oluşmuyordu | ✅ Düzeltildi — KANCA KURALI prompt bölümü | v5.2.8 |
| WebView2 modal kapanınca false success | ✅ Düzeltildi — canonical PostingService + Playwright doğrulama | v5.3.0 |
| Guru/sabah/gün sonu farklı gönderim yolları | ✅ Düzeltildi — tüm production modüller tek doğrulanmış posting hattına yönlendi | v5.3.0 |
| Market_Movers hacim verisi thread'e girmiyordu | ✅ Düzeltildi — hacim parser + HACIM LIDERLERI prompt bölümü | v5.3.0 |
| Üstat paneli tek hoca/tek tablo tipi varsayımı | ✅ Çoklu üstat + Mehmet Atışay takas/AKD/BOFA analizi eklendi | v5.4.5 |

---

## 7. Kritik Dosya Haritası

| Dosya | Konum | Açıklama |
|---|---|---|
| `Market_Status.txt` | `C:\iDeal\TARAMA_LOG\` | Anlık XU100 durumu, her 5 dk |
| `Market_Pulse_Alarm.txt` | `C:\iDeal\TARAMA_LOG\` | Nabız alarmları + EOD_SNAPSHOT |
| `Market_Movers.txt` | `C:\iDeal\TARAMA_LOG\` | Yükselen/düşen top 20, her 30 dk |
| `TeFo.txt` | `C:\iDeal\SembolListeleri\` | Tarama sembol listesi |
| `playwright_daemon.py` | `Scripts\` | X thread gönderim motoru |
| `social_intel.py` | `Scripts\` | Piyasa veri çekme + bigpara fallback |
| `PromptManager.cs` | `Services\` | LLM prompt şablonları |
| `OperationEngine.cs` | `Services\` | Zamanlayıcı tabanlı operasyonlar |
| `Robot_XU100_Nabiz_Monitor.txt` | `d:\MEGA\Robots\` | iDeal robot — log dosyalarını üretir |
| Tweetlerde anlamsız cümle bölünmeleri ve tek cümle kalması | ✅ Düzeltildi — PackTweets ve \n önceliği eklendi | v5.5.5 |
| Bot konuları sadece trendlerle eşleşiyordu | ✅ Düzeltildi — Konular listesinden rastgele fallback eklendi | v5.5.5 |
| Etkileşim (Reply) Chrome pencereleri görünür açılıyordu | ✅ Düzeltildi — social_intel.py headless=True yapıldı | v5.5.6 |
| Spor tweetlerine alakasız sinema/film yorumları veriliyordu | ✅ Düzeltildi — PromptManager'a SPOR kategorisi eklendi | v5.5.6 |
| Yanıtlar çok uzun ve hep zorunlu soru (Peki ya sen?) ile bitiyordu | ✅ Düzeltildi — PromptManager EK KURALLAR revize edildi | v5.5.6 |
| SPOR kategorisi siniflandirma promptunda (KATEGORİLER) eksikti | ✅ Düzeltildi — GetCategoryDetectionPrompt'a SPOR satırı eklendi | v5.5.7 |
| Fenomenlere otomatik yanıt atılıyordu | ✅ Düzeltildi — Etkileşim kapatıldı, sadece tarama moduna alındı | v5.5.8 |
| Manuel analiz thread metni 8 parça ile sınırlandırılıp sonuna 'UI Raporu' kuyruğu ekleniyordu | ✅ Düzeltildi — Sınır 25'e çıkarıldı, kuyruk mesajı silindi | v5.5.8 |
| Manuel analizlerde 1) KISA ÖZET gibi numaralı başlıklar Twitter'a sızıyordu | ✅ Düzeltildi — ThreadPipeline'a Anti-Robotik Regex eklendi | v5.5.8 |
