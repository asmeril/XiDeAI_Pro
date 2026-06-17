using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Extracts indicator values from chart screenshots using vision analysis
    /// Feeds Gemini's vision API to identify RSI, MACD, Pivots, Fibonacci, etc.
    /// </summary>
    public class IndicatorExtractor
    {
        private readonly GeminiService _gemini;
        private readonly Action<string>? _logger;

        public class IndicatorAnalysis
        {
            public string RSIValue { get; set; } = "";
            public string RSIStatus { get; set; } = ""; // OB, OS, Normal
            public string MACDStatus { get; set; } = ""; // Histogram color, direction
            public string DivergenceSignal { get; set; } = ""; // Bull, Bear, None
            public string PivotLevels { get; set; } = ""; // P, S1-S3, R1-R3
            public string FibonacciLevels { get; set; } = ""; // %38.2, %50, %61.8
            public string TrendStructure { get; set; } = ""; // HHs/LLs, MA alignment
            public string PatternSignals { get; set; } = ""; // Classical chart patterns
            public string SmartMoneySignals { get; set; } = ""; // Order Block, FVG, MSB
            public string VolumeAnalysis { get; set; } = ""; // Accumulation/Distribution
            public string SummaryContext { get; set; } = ""; // One-line summary for AI
        }

        public IndicatorExtractor(GeminiService gemini, Action<string>? logger = null)
        {
            _gemini = gemini;
            _logger = logger ?? (_ => { });
        }

        private void Log(string msg) => _logger?.Invoke(msg);

        /// <summary>
        /// Analyze screenshot and extract key indicator values
        /// Uses Gemini Vision to read values from the chart
        /// </summary>
        public async Task<IndicatorAnalysis> ExtractIndicatorsFromScreenshot(string screenshotPath)
        {
            var result = new IndicatorAnalysis();

            if (!System.IO.File.Exists(screenshotPath))
            {
                Log("⚠️ Screenshot not found for indicator extraction");
                return result;
            }

            try
            {
                Log("🔍 Grafik görseli vision API'ye gönderiliyor...");

                // Vision-based extraction prompt with Smart Money concepts (Improved v3.1)
                string prompt = @"Bu bir TradingView finansal grafik görüntüsüdür. Görüntü yüksek çözünürlüklüdür.
Analizi en ufak detayları görecek şekilde, sistematik yapmanı istiyorum.

═══════════════════════════════════════════════════════
🔍 ADIM 1: SOL ÜST 'LEGEND' (KÜNYE) BÖLGESİ
═══════════════════════════════════════════════════════
• Sol üst köşedeki sembol isminin hemen altındaki metin grubuna bak.
• OHLC değerlerini (A, Y, D, K) ve gösterge isimlerinin yanındaki değerleri buradan oku.
• Buradaki rakamlar grafikteki mumlardan çok daha nettir, ÖNCELİĞİ buraya ver.

═══════════════════════════════════════════════════════
🔍 ADIM 2: 'TeFo RSI+MACD' TABLOSU (Genellikle Sağ Alt veya Orta Alt)
═══════════════════════════════════════════════════════
• Grafik üzerine bindirilmiş (overlay) tabloyu bul.
• RSI: Satırındaki sayısal değeri ve 'OB/OS/Normal' durumunu oku.
• MACD: Histogramın rengine (Yeşil/Kırmızı) ve sayısal değerlerine bak.
• DIVERGENCE: Eğer tabloda 'Bullish' veya 'Bearish' Divergence uyarısı varsa MUTLAKA oku.

═══════════════════════════════════════════════════════
🔍 ADIM 3: SAĞ DİKEY EKSEN VE FİYAT ETİKETLERİ
═══════════════════════════════════════════════════════
• Sağdaki fiyat ölçeği üzerindeki TÜM renkli sayı kutucuklarını tek tek oku.
• Kırmızı (R1, R2, R3 - Direnç), Yeşil (S1, S2, S3 - Destek) ve Turuncu etiketleri kaçırma.
• Fibonacci seviyelerini (%38.2, %50, %61.8 vb.) hem grafik üzerindeki çizgilerden hem de sağ eksendeki etiketlerden tespit et.
• En sağdaki mevcut fiyat etiketini oku.

═══════════════════════════════════════════════════════
🔍 ADIM 4: SMART MONEY & FORMASYONLAR (V2.0 Güncel)
═══════════════════════════════════════════════════════
• Grafik alanındaki kutu ve yazıları tara:
  - '🟢 OB' veya '🔴 OB' (Order Block) - Teal/Turuncu kutular
  - 'FVG↑' veya 'FVG↓' (Fair Value Gap) - Lime/Fuşya kutular
  - '⬆ BOS' veya '⬇ BOS' (Break of Structure) - Yeşil/Mor çizgiler
  - '⚡ CHOCH' (Change of Character) - Sarı etiket (TREND DEĞİŞİMİ!)
  - '💧 LIQ' (Liquidity Pool) - Sarı noktalı kutular
  - Gri renkte görünen OB'ler 'MITIGATED' (kullanılmış) demektir
  - Fibonacci seviyelerinin grafik üzerindeki konumlarını ve fiyatlarını doğrula.
• Klasik formasyonları ayrıca kontrol et:
  - Üçgen, flama/bayrak, kanal, takoz, ikili dip/tepe, OBO/TOBO, fincan-kulp
  - Sadece mum yapısı ve trend çizgileri net görünüyorsa formasyon adı ver.
  - Net değilse 'Belirgin formasyon yok' yaz; tahmin yürütme.

═══════════════════════════════════════════════════════
ÇIKTI FORMATI (EKSİKSİZ DOLDUR):
═══════════════════════════════════════════════════════
Güncel Fiyat: [değer]
RSI: [değer], [Durum], [Varsa Divergence]
MACD: [değer], [Durum], [Varsa Divergence]
Pivot Levels: R1=[X], R2=[Y], S1=[Z], S2=[W]
Smart Money: [OB/FVG/BOS/CHOCH/LIQ/Mitigation ve FIB seviyeleri]
Formasyon: [Net formasyon adı + kırılım/iptal seviyesi veya Belirgin formasyon yok]
Trend: [Yükseliş/Düşüş/Yatay]

⚠️ ÖNEMLİ TALİMATLAR:
- Grafik otomatik ölçeklendirilmiştir, tüm önemli seviyeler (özellikle sağ eksen) görünür durumdadır.
- Görüntüdeki gri/siyah filigran (watermark) yazılarını görmezden gel, onlar analizin parçası değil.
- 'Görünmuyor' demeden önce görüntüyü zihninde büyüterek tekrar bak. 
- Legend (sol üst) kısmındaki rakamlar her zaman en doğru kaynaktır.
- Rakamları okurken ondalık basamaklara dikkat et.";

                // v4.10.5 FIX: Directly pass screenshotPath (file path) to SendMultimodalRequest.
                // Previously, imageBase64 string was passed — but SendMultimodalRequest expects a
                // file path, not a base64 string. File.Exists(base64) → false → fell back to
                // text-only SendRequest → model never saw the image → 2 wasted minutes.
                string? response = await _gemini.SendMultimodalRequest(prompt, screenshotPath);
                
                if (string.IsNullOrEmpty(response))
                {
                    Log("⚠️ Vision API returned empty response");
                    return result;
                }

                Log($"📊 Vision API Response: {response.Substring(0, Math.Min(200, response.Length))}...");

                // Parse response
                result = ParseIndicatorResponse(response);
                
                // Build summary context for AI
                result.SummaryContext = BuildSummaryContext(result);

                Log("✅ Indicator extraction completed");
                return result;
            }
            catch (Exception ex)
            {
                Log($"❌ Indicator extraction error: {ex.Message}");
                return result;
            }
        }

        private IndicatorAnalysis ParseIndicatorResponse(string response)
        {
            var result = new IndicatorAnalysis();

            // Parse each line - now with Smart Money concepts
            var lines = response.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("RSI:"))
                {
                    result.RSIValue = ExtractValue(line, "RSI:");
                    // Also extract divergence from same line if present
                    if (line.Contains("Divergence:"))
                        result.DivergenceSignal = ExtractValue(line, "Divergence:");
                }
                else if (line.Contains("MACD:"))
                {
                    result.MACDStatus = ExtractValue(line, "MACD:");
                    // Also extract MACD divergence from same line
                    if (line.Contains("Divergence:"))
                    {
                        string diverFromLine = ExtractValue(line, "Divergence:");
                        if (string.IsNullOrEmpty(result.DivergenceSignal))
                            result.DivergenceSignal = diverFromLine;
                    }
                }
                else if (line.Contains("Divergence:") && string.IsNullOrEmpty(result.DivergenceSignal))
                {
                    result.DivergenceSignal = ExtractValue(line, "Divergence:");
                }
                else if (line.Contains("Pivot:"))
                {
                    result.PivotLevels = ExtractValue(line, "Pivot:");
                }
                else if (line.Contains("Fibonacci:"))
                {
                    result.FibonacciLevels = ExtractValue(line, "Fibonacci:");
                }
                else if (line.Contains("Order Block:"))
                {
                    result.SmartMoneySignals = ExtractValue(line, "Order Block:");
                }
                else if (line.Contains("FVG:"))
                {
                    string fvgValue = ExtractValue(line, "FVG:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | " + fvgValue;
                    else
                        result.SmartMoneySignals = fvgValue;
                }
                else if (line.Contains("MSB:") || line.Contains("BOS:"))
                {
                    string msbValue = line.Contains("BOS:") ? ExtractValue(line, "BOS:") : ExtractValue(line, "MSB:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | BOS: " + msbValue;
                    else
                        result.SmartMoneySignals = "BOS: " + msbValue;
                }
                else if (line.Contains("CHOCH:"))
                {
                    string chochValue = ExtractValue(line, "CHOCH:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | ⚡CHOCH: " + chochValue;
                    else
                        result.SmartMoneySignals = "⚡CHOCH (Trend Değişimi): " + chochValue;
                }
                else if (line.Contains("Liquidity:") || line.Contains("LIQ:"))
                {
                    string liqValue = line.Contains("LIQ:") ? ExtractValue(line, "LIQ:") : ExtractValue(line, "Liquidity:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | 💧LIQ: " + liqValue;
                    else
                        result.SmartMoneySignals = "💧Liquidity Pool: " + liqValue;
                }
                else if (line.Contains("Mitigation:") || line.Contains("Mitigated:"))
                {
                    string mitValue = line.Contains("Mitigated:") ? ExtractValue(line, "Mitigated:") : ExtractValue(line, "Mitigation:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | Mitigated OB: " + mitValue;
                    else
                        result.SmartMoneySignals = "Mitigated OB: " + mitValue;
                }
                else if (line.Contains("Breakout:"))
                {
                    string breakoutValue = ExtractValue(line, "Breakout:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | " + breakoutValue;
                    else
                        result.SmartMoneySignals = breakoutValue;
                }
                else if (line.Contains("Formasyon:") || line.Contains("Pattern:"))
                {
                    result.PatternSignals = line.Contains("Formasyon:") ? ExtractValue(line, "Formasyon:") : ExtractValue(line, "Pattern:");
                }
                else if (line.Contains("Trend:"))
                {
                    result.TrendStructure = ExtractValue(line, "Trend:");
                }
                else if (line.Contains("Volume:"))
                {
                    result.VolumeAnalysis = ExtractValue(line, "Volume:");
                }
            }

            return result;
        }

        private string ExtractValue(string line, string prefix)
        {
            int idx = line.IndexOf(prefix);
            if (idx < 0) return "";
            return line.Substring(idx + prefix.Length).Trim();
        }

        private string BuildSummaryContext(IndicatorAnalysis ind)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 TECHNICAL ANALYSIS SUMMARY (From IndicatorGuide):");
            sb.AppendLine();
            
            // Momentum Section
            if (!string.IsNullOrEmpty(ind.RSIValue) || !string.IsNullOrEmpty(ind.MACDStatus))
            {
                sb.AppendLine("⚡ MOMENTUM INDICATORS:");
                if (!string.IsNullOrEmpty(ind.RSIValue))
                    sb.AppendLine($"  • RSI: {ind.RSIValue}");
                if (!string.IsNullOrEmpty(ind.MACDStatus))
                    sb.AppendLine($"  • MACD: {ind.MACDStatus}");
                if (!string.IsNullOrEmpty(ind.DivergenceSignal) && ind.DivergenceSignal.ToLower() != "none")
                    sb.AppendLine($"  • ⚠️ DIVERGENCE: {ind.DivergenceSignal} (Strong Reversal Signal!)");
                sb.AppendLine();
            }
            
            // Levels Section
            if (!string.IsNullOrEmpty(ind.PivotLevels) || !string.IsNullOrEmpty(ind.FibonacciLevels))
            {
                sb.AppendLine("📍 SUPPORT / RESISTANCE LEVELS:");
                if (!string.IsNullOrEmpty(ind.PivotLevels))
                    sb.AppendLine($"  • Pivot Points: {ind.PivotLevels}");
                if (!string.IsNullOrEmpty(ind.FibonacciLevels))
                    sb.AppendLine($"  • Fibonacci Retracement: {ind.FibonacciLevels}");
                sb.AppendLine();
            }
            
            // Smart Money Section
            if (!string.IsNullOrEmpty(ind.SmartMoneySignals) && ind.SmartMoneySignals.ToLower() != "none")
            {
                sb.AppendLine("💰 SMART MONEY STRUCTURES:");
                sb.AppendLine($"  {ind.SmartMoneySignals}");
                sb.AppendLine();
            }

            // Pattern Section
            if (!string.IsNullOrEmpty(ind.PatternSignals) &&
                !ind.PatternSignals.Contains("yok", StringComparison.OrdinalIgnoreCase) &&
                !ind.PatternSignals.Contains("none", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("📐 CHART PATTERN:");
                sb.AppendLine($"  • {ind.PatternSignals}");
                sb.AppendLine();
            }
            
            // Trend Section
            if (!string.IsNullOrEmpty(ind.TrendStructure))
            {
                sb.AppendLine("📈 TREND STRUCTURE:");
                sb.AppendLine($"  • {ind.TrendStructure}");
                sb.AppendLine();
            }
            
            // Volume Section
            if (!string.IsNullOrEmpty(ind.VolumeAnalysis))
            {
                sb.AppendLine("📊 VOLUME ANALYSIS:");
                sb.AppendLine($"  • {ind.VolumeAnalysis}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
