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
                Log("🔍 Analyzing indicators from screenshot...");

                byte[] imageBytes = System.IO.File.ReadAllBytes(screenshotPath);
                string imageBase64 = Convert.ToBase64String(imageBytes);

                // Vision-based extraction prompt with Smart Money concepts
                string prompt = @"Bu bir TradingView finansal grafik görüntüsüdür. Analizi sistematik yapmanı istiyorum. 
Sırayla şu adımları izle ve değerleri çıkart:

═══════════════════════════════════════════════════════
🔍 ADIM 1: GÖSTERGE TABLOSU TARAMASI (Alt Orta Bölge)
═══════════════════════════════════════════════════════
• TeFo RSI+MACD tablosuna odaklan. 
• RSI satırındaki rakamı ve yanındaki OB/OS durumunu oku.
• MACD satırındaki iki rakamı ve histogram yönünü (Bull/Bear) oku.

═══════════════════════════════════════════════════════
🔍 ADIM 2: FİYAT EKSENİ VE ETİKET TARAMASI (Sağ Kenar)
═══════════════════════════════════════════════════════
• Sağ dikey eksendeki TÜM renkli sayı etiketlerine bak.
• Kırmızı etiketleri (R1-D, R2-D vb.) ve Yeşil etiketleri (S1-D, S2-D vb.) tek tek oku.
• Turuncu/Sarı haftalık (W) etiketleri varsa onları da oku.
• Hiçbir etiketi atlama, her birinin yanındaki sayıyı tam yaz.

═══════════════════════════════════════════════════════
🔍 ADIM 3: GRAFİK ALANI İŞARETÇİLERİ (Orta Bölge)
═══════════════════════════════════════════════════════
• Kutuları ve küçük etiketleri tara:
  - Yeşil 'OB' metni içeren kutular (Bullish Order Block)
  - Kırmızı/Pembe 'OB' metni içeren kutular (Bearish Order Block)
  - 'FVG' yazan boşluk kutuları (Turkuaz veya Kahverengi)
  - '↑ MSB' veya '↓ MSB' yazan yönlü oklar/etiketler.

═══════════════════════════════════════════════════════
🔍 ADIM 4: FİYAT VE TREND GÖZLEMİ
═══════════════════════════════════════════════════════
• Sol/Sağ üstteki güncel fiyatı ('K' değeri) oku.
• Mumların genel eğilimini (Trend) belirle.

═══════════════════════════════════════════════════════
ÇIKTI FORMATI (EKSİKSİZ DOLDUR):
═══════════════════════════════════════════════════════
Güncel Fiyat: [değer]
RSI: [değer], [Durum], [Yön]
MACD: [değer], [Durum], [Yön]
Pivot Daily: R1=[X], R2=[Y], S1=[Z], S2=[W]
Pivot Weekly: R1-W=[X], S1-W=[Y]
Order Block: [Varsa konum ve fiyat]
FVG: [Varsa konum ve fiyat]
MSB: [Varsa yön ve adet]
Trend: [Yükseliş/Düşüş/Yatay]

⚠️ ÖNEMLİ: 
- 'Görünmüyor' demeden önce görüntüyü %200 yakınlaştırmış gibi dikkatli tara.
- Değerleri okurken hata yapma, rakamları net gördüğünden emin ol.
- Eğer bir değer gerçekten yoksa 'Yok' yaz, ama 'Görünebilir' her alanın üzerinden geç.";

                string response = await _gemini.SendMultimodalRequest(prompt, imageBase64);
                
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
                else if (line.Contains("MSB:"))
                {
                    string msbValue = ExtractValue(line, "MSB:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | " + msbValue;
                    else
                        result.SmartMoneySignals = msbValue;
                }
                else if (line.Contains("Breakout:"))
                {
                    string breakoutValue = ExtractValue(line, "Breakout:");
                    if (!string.IsNullOrEmpty(result.SmartMoneySignals) && result.SmartMoneySignals != "None")
                        result.SmartMoneySignals += " | " + breakoutValue;
                    else
                        result.SmartMoneySignals = breakoutValue;
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
