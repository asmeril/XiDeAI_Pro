# TeFo RSI+MACD Gösterge Rehberi (AI İçin)

Bu belge, TradingView grafiklerindeki **tüm görsel öğelerin** ne anlama geldiğini açıklar.

---

## 📊 RSI + MACD TABLOSU (Ekran Alt Ortada)
Grafiğin alt ortasında **4 sütunlu bir tablo** görünür:

| Sütun | İçerik | Açıklama |
|-------|--------|----------|
| 1 | Gösterge Adı | RSI veya MACD |
| 2 | Değer | RSI: 0-100 arası, MACD: pozitif/negatif sayı |
| 3 | Ek Bilgi | RSI → OB/OS/MID, MACD → Histogram değeri |
| 4 | Divergence | Bull (Yeşil) / Bear (Kırmızı) / None (Gri) |

### RSI (22 Periyot) Yorumlama:
- **OB (Overbought):** RSI > 70 → Aşırı alım bölgesi, düşüş riski
- **OS (Oversold):** RSI < 30 → Aşırı satım bölgesi, yükseliş potansiyeli
- **MID:** 30-70 arası → Nötr bölge, net sinyal yok

### MACD (12/26/9) Yorumlama:
- **Pozitif MACD:** Yükseliş trendi güçlü
- **Negatif MACD:** Düşüş trendi güçlü
- **Yeşil Histogram:** Momentum artıyor (güçleniyor)
- **Kırmızı Histogram:** Momentum azalıyor (zayıflıyor)

### Divergence (Uyumsuzluk) Sinyalleri:
- **Bull (Yeşil):** Fiyat düşerken gösterge yükseliyor → **Güçlü yukarı dönüş sinyali**
- **Bear (Kırmızı):** Fiyat yükselirken gösterge düşüyor → **Güçlü aşağı dönüş sinyali**
- **None (Gri):** Divergence yok → Normal hareket

⚠️ **KRİTİK:** RSI ve MACD'nin aynı anda divergence göstermesi **çok güçlü sinyal**dir!

---

## 📈 HAREKETLİ ORTALAMALAR (Opsiyonel - Varsayılan Kapalı)
Aktif ise 6 farklı EMA çizgisi görünür (**linewidth=2**, AI Vision Optimized):

| Renk | Kod | Periyot | Anlam |
|------|-----|---------|-------|
| Yeşil | #00E676 | EMA 13 | Kısa vadeli trend (en hızlı) |
| Mavi | #2196F3 | EMA 21 | Kısa-orta vade geçiş noktası |
| Kırmızı | #FF5252 | EMA 55 | Orta vade trend belirleyici |
| Turuncu | #FF9800 | EMA 89 | Orta-uzun vade (Fibonacci sayısı) |
| Koyu Yeşil | #4CAF50 | EMA 144 | Uzun vade (Fibonacci sayısı) |
| Mor | #9C27B0 | EMA 233 | Çok uzun vade (Fibonacci sayısı) |

**Yorumlama:**
- Fiyat tüm MA'ların **üstünde** → Güçlü yükseliş trendi
- Fiyat tüm MA'ların **altında** → Güçlü düşüş trendi
- MA'lar **yukarı doğru sıralı** (13>21>55>...) → Yükseliş trendi
- MA'lar **aşağı doğru sıralı** → Düşüş trendi
- EMA 13-21 kesişimi → Kısa vade trend değişimi
- EMA 55-89 kesişimi → Orta vade trend değişimi

---

## 🔵 ARS (Adaptive Range Stop) - Turkuaz Çizgi
- **Renk:** Turkuaz (#00FFFF) - kalın çizgi
- **Mantık:** Dinamik stop-loss seviyesi (Faktör: 0.6)
- **Kullanım:**
  - Fiyat ARS **üstünde** → Long pozisyon güvenli
  - Fiyat ARS **altına** düşerse → Long stop, Short fırsatı
  - ARS çizgisinin **yönü** trend yönünü gösterir
  - ARS kırılımı **trend değişim** sinyalidir

---

## 📐 OTOMATİK TREND ÇİZGİLERİ (Diagonal Support/Resistance)

### Destek Çizgileri (Yeşil)
- **Renk:** #17FF27 (parlak yeşil)
- **Stil:** Solid/Dotted/Dashed (ayarlanabilir)
- **Etiket:** Fiyatın üstünde (label_upper_left)
- **Uzama:** Sağa doğru uzar (extend.right)
- **Maksimum:** 3 çizgi gösterilir
- **Anlam:** Fiyat bu çizgilere yaklaşınca destek bulur

### Direnç Çizgileri (Pembe)
- **Renk:** #FF77AD (pembe)
- **Stil:** Solid/Dotted/Dashed (ayarlanabilir)
- **Etiket:** Fiyatın altında (label_lower_left)
- **Uzama:** Sağa doğru uzar
- **Maksimum:** 3 çizgi gösterilir
- **Anlam:** Fiyat bu çizgilere yaklaşınca direnç görür

**Yorumlama:**
- Trend çizgisi **kırılımı** → Güçlü sinyal (alert var)
- Çizgiye **yaklaşma** → Test beklenir
- Çizgi **eğimi** → Trend gücünü gösterir

---

## 🔢 PİVOT SEVİYELERİ (Yatay Çizgiler + Sol Etiketler)

Grafiğin **sol tarafında** 3 farklı zaman dilimi için pivot seviyeleri görünür:

| Etiket | Anlam | Renk | Hesaplama |
|--------|-------|------|-----------|
| P-D, P-W, P-M | Pivot (Günlük/Haftalık/Aylık) | Mavi | (H + L + C) / 3 |
| S1, S2, S3 | Destek seviyeleri | Yeşil | P bazlı formüller |
| R1, R2, R3 | Direnç seviyeleri | Kırmızı | P bazlı formüller |

**Görsel Özellikler:**
- **Etiket Konumu:** Sol tarafta (label_right) - artık ekran dışında kalmaz
- **Çizgi Uzatma:** Sağa doğru uzar (extend.right) - tüm grafiği kaplar
- **Etiket Boyutu:** Ayarlanabilir (tiny/small/normal/large)
- **Etiket Arka Plan:** Siyah transparan kutu (okunabilirlik için)
- **Aylık Pivotlar:** Artık görünür ve okunabilir

**Traditional Pivot Formülleri:**
- **P (Pivot)** = (Önceki High + Low + Close) / 3
- **S1** = P × 2 - Önceki High
- **S2** = P - (Önceki High - Low)
- **S3** = P × 2 - (2 × Önceki High - Low)
- **R1** = P × 2 - Önceki Low
- **R2** = P + (Önceki High - Low)
- **R3** = P × 2 + Önceki High - 2 × Önceki Low

**Yorumlama:**
- Fiyat P **üstünde** → Yükseliş eğilimi (R seviyeleri hedef)
- Fiyat P **altında** → Düşüş eğilimi (S seviyeleri hedef)
- S seviyeleri **destek** olarak çalışır
- R seviyeleri **direnç** olarak çalışır
- Günlük (D), Haftalık (W), Aylık (M) pivotlar farklı etiketlerle gösterilir
- **Önemli:** Tüm pivot çizgileri sağa uzar, etiketler sol tarafta sabit kalır

---

## 🟩🟥 DESTEK DİRENÇ BÖLGELERİ (Renkli Kutular - ATR Bazlı)

### Aktif Direnç Bölgesi
- **Fiyat altındaysa:** Kırmızı/Turuncu kutu (#FF5252)
- **Fiyat üstündeyse:** Yeşil kutu (#4CAF50) → Kırılmış direnç artık destek oldu

### Aktif Destek Bölgesi
- **Fiyat üstündeyse:** Yeşil kutu (#4CAF50)
- **Fiyat altındaysa:** Kırmızı kutu (#FF5252) → Kırılmış destek artık direnç oldu

### Önceki Bölgeler (Previous Zones)
- **Önceki Destek:** Kırmızı transparan kutu
- **Önceki Direnç:** Yeşil transparan kutu

**Dinamik Hesaplama:**
- ATR (14 periyot) bazlı bölge genişliği
- Maksimum bölge boyutu: ATR × 2.5
- 25 bar lookback ile oluşturulur
- ATR hareketi × 1.0 onay gerektirir

**Yorumlama:**
- Bölge **içinde** fiyat → Konsolidasyon
- Bölge **kırılımı** → Güçlü hareket başlar
- Renk **değişimi** → Destek/Direnç rol değiştirdi
- Bölge **kalınlığı** → Volatilite göstergesi

---

## 🎯 FİBONACCİ DÜZELTMESİ (Opsiyonel - Varsayılan Kapalı)

Aktif ise kesikli yatay çizgiler ve seviye etiketleri görünür:

**Ana Seviyeler:**
- **0** → Trend başlangıcı (kırmızı)
- **0.236** → İlk düzeltme seviyesi
- **0.382** → Önemli düzeltme (altın oran bileşeni)
- **0.5** → Orta nokta (psikolojik seviye)
- **0.618** → **Altın oran** (en güçlü düzeltme)
- **0.786** → Derin düzeltme
- **1.0** → Trend bitiş noktası
- **1.618, 2.618, 3.618, 4.236** → Uzatma seviyeleri

**En Önemli Seviyeler:**
- **0.382, 0.5, 0.618** → Düzeltme alım/satım bölgeleri
- **1.618, 2.618** → Kar al seviyeleri (uzatmalar)

**Yorumlama:**
- Fiyat Fibo seviyesinde **bounce** → Güçlü reaksiyon
- **0.618'de** duruş → Trend devam edebilir
- **0.786'yı** geçerse → Trend zayıfladı
- Uzatma seviyelerinde **kar realizasyonu** artar

---

## ⚠️ ANALİZ YAPILIRKEN DİKKAT EDİLECEKLER

### 1. Önce Tabloya Bak (Alt Orta)
- RSI ve MACD değerlerini oku
- Divergence durumunu kontrol et
- Histogram yönünü incele

### 2. ARS Çizgisini Kontrol Et
- Fiyat ARS üstünde mi altında mı?
- ARS yönü nedir?

### 3. Trend Çizgilerini İncele
- Hangi destek/direnç çizgileri aktif?
- Fiyat çizgilere yakın mı?
- Kırılım var mı?

### 4. Pivot Seviyelerini Değerlendir
- Fiyat hangi pivot seviyeleri arasında?
- P üstünde mi altında mı?
- Hangi S/R seviyelerine yakın?

### 5. S/R Bölgelerini Gözlemle
- Fiyat hangi bölgede?
- Bölge rengi ne? (Yeşil/Kırmızı)
- Kırılım oldu mu?

---

## 🔥 GÜÇLÜ SİNYAL KOMBİNASYONLARI

### Güçlü Alım Sinyali:
```
RSI: OS (< 30) + Bullish Divergence (Yeşil)
MACD: Pozitif histogram + Bullish Divergence (Yeşil)
Fiyat: Yeşil destek bölgesinde
Trend: Yeşil destek çizgisinde bounce
ARS: Fiyat ARS'nin üstünde
Pivot: S1 veya S2 seviyesinde
```

### Güçlü Satım Sinyali:
```
RSI: OB (> 70) + Bearish Divergence (Kırmızı)
MACD: Negatif histogram + Bearish Divergence (Kırmızı)
Fiyat: Kırmızı direnç bölgesinde
Trend: Pembe direnç çizgisinde ret
ARS: Fiyat ARS'nin altına düştü
Pivot: R1 veya R2 seviyesinde
```

### Orta Vade Trend Değişimi:
```
MA'lar: EMA 55-89 golden/death cross
ARS: Yön değişimi
Pivot: P seviyesi kırılımı
S/R Bölgesi: Renk değişimi (yeşil→kırmızı veya tersi)
Fibonacci: 0.618 seviyesi kırılımı
```

---

## 📝 AI ANALİZ ŞABLONU

**Analiz yaparken şu formatı kullan:**

1. **Fiyat Konumu:** [Hangi bölgede, hangi seviyelerde]
2. **Gösterge Konsensüsü:**
   - RSI: [Değer] - [OB/OS/MID] - [Divergence durumu]
   - MACD: [Değer] - [Histogram] - [Divergence durumu]
3. **Teknik Seviyeler:**
   - ARS: [Fiyatla ilişki]
   - Pivotlar: [Hangi P/S/R seviyelerinde]
   - Trend Çizgileri: [Destek/Direnç durumu]
   - S/R Bölgeleri: [Renk, konum]
4. **Hareketli Ortalamalar:** [Varsa - fiyat ilişkisi]
5. **Fibonacci:** [Aktifse - hangi seviyelerde]
6. **Sonuç:** [Net strateji önerisi]

**Örnek:**
> "SASA 4.35 seviyesinde, yeşil destek bölgesi içinde. RSI 28 (OS) + Bullish Divergence, MACD negatif ama histogram yeşile dönüyor. Fiyat ARS (4.20) üstünde tutunuyor. Günlük S1 (4.30) desteği çalıştı. Yeşil trend çizgisi 4.25'te güçlü destek. → **Kısa vade alım fırsatı, hedef R1 (4.55), stop ARS altı (4.18)**"

---

## 🟦 ORDER BLOCK (Kurumsal Sipariş Bölgeleri)

Order Block, güçlü bir fiyat hareketinden hemen önce oluşan **son kararsızlık mumunun** oluşturduğu bölgedir. Kurumsal oyuncuların sipariş verdiği bölgeleri işaretler.

### Bullish Order Block (🟢 OB)
- **Renk:** Teal transparan (#00BFA5, 60% şeffaflık) - AI Vision Optimized
- **Border:** Opak Teal (#00BFA5, 0% şeffaflık)
- **Etiket:** "🟢 OB" (normal boyut, Teal arka plan)
- **Oluşum:** Düşüş trendi → Son kırmızı mum → Güçlü yeşil mum (ATR × 1.5)
- **Bölge:** Son kırmızı mumun Low-High aralığı
- **Anlam:** Kurumsal alım bölgesi, fiyat buraya dönerse **destek** bulabilir

### Bearish Order Block (🔴 OB)
- **Renk:** Turuncu transparan (#FF6D00, 60% şeffaflık) - AI Vision Optimized
- **Border:** Opak Turuncu (#FF6D00, 0% şeffaflık)
- **Etiket:** "🔴 OB" (normal boyut, Turuncu arka plan)
- **Oluşum:** Yükseliş trendi → Son yeşil mum → Güçlü kırmızı mum (ATR × 1.5)
- **Bölge:** Son yeşil mumun Low-High aralığı
- **Anlam:** Kurumsal satış bölgesi, fiyat buraya dönerse **direnç** görür

**Yorumlama:**
- Order Block'a **geri dönüş** → Test beklenir
- OB içinde **pivot** → Güçlü reaksiyon
- OB **kırılımı** → Trend zayıfladı
- Maksimum 5 OB gösterilir (en güncel olanlar)

---

## 💠 FVG (Fair Value Gap - Fiyat Boşluğu)

FVG, 3 mum arasında oluşan **fiyat dengesizliğidir** (imbalance). Piyasa bu boşlukları doldurmaya çalışır.

### Bullish FVG (FVG↑)
- **Renk:** Lime transparan (color.lime, 50% şeffaflık) - AI Vision Optimized
- **Border:** Opak Lime + **Solid çizgi** (dashed değil, width=2)
- **Etiket:** "FVG↑" (normal boyut, Lime arka plan)
- **Oluşum:** Mum 1 High < Mum 3 Low → Arada gap var
- **Bölge:** Mum 1 High ile Mum 3 Low arası
- **Anlam:** Yükseliş imbalance, fiyat geri dönüp bu boşluğu **doldurabilir** (destek)

### Bearish FVG (FVG↓)
- **Renk:** Fuşya transparan (color.fuchsia, 50% şeffaflık) - AI Vision Optimized
- **Border:** Opak Fuşya + **Solid çizgi** (width=2)
- **Etiket:** "FVG↓" (normal boyut, Fuşya arka plan)
- **Oluşum:** Mum 1 Low > Mum 3 High → Arada gap var
- **Bölge:** Mum 3 High ile Mum 1 Low arası
- **Anlam:** Düşüş imbalance, fiyat geri dönüp bu boşluğu **doldurabilir** (direnç)

**Yorumlama:**
- FVG'ye **yaklaşma** → Boşluk doldurma beklenir
- FVG içinde **reaksiyon** → Güçlü destek/direnç
- FVG **tamamen doldu** → Denge sağlandı
- Minimum gap boyutu: ATR × 0.3 (otomatik)
- Maksimum 5 FVG gösterilir

---

## ⚡ MSB (Market Structure Break - Piyasa Yapısı Kırılımı)

MSB, önceki swing high/low seviyelerinin kırılmasıyla **trend değişimini** işaretler.

> **⚠️ V2.0 Güncellemesi:** Artık MSB yerine **BOS** (Break of Structure) ve **CHOCH** (Change of Character) ayrımı yapılıyor. Detaylar için aşağıdaki BOS/CHOCH bölümüne bakın.

### Bullish MSB/BOS (⬆ BOS veya ⚡ CHOCH)
- **Çizgi:** Lime kalın (**width=3**) - AI Vision Optimized
- **Etiket:** "⬆ BOS" veya "⚡ CHOCH" (**large** boyut)
- **Oluşum:** Önceki swing high seviyesi yukarı kırıldı
- **Anlam:** Yükseliş trendi güçleniyor, **higher high** oluştu
- **Strateji:** Long pozisyon açma/tutma sinyali

### Bearish MSB/BOS (⬇ BOS veya ⚡ CHOCH)
- **Çizgi:** Fuşya kalın (**width=3**) - AI Vision Optimized
- **Etiket:** "⬇ BOS" veya "⚡ CHOCH" (**large** boyut)
- **Oluşum:** Önceki swing low seviyesi aşağı kırıldı
- **Anlam:** Düşüş trendi güçleniyor, **lower low** oluştu
- **Strateji:** Short pozisyon açma/tutma sinyali

**Yorumlama:**
- MSB/BOS + RSI Divergence → **Güçlü trend değişimi**
- MSB/BOS + Order Block → Kurumsal onay
- **CHOCH:** Trend yönü değişiyor (önemli!)
- Swing uzunluğu: 5 bar (varsayılan)

---

## 🚀 BREAKOUT / BREAKDOWN (Kırılım Sinyalleri)

Konsolidasyon (dar aralık) sonrası güçlü fiyat hareketlerini tespit eder.

### Bullish Breakout (🚀 BREAKOUT)
- **Görünüm:** Yeşil "🚀 BREAKOUT" etiketi + yeşil kesikli çizgi
- **Oluşum:** 20 barlık dar aralık (ATR × 2) → Yukarı güçlü kırılım
- **Koşul:** Close > Range High + Güçlü yeşil mum (> ATR)
- **Strateji:** Alım fırsatı, momentum başladı

### Bearish Breakdown (💥 BREAKDOWN)
- **Görünüm:** Kırmızı "💥 BREAKDOWN" etiketi + kırmızı kesikli çizgi
- **Oluşum:** 20 barlık dar aralık → Aşağı güçlü kırılım
- **Koşul:** Close < Range Low + Güçlü kırmızı mum (> ATR)
- **Strateji:** Satış fırsatı, momentum başladı

**Yorumlama:**
- Breakout + Hacim artışı → **Güvenilir sinyal**
- Breakout sonrası **pullback** → Giriş fırsatı
- Fake breakout → Hızlı geri dönüş (OB veya FVG'de)
- Range boyutu minimum ATR × 2 olmalı

---

## 💧 LİKİDİTE HAVUZLARI (Liquidity Pools - LIQ)

Likidite havuzları, **stop-loss emirlerinin** yoğun olduğu bölgeleri gösterir. Kurumsal oyuncular bu bölgeleri "avlamak" için kullanır.

### Görünüm:
- **Kutu:** Sarı transparan (color.yellow, 70%)
- **Etiket:** "💧 LIQ" (normal boyut)
- **Kenar:** Sarı noktalı çizgi (dotted)

### Tespit Mantığı:
- **Eşit High'lar:** 3 ardışık bar'da aynı high seviyesi (ATR × 0.1 tolerans)
- **Eşit Low'lar:** 3 ardışık bar'da aynı low seviyesi
- Bu seviyeler **stop-loss avı** (liquidity sweep) için hedef

### Yorumlama:
- Likidite havuzuna **yaklaşma** → Sweep beklenir
- Havuza **dokunup geri dönüş** → Fake breakout, ters yön sinyali
- Havuz **temizlendikten sonra** → Gerçek hareket başlar
- Maksimum 3 havuz gösterilir (en güncel)

**Strateji:**
- LIQ bölgesinin **hemen altında/üstünde** stop koymaktan kaçın
- LIQ sweep sonrası **ters yön giriş** fırsatı
- LIQ + OB + MSB → **Ultra güçlü sinyal**

---

## ⚡ BOS/CHOCH (Break of Structure / Change of Character)

V2.0 güncellemesiyle MSB artık **BOS** ve **CHOCH** olarak ayrıştırıldı.

### BOS (Break of Structure - ⬆ BOS / ⬇ BOS)
- **Anlam:** Mevcut trend yönünde yapı kırılımı (trend devam)
- **Yeşil "⬆ BOS":** Yükseliş trendinde yeni higher high
- **Mor "⬇ BOS":** Düşüş trendinde yeni lower low
- **Strateji:** Trend yönünde pozisyon tut

### CHOCH (Change of Character - ⚡ CHOCH)
- **Renk:** Sarı (color.yellow, 0%)
- **Etiket:** "⚡ CHOCH" (large boyut)
- **Anlam:** **TREND DEĞİŞİMİ** - önceki trendin tersine kırılım!
- **Oluşum:** Düşüş trendindeyken yukarı MSB veya yükseliş trendindeyken aşağı MSB
- **Strateji:** Pozisyon yönünü değiştir, yeni trend başlıyor

### Yorumlama:
- **BOS:** Trendle birlikte devam et
- **CHOCH:** Dikkat! Trend değişiyor, pozisyonu gözden geçir
- CHOCH + RSI Divergence → **Çok güçlü dönüş sinyali**
- CHOCH + OB → Yeni trendin ilk giriş noktası

---

## 🔘 MİTİGATİON BLOCKS (Kırılan Order Block'lar)

Mitigation, fiyatın bir Order Block'a girip çıktığı anı tespit eder. Bu OB artık "kullanılmış" sayılır.

### Görünüm:
- **Renk:** Gri (color.gray, 70%) - eski yeşil/kırmızı yerine
- **Border:** Gri (beyaz değil)
- **Etiket:** Aynı kalır (🟢 OB veya 🔴 OB)

### Tespit Mantığı:
- **Bullish OB Mitigation:** Fiyat OB'nin içine girdi (low ≤ OB top) ve yukarı çıktı (close > OB top)
- **Bearish OB Mitigation:** Fiyat OB'nin içine girdi (high ≥ OB bottom) ve aşağı çıktı (close < OB bottom)

### Yorumlama:
- **Gri OB:** Bu bölge artık güçlü destek/direnç değil
- Fiyat gri OB'ye tekrar dönerse **zayıf reaksiyon** beklenir
- Gri OB'nin **altı/üstü** daha güvenli stop seviyesi
- Aktif (renkli) OB'ler hala güçlü, gri olanlar tükenmiş

**Strateji:**
- Yeni oluşan (renkli) OB'leri tercih et
- Gri OB'lere güvenme, ikincil destek/direnç olarak değerlendir
- Gri OB + Yeni FVG → Yeni fırsat (taze bölge)

---

## 🔥 GÜÇLÜ SİNYAL KOMBİNASYONLARI (GÜNCELLENMİŞ)

### Ultra Güçlü Alım Sinyali:
```
RSI: OS (< 30) + Bullish Divergence (Yeşil)
MACD: Pozitif histogram + Bullish Divergence (Yeşil)
Order Block: 🟢 OB bölgesinde
FVG: Bullish FVG içinde
MSB: ⬆ MSB kırılımı
Fiyat: Yeşil destek bölgesinde + ARS üstünde
Pivot: S1 veya S2 seviyesinde
Breakout: 🚀 BREAKOUT sinyali
```

### Ultra Güçlü Satım Sinyali:
```
RSI: OB (> 70) + Bearish Divergence (Kırmızı)
MACD: Negatif histogram + Bearish Divergence (Kırmızı)
Order Block: 🔴 OB bölgesinde
FVG: Bearish FVG içinde
MSB: ⬇ MSB kırılımı
Fiyat: Kırmızı direnç bölgesinde + ARS altında
Pivot: R1 veya R2 seviyesinde
Breakdown: 💥 BREAKDOWN sinyali
```

### Orta Vade Trend Değişimi:
```
MA'lar: EMA 55-89 golden/death cross
ARS: Yön değişimi
MSB: Market structure break
Order Block: Yeni OB oluşumu
FVG: Ters yönde FVG oluştu
Pivot: P seviyesi kırılımı
S/R Bölgesi: Renk değişimi
```

---

## 📝 AI ANALİZ ŞABLONU (GÜNCELLENMİŞ)

**Analiz yaparken şu formatı kullan:**

1. **Fiyat Konumu:** [Hangi bölgede, hangi seviyelerde]
2. **Gösterge Konsensüsü:**
   - RSI: [Değer] - [OB/OS/MID] - [Divergence durumu]
   - MACD: [Değer] - [Histogram] - [Divergence durumu]
3. **Teknik Seviyeler:**
   - ARS: [Fiyatla ilişki]
   - Pivotlar: [Hangi P/S/R seviyelerinde]
   - Trend Çizgileri: [Destek/Direnç durumu]
   - S/R Bölgeleri: [Renk, konum]
4. **Smart Money Konseptleri:**
   - Order Blocks: [🟢 OB veya 🔴 OB yakınlığı]
   - FVG: [Bullish/Bearish FVG durumu]
   - MSB: [⬆/⬇ MSB kırılımı var mı]
   - Breakout: [🚀 BREAKOUT / 💥 BREAKDOWN]
5. **Hareketli Ortalamalar:** [Varsa - fiyat ilişkisi]
6. **Fibonacci:** [Aktifse - hangi seviyelerde]
7. **Sonuç:** [Net strateji önerisi]

**Gelişmiş Örnek:**
> "SASA 4.35 seviyesinde, 🟢 Bullish Order Block (4.30-4.35) içinde. RSI 28 (OS) + Bullish Divergence, MACD negatif ama histogram yeşile dönüyor. Bullish FVG 4.28-4.32 aralığında destek sağlıyor. ⬆ MSB ile 4.50 seviyesi kırıldı (yeni higher high). Fiyat ARS (4.20) üstünde güçlü. Günlük S1 (4.30) desteği çalıştı. Yeşil trend çizgisi 4.25'te. 🚀 BREAKOUT sinyali aktif - konsolidasyon üstü kırılım. → **ULTRA GÜÇLÜ alım fırsatı: Hedef R1 (4.55) sonra R2 (4.75), Stop: OB altı (4.28), Risk/Reward: 1/3**"
