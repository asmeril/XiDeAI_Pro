# XiDeAI Pro – Geliştirme Günlüğü (04–06 Ocak 2026)

> Bu dosya, 04/01/2026–06/01/2026 tarihleri arasında yapılan geliştirmelerin ayrıntılı kaydıdır. Tüm değişiklikler Windows (.NET 8 WinForms) XiDeAI Pro uygulamasına yöneliktir.

---

## 04 Ocak 2026 – Vision Prompt Revizyonu

### IndicatorExtractor.cs
- Vision API prompt’u **tamamen Türkçe** ve **gösterge bazlı** yeniden yazıldı.
- Talimat seti genişletildi:
  - **TeFo RSI+MACD tablosu:** Sol alt köşeden RSI, MACD, histogram ve diverjans tespiti.
  - **Güncel fiyat:** SAT/AL butonlarından veya K değeri üzerinden okuma.
  - **Pivot seviyeleri:** R1-D, R2-D, R3-D, S1-W, S2-W, S3-W vb. label’ları özellikle ara.
  - **Order Block:** Yeşil (Bullish) ve Pembe (Bearish) OB etiketleri.
  - **FVG:** Cyan (Bullish) ve Kahverengi (Bearish) kutular.
  - **MSB:** Yeşil ↑MSB ve Mor ↓MSB etiketleri.
- Kritik kural: “Her köşeyi kontrol etmeden ‘Not visible’ deme”.

---

## 05 Ocak 2026 – Screenshot ve Sembol Formatı Düzeltmeleri

### screenshot.py – Mouse/End Tuşu & OHLC
- TradingView, mouse imlecinin olduğu barın OHLC’sini gösterdiği için yanlış kapanış okunuyordu.
- Çözüm: `Keys.END` ile son bara gitme ve mouse’u chart’ın sağ tarafına taşıma eklendi.
  ```python
  actions.move_to_element(chart_area)
  actions.click()
  actions.send_keys(Keys.END)
  actions.perform()
  actions.move_to_element_with_offset(chart_area, int(chart_width * 0.45), int(chart_height * 0.3))
  ```

### screenshot.py – yfinance Sembol Dönüşümü
- `BIST:XU100` → `XU100.IS` dönüşümü yapılmadığı için 404 alınıyordu.
- Çözüm: BIST prefix’i temizlendi, BIST hisse/endekslerine `.IS` eklendi (USDT/FX vb. hariç tutuldu).
  ```python
  if ':' in ticker:
      ticker = ticker.split(':')[1]  # BIST:THYAO -> THYAO
  if not any(x in ticker for x in ['.', '^', '=', 'USDT']):
      ticker = f"{ticker}.IS"
  ```

---

## 06 Ocak 2026 – Pivot JSON Kaydı, Prompt Enjeksiyonu ve Influencer Talimatı

### screenshot.py – Pivot JSON Kaydetme (Yeni)
- Önceden pivot hesaplanıyor ancak JSON’a **kaydedilmiyordu**; AI prompt’u pivot değerlerini alamıyordu.
- Screenshot başarıyla alındıktan sonra yfinance OHLC + pivotlar JSON’a yazılıyor:
  - Dosya adı: `SYMBOL_pivots_YYYYMMDD.json`
  - İçerik: `symbol`, `timestamp`, `ohlc {date, open, high, low, close, source}`, `pivots {pivot, r1-3, s1-3, calculated_from_date, valid_for_date}`
- Klasör yoksa oluşturuluyor; `ensure_ascii=False` ile yazılıyor.

### ManualAnalysisService.cs – Pivot Değerlerini Prompt’a Enjeksiyon
- Pivot JSON yüklendikten sonra **gerçek seviyeler** prompt’a eklenmeye başladı:
  - R3, R2, R1, Pivot, S1, S2, S3 tümü priceContext’e ekleniyor.
  - Not: “Grafikte görünen pivotlar önceki güne ait olabilir; yfinance verisi günceldir” uyarısı eklendi.

### PromptManager.cs – Influencer Bölümü Güçlendirme
- Önceki prompt, bir tek influencer’a indirgenebiliyordu (ör. Perihan Tantuğ).
- Yeni talimatlar:
  - “Sadece 1 kişiyi seçme; en az 2-3 görüşü teknik tutarlılıkla ekle.”
  - “Hype arama; teknik sinyallerle uyumlu olanları 1-2 cümleyle özetle.”
  - Format: `@Handle: [kısa teknik özet]`

### Genel Build/Dağıtım
- `dotnet publish -c Release -r win-x64 --self-contained -o publish`
- Çıktılar `Dist/publish/` altına kopyalandı (XiDeAI_Pro.exe tek dosya; PublishSingleFile).
- Python `screenshot.py` ve `Config` içeriği publish’e kopyalandı (post-publish script).

---

## Etkilenen Dosyalar
- `Services/IndicatorExtractor.cs`
- `Services/ManualAnalysisService.cs`
- `Services/PromptManager.cs`
- `Scripts/screenshot.py`
- `Dist/publish/XiDeAI_Pro.exe` (self-contained, tüm C# değişiklikleri içerir)

---

## Test Beklentisi (XU100 Örneği)
1. `screenshot.py` çalışınca `XU100_pivots_YYYYMMDD.json` oluşmalı (yfinance OHLC’den).
2. Vision API: RSI, MACD, Pivot label’ları, OB/FVG/MSB etiketlerini okumalı.
3. ManualAnalysisService: Pivot JSON’u yükleyip R1/S1/Pivot değerlerini prompt’a eklemeli.
4. Gemini analizinde 2-3 influencer yorumu teknik filtreyle özetlenmeli.

---

## Notlar
- Semboller: BIST hisseleri/endeksleri `.IS` ile yfinance’e sorgulanıyor (USDT/FX hariç).
- Mouse konumu: End tuşu + sağ tarafta imleç, TradingView OHLC’nin son bar’ı göstermesini sağlıyor.
- Prompt dil ve talimatlar tamamen Türkçe; Smart Money (OB/FVG/MSB) vurgusu korunuyor.
