# XiDeAI Pro v5.0.0 Release Manifest

## 📅 Release Date
29.05.2026

## 🎯 Focus
**🤖 v5.0.0 - Qwen3 Reasoning Suppression & Vision Stability**

## ⚠️ Critical Changes

- **`/no_think` Prefix (KRİTİK):** LM Studio'daki Qwen3 modeli `budget_tokens` parametresini yok sayıyordu ve tüm token kapasitesini chain-of-thought düşünmeye harcıyordu. `content` alanı boş dönüyor, "Provider returned empty vision response" hatası oluşuyordu. Artık her prompt başına `/no_think\n` eklenerek reasoning modu kapatılıyor.
- **Vision Timeout 600s:** Qwen3 vision analizleri 300s'de timeout alıyordu. Timeout 600 saniyeye çıkarıldı.
- **Sürüm Serisi:** `4.10.9` son `4.x` sürümüdür. Bu sürümden itibaren `5.x` serisi başlar.

## 🔧 Changes

### Services/AI/LMStudioProvider.cs
- Tüm metin prompt'larına `/no_think\n` prefix eklendi (Qwen3 chain-of-thought bastırma).
- Tüm vision prompt'larına `/no_think\n` prefix eklendi.
- `thinking` parametresi request body'den kaldırıldı (LM Studio zaten yok sayıyordu).
- Vision timeout: 300s → **600s**.
- Metin timeout: 180s → **300s**.
- `ExtractContentFromChoice()`: `content` boşsa `reasoning_content`'e fallback yapıyor.

### XiDeAI_Pro.csproj
- Version: `4.10.9` → `5.0.0`
- AssemblyVersion / FileVersion: `5.0.0.0`

### setup.iss
- MyAppVersion: `5.0.0`

## 📊 Verification
- `XiDeAI_Pro.exe`: ~68 MB (single-file, self-contained)
- `XiDeAI_v5.0.0_Setup.exe`: ~64 MB
- LM Studio log'da `reasoning_tokens: 0` veya çok düşük görünmeli.
- Vision analizi 600s içinde tamamlanmalı, `content` dolu dönmeli.

## 🔗 İlgili Commit
`9bafbe1` — release: v5.0.0 - /no_think prefix, 600s vision timeout, version bump
`942fa49` — fix: use /no_think prefix to suppress Qwen3 reasoning + raise timeouts to 300s/600s
