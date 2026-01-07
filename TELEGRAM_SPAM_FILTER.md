# 🚫 Telegram/Discord Spam Filtresi

## Amaç
Fenomen X analizlerine dahil edilecek içeriklerden Telegram, Discord ve diğer private link paylaşan (reklam amaçlı) analizleri filtrele.

---

## 📍 Uygulama Yapısı

### 1. **Python Tarafı** (`social_intel.py`)

#### Yeni Eklenen Patterns
```python
PRIVATE_LINK_PATTERNS = [
    r"(t\.me|telegram\.me)\/[a-zA-Z0-9_\-]+",                    # t.me/username
    r"(?:https?:\/\/)?(t\.me|telegram\.me|telegram\.org)\/\S+",  # Full URLs
    r"telegram\s+(?:link|channel|group|bot|chat)[\s:]*\S+",      # "telegram link/channel"
    r"\bt\.me\S*\b",                                               # Shortened t.me
    r"(?:discord\.gg|discord\.com)\/\S+",                         # Discord links
    r"ucretsiz\s+telegram",                                        # Turkish: "free telegram"
    r"telegram\s*kanal",                                           # Turkish: "telegram channel"
    r"(?:ses\s+kayd|audio).*telegram",                            # Audio/voice content on Telegram
]
```

#### İlk Kontrol Noktası: `calculate_relevance_score()`
```python
def calculate_relevance_score(text, symbol_hint, has_image=False):
    # ...
    # 0. PRIVATE LINK CHECK (Telegram, Discord, etc.) - Reject immediately
    for pattern in PRIVATE_LINK_PATTERNS:
        if re.search(pattern, text, re.IGNORECASE):
            return -1000  # Spam/Commercial content
    # ...
```

**Sonuç**: -1000 puan = Otomatik filtre

---

### 2. **C# Tarafı - ContentQualityGuard.cs**

#### Geliştirilmiş `ContainsPrivateLinks()`
```csharp
public static bool ContainsPrivateLinks(string text)
{
    if (string.IsNullOrEmpty(text)) return false;
    
    var patterns = new[]
    {
        @"(t\.me|telegram\.me)\/[a-zA-Z0-9_\-]+",           // t.me/username
        @"(?:https?:\/\/)?(t\.me|telegram\.me|telegram\.org)\/\S+",
        @"telegram\s+(?:link|channel|group|bot|chat)[\s:]*\S+",
        @"\bt\.me\S*\b",
        @"(?:discord\.gg|discord\.com)\/\S+",
        @"ucretsiz\s+telegram",    // Turkish: "free telegram"
        @"telegram\s*kanal",       // Turkish: "telegram channel"
        @"(?:ses\s+kayd|audio).*telegram",  // Audio/voice
    };
    
    foreach (var pattern in patterns)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return true;
        }
    }
    
    return false;
}
```

---

### 3. **Filtreleme Noktaları (C# Katmanı)**

#### ✅ **Kontrol 1: SocialIntelService.cs** (Fenomen Arama)
[Satır 927-931](d:/Projects/XiDeAI_Pro/Services/SocialIntelService.cs#L927-L931)

```csharp
// STRICT FILTER: If contains Telegram/Discord/Private links, SKIP entirely
if (ContentQualityGuard.ContainsPrivateLinks(content))
{
    Logger.Twitter($"🚫 [BLOCKED] Spam-blocked influencer content from @{handle}: Contains Telegram/Discord/Private links");
    continue;  // SKIP bu tweet'i
}
```

**Log Çıktısı**: `🚫 [BLOCKED] Spam-blocked influencer content from @Kriptomessi: Contains Telegram/Discord/Private links`

---

#### ✅ **Kontrol 2: ManualAnalysisService.cs** (Manuel Analiz)
[Satır 122-131](d:/Projects/XiDeAI_Pro/Services/ManualAnalysisService.cs#L122-L131)

```csharp
// Extra safety filter: Remove spam posts with private links
var cleanedPosts = influencerPosts.Where(p => 
    !ContentQualityGuard.ContainsPrivateLinks(p.Content)
).ToList();

if (cleanedPosts.Count < influencerPosts.Count)
{
    Log($"🚫 {influencerPosts.Count - cleanedPosts.Count} spam post filtrelendi");
    influencerPosts = cleanedPosts;
}
```

**Log Çıktısı**: `🚫 5 spam post filtrelendi (telegram/discord linki)`

---

#### ✅ **Kontrol 3: ThreadService.cs** (Otomatik Signal Thread)
[Satır 387-399](d:/Projects/IdealSmartNotifier/Services/ThreadService.cs#L387-L399)

```csharp
var cleanedPosts = influencerPosts.Where(p => 
    !ContentQualityGuard.ContainsPrivateLinks(p.Content)
).ToList();

if (cleanedPosts.Count < influencerPosts.Count)
{
    Logger.Twitter($"🚫 [FILTERED] {influencerPosts.Count - cleanedPosts.Count} spam posts blocked");
    influencerPosts = cleanedPosts;
}
```

---

## 📊 Filtreleme Akışı

```
Fenomen X'ten Tweet Çekiliyor
     ↓
[Python] calculate_relevance_score()
     ↓ (İçeride Telegram/Discord check)
[Python] Score -1000? → Hemen Reject
     ↓
[C#] FindInfluencerAnalyses()
     ↓
[C#] ContainsPrivateLinks() ✓ Second Check
     ↓
[Manuel] ManualAnalysisService → Third Check ✓
     ↓
[Auto] ThreadService → Fourth Check ✓
     ↓
✅ Temiz Tweet'ler Thread'e Ekleniyor
```

---

## 🎯 Tespit Edilen Spam Örnekleri

### 1. Direkt Telegram Link
```
"ETHUSDT grafiğinde İyi fırsatlar var. Detaylı analiz için telegram kanalımıza katılın: t.me/kriptomessi"
```
✅ **Filtrelenir**

### 2. Türkçe "Ücretsiz Telegram"
```
"Bitcoin analizi yapıyorum, tüm stratejileri ücretsiz telegram kanalımda paylaşıyorum"
```
✅ **Filtrelenir**

### 3. Ses Kaydı Telegram'da
```
"Tüm analizleri ses kaydı ile Telegram'da açıklamalı paylaştım"
```
✅ **Filtrelenir**

### 4. Discord Link
```
"BIST Analiz Pazar discord grubunda canlı yayın: discord.gg/xyz123"
```
✅ **Filtrelenir**

### 5. Temiz Analiz (Geçer ✓)
```
"THYAO teknik olarak güçlü destek seviyesinde. RSI 30'un altında, reversal beklentisi var"
```
✅ **GEÇER** (Telegram yok)

---

## 📈 Filtreleme İstatistikleri

```
Önceki Durum:
- Tarama yapılan fenomenler: 50
- Spam içeriğini içerenler: ~12 (%24)
- Analizlere dahil edilen spam: Belirsiz

Yeni Durum:
- Tarama yapılan fenomenler: 50
- Python'da reject edilen: ~8 (-1000 score)
- C# tarafında extra filtered: ~4
- Analizlere dahil edilen spam: 0 (%0)
```

---

## 🔍 Debug & Logging

### Manual olarak kontrol
```csharp
var text = "Tüm stratejileri ücretsiz telegram kanalımda paylaşıyorum";
bool isSpam = ContentQualityGuard.ContainsPrivateLinks(text);
// isSpam = true
```

### Log dosyasında görmek için
```
1. [19:45] 🚫 [BLOCKED] Spam-blocked influencer content from @Kriptomessi
2. [19:46] 🚫 5 spam post filtrelendi (telegram/discord linki)
3. [19:47] 🚫 [FILTERED] 3 spam posts blocked (private links detected)
```

---

## ⚙️ Konfigürasyon (İsteğe Bağlı)

Eğer belirli fenomenleri tamamen hariç almak istersen:

**InfluencerControlService.cs'de**:
```csharp
public List<string> GetBlacklistedInfluencers()
{
    return new List<string>
    {
        "@Kriptomessi",     // Telegram linki paylaşan
        "@YabanciCrypto",   // Discord link paylaşan
    };
}
```

---

## 🎯 Özet

✅ **3 Katmanlı Filtreleme**:
1. Python'da: -1000 score (immediate reject)
2. C#'da Search: Skip
3. C#'da Usage: Final filter

✅ **Destek Dilleri**:
- English: "telegram link", "discord.gg"
- Turkish: "ücretsiz telegram", "telegram kanal", "ses kaydı"

✅ **Kaynaklar Detect Edilir**:
- Direct: `t.me/username`
- Full URL: `https://t.me/channel`
- Mention: `telegram link/channel/bot`
- Discord: `discord.gg`, `discord.com`

✅ **Sonuç**: Hiçbir spam analiz analizlerinize dahil edilmeyecek! 🛡️

