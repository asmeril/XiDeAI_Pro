# XiDeAI Pro - Project Manifest v5.4.5

**Release Date:** 2026-06-09
**Version:** 5.4.5
**Build:** Multi-guru takas analysis / guru panel source attribution
**Setup:** `Output/XiDeAI_v5.4.5_Setup.exe` after Windows publish

---

## Bu Sürümde Ne Değişti? (v5.4.5)

### 1. Çoklu Üstat Paneli

- Üstat paneli artık tek `GuruHandle` varsayımıyla sınırlı değildir.
- Varsayılan üstat kaynakları:
  - `@EFELERiiNEFESi3`
  - `@matisay67`
- Panel üstünde hoca seçimi yapılır; tarama, analiz, mention ve kaynak kuralları seçili hocaya göre çalışır.

### 2. Mehmet Atışay Takas Analizi Desteği

- `@matisay67` takas/yabancı payı/AKD/BOFA analizleri için özel üstat kaynağı olarak eklendi.
- AKD aracı kurum dağılımı, BOFA Bank of America olarak yorumlanır.
- Yabancı payı, fiili dolaşım oranı/lotu ve BOFA son 2 AKD farkı teknik analiz aday seçiminde kullanılır.

### 3. Tablo Türü ve Aday Seçimi

- Görsel tablo parse promptu artık tablo türünü ayırır:
  - Teknik tarama tablosu
  - Takas/yabancı payı/fiili dolaşım/AKD/BOFA tablosu
- Modelden en fazla 5 analiz adayı istenir.
- Her aday için kısa `Reason` gerekçesi alınır ve analiz bağlamına eklenir.
- Period bulunamazsa varsayılan `G` kullanılır.

### 4. Üstat Analiz Dili ve Kaynak Atfı

- Üstat analizleri seçili hocaya saygıyı ölçülü şekilde belirtir.
- Analiz bağımsız seviye/teyit/risk planı olarak kurulur.
- Thread içinde yalnız seçili hoca mention edilebilir.
- Kaynak tarama tweet URL'si zorunludur; eksikse otomatik eklenir.
- Takas verisinin tek başına al/sat sinyali olmadığı, fiyat/hacim/kapanış teyidi gerektiği prompt seviyesinde zorunlu hale getirildi.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|---|---|
| `ConfigManager.cs` | `GuruHandles` listesi ve varsayılan `@matisay67` eklendi |
| `MainForm.cs` | Üstat seçimi ComboBox, seçili hoca ile tarama/analiz, takas reason bağlamı |
| `Services/GeminiService.cs` | `ParseGuruTableFromImage` artık reason döndürür; takas tablo promptu eklendi |
| `Services/PromptManager.cs` | Üstat/takas analiz promptu ve kaynak/mention guardrail güncellendi |
| `PROJECT_INDEX.md` | v5.4.5 notları |
| `PROJECT_MANIFEST_v5.4.5.md` | Bu manifest |
| `.agent/workflows/publish.md` | Preflight manifest kontrolü güncellendi |
| `version.json`, `XiDeAI_Pro.csproj`, `setup.iss`, `build_cmd.ps1` | Sürüm `5.4.5` |

---

## Doğrulama

- `python3 -m py_compile Scripts/social_intel.py Scripts/playwright_daemon.py Scripts/x_daemon.py`
- `jq empty version.json`
- `git diff --check`
- `.NET build` Linux ortamında `dotnet` bulunmadığı için çalıştırılamadı; Windows build gereklidir.
