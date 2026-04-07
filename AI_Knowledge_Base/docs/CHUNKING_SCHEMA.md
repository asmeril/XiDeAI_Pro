# AI_Knowledge_Base Chunking Schema

Bu belge, yerel model eğitimi veya RAG indeksleme sırasında AI_Knowledge_Base içeriğinin nasıl parçalanacağını tanımlar.

## Goals

- domain anlamını korumak
- mimari bağlamı kaybetmemek
- retrieval sırasında yüksek isabet sağlamak
- uzun C# ve Python dosyalarında anlamsal bölünme yapmak

## General Strategy

### Markdown / Docs

Chunk boundary:
- `#`, `##`, `###` başlıkları
- tablo blokları
- madde listeleri

Hedef boyut:
- 400 ile 1200 token

Overlap:
- 80 ile 120 token

Metadata:
- title
- heading_path
- source_file
- trust_level
- doc_type

### C# Code

Chunk boundary:
- class başına
- method başına
- çok uzun methodlarda mantıksal blok başına

Hedef boyut:
- 80 ile 220 satır

Overlap:
- 10 ile 20 satır

Metadata:
- language: csharp
- class_name
- method_name
- source_file
- trust_level
- subsystem

Özel not:
- MainForm.cs gibi büyük UI/orchestration dosyalarında chunk'lar event handler veya bölgesel init fonksiyonları etrafında yapılmalı.

### Python Code

Chunk boundary:
- function başına
- class method grupları
- CLI entrypoint blokları

Hedef boyut:
- 60 ile 180 satır

Overlap:
- 8 ile 15 satır

Metadata:
- language: python
- symbol_name
- source_file
- trust_level
- script_role

## Recommended Subsystem Tags

Kullanılabilecek `subsystem` / `script_role` etiketleri:
- thread-publishing
- x-automation
- ai-generation
- news-processing
- signal-processing
- manual-analysis
- screenshot-capture
- config-management
- orchestration
- logging
- rag-tooling

## Special Handling Rules

### 1. Thread/Tweet Logic

Şu dosyalar daha küçük ve yüksek çözünürlüklü chunk'lanmalı:
- codebase/Services_ThreadService.cs
- codebase/Services_SocialIntelService.cs
- codebase/Services_GeminiService.cs
- docs/Config_TwitterThreadOtomasyonRehberi.md

Sebep:
- ürün davranışının en kritik ve hata hassas kısmı burasıdır.

### 2. Snapshot Warning

`codebase/` altındaki içerik için her chunk'a şu metadata eklenmelidir:
- freshness: historical-snapshot
- canonical_source: live-repo-may-differ

### 3. Debug Scripts

`Scripts_debug_*.py` için:
- default olarak ana retrieval indeksine düşük öncelik verilmeli
- gerekiyorsa ayrı bir `debug-corpus` koleksiyonuna alınmalı

## Example Metadata Object

```json
{
  "corpus": "xideai-pro",
  "source_scope": "codebase",
  "trust_level": "reference",
  "freshness": "historical-snapshot",
  "language": "csharp",
  "subsystem": "thread-publishing",
  "source_file": "codebase/Services_ThreadService.cs",
  "class_name": "ThreadService",
  "method_name": "PostSignalThread"
}
```

## Retrieval Recommendation

Sorgu tipine göre boost önerisi:
- thread/reply/publish/cookie/playwright/selenium: thread-publishing + x-automation
- analiz/prompt/gemini/model: ai-generation
- haber/news/flash/category: news-processing
- signal/scan/batch: signal-processing
