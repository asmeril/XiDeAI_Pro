# AI_Knowledge_Base Training Manifest

## Purpose

Bu klasör, XiDeAI Pro için yerel yapay zeka model eğitimi, ince ayar, RAG indeksleme ve davranış kalibrasyonu amacıyla korunur.

Bu içerik canlı uygulama koduyla aynı şey değildir. Bazı dosyalar güncel referans, bazıları tarihsel snapshot, bazıları ise kavramsal eğitim materyalidir.

## Dataset Classes

### 1. Gold Training Data

Yerel modelin ürün davranışı, domain dili ve kavramsal bağlam öğrenmesi için en yüksek öncelikli veri.

Kapsam:
- docs/PROJECT_INDEX.md
- docs/PROJECT_DIARY.md
- docs/PROJECT_MANIFEST_*.md
- docs/Config_TwitterThreadOtomasyonRehberi.md
- docs/Config_OrderBlock_FVG_MSB_KULLANIM.md
- docs/Config_IndicatorGuide.md
- docs/Config_symbols_*.txt

Kullanım:
- prompt engineering
- RAG source documents
- instruction tuning reference
- domain terminology alignment

### 2. Reference Code Snapshot

Canlı projeye yakın ama eğitim/karşılaştırma amaçlı saklanan kod referansı.

Kapsam:
- codebase/MainForm.cs
- codebase/Program.cs
- codebase/ConfigManager.cs
- codebase/Services_*.cs
- codebase/Scripts_*.py

Kullanım:
- code retrieval / code search
- architecture pattern learning
- regression comparison
- legacy behavior recovery

Not:
- Bu bölüm canlı kod ile drift edebilir.
- Fine-tuning için kullanılırken "historical snapshot" etiketiyle işlenmelidir.

### 3. Auxiliary / Tooling Data

Yardımcı üretim, RAG hazırlığı veya veri temizliği araçları.

Kapsam:
- codebase/Scripts_setup_rag.py
- codebase/Scripts_xideai_rag.py
- codebase/Scripts_clean_data.py
- codebase/Scripts_debug_*.py

Kullanım:
- veri hazırlama
- embedding pipeline hazırlığı
- deneysel analiz

## Recommended Metadata Tags

Her belge veya chunk için aşağıdaki metadata önerilir:

- corpus: xideai-pro
- source_scope: docs | codebase | tooling
- trust_level: gold | reference | auxiliary
- freshness: live-like | historical | unknown
- modality: documentation | csharp | python | config
- intended_use: finetune | rag | retrieval | evaluation

## Inclusion Rules

Şunlar korunmalıdır:
- domain terminolojisini taşıyan tüm rehber dosyaları
- proje mimarisini anlatan indeks/manifest dosyaları
- strateji, thread, haber ve otomasyon mantığını gösteren kod snapshotları

Şunlar eğitime dikkatle dahil edilmelidir:
- debug scriptleri
- eski sürüm manifestleri
- drift etmiş UI satır referansları

Şunlar canlı gerçek kabul edilmemelidir:
- eski snapshot satır numaraları
- eski mimari kararları
- artık kullanılmayan servis ilişkileri

## Maintenance Policy

Bu klasör:
- silinmemeli
- otomatik temizlik kapsamında değerlendirilmemeli
- sadece bilinçli olarak güncellenmeli
- canlı repo refactor'larından bağımsız olarak korunmalı

## Current Intent

Kullanıcı bu klasörü yerel AI modeli eğitmek için kullanmaktadır.
Bu nedenle AI_Knowledge_Base, disposable backup değil, bilinçli bir training corpus olarak ele alınmalıdır.