# XiDeAI Pro v5.2.3 — Yayınlama Rehberi (Publish Guide)

Bu doküman, **v5.2.3** sürümünün kararlılık güncellemelerini ve Windows yerel makinesinde kurulum paketi (installer) oluşturma adımlarını içerir.

---

## 📦 Kurulum Dosyaları (Artifacts)

| Dosya | Açıklama | Konum |
|---|---|---|
| **`XiDeAI_v5.2.3_Setup.exe`** | v5.2.3 Windows Kurulum Paketi | `Output\` |
| **`version.json`** | Güncel versiyon ve changelog dosyası | `Output\` ve Proje Dizini |

---

## 🛠️ Yayınlama Adımları (Nasıl Paketlendi?)

Uygulamanın Windows üzerinde otomatik derlenmesi ve paketlenmesi için şu komut kullanılmıştır:

```powershell
powershell -ExecutionPolicy Bypass -File .\release.ps1 -Version 5.2.3 -Changelog "v5.2.3: LM Studio reasoning suppression, LogFileWatcher snapshot db, SymbolNormalizer integration, SocialIntel filtering, Playwright media-first posting and regex sanitization"
```

### Script Neler Yaptı?
1. **Kill Switch:** Çalışan `XiDeAI` ve `ISCC` (Inno Setup) işlemlerini kilit olmaması için sonlandırdı.
2. **Version Bump:** `XiDeAI_Pro.csproj` ve `setup.iss` dosyalarındaki versiyonları otomatik `5.2.3` yaptı.
3. **Dotnet Publish:** Projeyi self-contained, no-trimming ve sıkıştırılmış tek dosya (`XiDeAI_Pro.exe`) olarak `Dist/publish` altına derledi.
4. **Clean Scripts:** Python bağımlılıklarını (`Scripts/`) kopyaladı; gereksiz screenshots ve `__pycache__` klasörlerini temizledi.
5. **Inno Setup (ISCC):** `setup.iss` dosyasını derleyerek `Output/XiDeAI_v5.2.3_Setup.exe` dosyasını üretti.
6. **Version Metadata:** `Output/version.json` dosyasını güncel tarih ve changelog ile oluşturdu.

---

## 🔗 Git Değişiklikleri ve Push Durumu

Tüm geliştirme dosyaları ve sürüm güncellemeleri Git repomuzun `master` dalına başarıyla push edilmiştir.

**Commit:** `v5.2.3: media-first posting + keyboard.insert_text + regex sanitization`
