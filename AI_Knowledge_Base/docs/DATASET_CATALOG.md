# AI_Knowledge_Base Dataset Catalog

Bu katalog, AI_Knowledge_Base içeriğinin eğitim ve retrieval amaçlı nasıl sınıflandırılacağını tanımlar.

## Scope

Kök klasör:
- AI_Knowledge_Base/

Alt alanlar:
- docs/
- codebase/

## Dataset Tiers

### Gold

Amaç:
- domain instruction tuning
- terminology grounding
- behavior alignment
- product architecture understanding

Dosyalar:
- docs/README.md
- docs/TRAINING_MANIFEST.md
- docs/PROJECT_INDEX.md
- docs/PROJECT_DIARY.md
- docs/PROJECT_MANIFEST_v3.9.4.md
- docs/Config_TwitterThreadOtomasyonRehberi.md
- docs/Config_OrderBlock_FVG_MSB_KULLANIM.md
- docs/Config_IndicatorGuide.md
- docs/Config_symbols_bist.txt
- docs/Config_symbols_crypto.txt
- docs/Config_symbols_forex.txt
- docs/Config_symbols_commodities.txt
- docs/Config_symbols_indices.txt

Önerilen metadata:
- trust_level: gold
- source_scope: docs
- intended_use: finetune|rag
- freshness: historical-reference

### Reference

Amaç:
- code retrieval
- architecture comparison
- legacy behavior recovery
- implementation pattern learning

Dosya grupları:
- codebase/MainForm.cs
- codebase/Program.cs
- codebase/ConfigManager.cs
- codebase/TextProgressBar.cs
- codebase/PlatformSupport.cs
- codebase/Services_*.cs
- codebase/Services_AI_*.cs
- codebase/Services_Core_IModule.cs
- codebase/Scripts_social_intel.py
- codebase/Scripts_screenshot.py
- codebase/Scripts_lock_manager.py

Önerilen metadata:
- trust_level: reference
- source_scope: codebase
- intended_use: rag|retrieval|evaluation
- freshness: historical-snapshot

### Auxiliary

Amaç:
- veri hazırlama
- deneysel yardımcı araçlar
- RAG altyapı üretimi

Dosyalar:
- codebase/Scripts_xideai_rag.py
- codebase/Scripts_setup_rag.py
- codebase/Scripts_clean_data.py
- codebase/Scripts_create_test_lock.py
- codebase/Scripts_debug_*.py

Önerilen metadata:
- trust_level: auxiliary
- source_scope: tooling
- intended_use: preprocessing|evaluation
- freshness: unknown

## Recommended Exclusions From High-Trust Training

Bu dosyalar tamamen silinmemeli, ama yüksek güvenli instruction tuning corpus'una doğrudan karıştırılmamalıdır:
- debug scriptleri
- eski satır referansları içeren indeksler
- versiyon drift'i yüksek snapshot kodlar

## Suggested Chunk Priority

Öncelik sırası:
1. Gold docs
2. Thread/news/config rehberleri
3. Services_ThreadService.cs / Services_SocialIntelService.cs / Services_GeminiService.cs
4. MainForm.cs ve diğer büyük orchestration dosyaları
5. Auxiliary scriptler

## Practical Use

### Fine-Tuning Candidate Set
- docs/*.md
- docs/Config_*.md

### RAG Candidate Set
- docs/*
- codebase/Services_*.cs
- codebase/Scripts_*.py
- codebase/MainForm.cs

### Evaluation / Regression Set
- codebase/Services_ThreadService.cs
- codebase/Services_SocialIntelService.cs
- codebase/Services_GeminiService.cs
- codebase/Services_NewsEngine.cs
