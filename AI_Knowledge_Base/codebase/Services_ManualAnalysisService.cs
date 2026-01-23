using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Pivot JSON'dan okunan veri
    /// </summary>
    public class PivotData
    {
        public decimal Pivot { get; set; }
        public decimal R1 { get; set; }
        public decimal R2 { get; set; }
        public decimal R3 { get; set; }
        public decimal S1 { get; set; }
        public decimal S2 { get; set; }
        public decimal S3 { get; set; }
        public string IntervalLabel { get; set; } = "GÜNLÜK";
        public string CalculatedFromDate { get; set; } = "";
        public string ValidForDate { get; set; } = "";
        
        public override string ToString()
        {
            return $"P:{Pivot:F2} | R1:{R1:F2} R2:{R2:F2} | S1:{S1:F2} S2:{S2:F2} | ({CalculatedFromDate} → {ValidForDate})";
        }
    }

    public class ManualAnalysisResult
    {
        public bool Success { get; set; }
        public string ReportText { get; set; } = "";
        public string ScreenshotPath { get; set; } = "";
        public string TvLink { get; set; } = "";
        public PriceInfo? PriceInfo { get; set; }
        public List<InfluencerPost> InfluencerPosts { get; set; } = new List<InfluencerPost>();
        public PivotData? PivotData { get; set; }
        /// <summary>
        /// Short thread format (4 tweets) for X posting
        /// </summary>
        public string ShortThread { get; set; } = "";
    }

    public class ManualAnalysisService
    {
        private readonly GeminiService _geminiService;
        private readonly PriceFetchService _priceService;
        private readonly ScreenshotService _screenshotService;
        private readonly SocialIntelService _socialIntel;
        private readonly InfluencerControlService _influencerControl;
        private readonly MemoryEngine _memory;
        private readonly Action<string>? _logger;
        
        public ManualAnalysisService(GeminiService geminiService, ScreenshotService screenshotService, SocialIntelService socialIntel, InfluencerControlService influencerControl, MemoryEngine memory, Action<string>? logger = null)
        {
            _geminiService = geminiService;
            _priceService = new PriceFetchService();
            _screenshotService = screenshotService;
            _socialIntel = socialIntel;
            _influencerControl = influencerControl;
            _memory = memory;
            _logger = logger ?? (_ => { }); // Default to no-op if null
        }

        private void Log(string message)
        {
             _logger?.Invoke(message);
        }

        public async Task<ManualAnalysisResult> PerformManualAnalysis(string symbol, string marketType, string timeFrame, string chartId = "", string basis = "TL")
        {
            var result = new ManualAnalysisResult();
            try
            {
                Log($"🚀 [ANALİZ BAŞLADI] Sembol: {symbol}, Periyot: {timeFrame}, Baz: {basis}");

                // 0. Get Historical Context from Shared Memory
                string historicalContext = _memory.GetSymbolContext(symbol);
                if (!string.IsNullOrEmpty(historicalContext))
                {
                    Log("📜 Geçmiş analiz hafızası yüklendi (30 gün).");
                }

                // 1. Get Price Info
                Log("💰 Fiyat bilgisi çekiliyor...");
                var priceInfo = await _priceService.GetPriceAsync(symbol, marketType);
                if (priceInfo != null)
                {
                    result.PriceInfo = priceInfo;
                    Log($"✅ Fiyat alındı: {priceInfo.Price}");
                }
                else 
                {
                    Log("⚠️ Fiyat bilgisi alınamadı, devam ediliyor...");
                }

                // 2. Pre-Check: Validate TradingView Symbol Existence
                string tvSymbol = ConvertToTradingViewSymbol(symbol, marketType, basis);
                Log($"🔍 TV Sembol Dönüşümü: {tvSymbol}");
                if (!await IsTradingViewSymbolValid(tvSymbol)) 
                {
                     Log($"⚠️ UYARI: TradingView üzerinde '{tvSymbol}' bulunamadı! Grafik hatalı olabilir.");
                }

                // 3. Generate Screenshot & Link
                string linkInterval = (timeFrame == "G" || timeFrame == "Daily") ? "D" : timeFrame;
                
                string tvLink = string.IsNullOrEmpty(chartId)
                    ? $"https://tr.tradingview.com/chart/?symbol={tvSymbol}&interval={linkInterval}&theme=dark"
                    : $"https://tr.tradingview.com/chart/{chartId}/?symbol={tvSymbol}&interval={linkInterval}&theme=dark";
                
                result.TvLink = tvLink;
                
                string? screenshotPath = null;
                try
                {
                    if (_screenshotService != null)
                    {
                        Log("📸 Ekran görüntüsü süreci başlatıldı...");
                        string period = timeFrame;
                        if(timeFrame == "G" || timeFrame == "Daily") period = "D"; 
                        
                        screenshotPath = await _screenshotService.CaptureChart(tvSymbol, period, chartId);
                        if (File.Exists(screenshotPath))
                        {
                            result.ScreenshotPath = screenshotPath;
                            Log($"✅ Ekran görüntüsü başarılı: {Path.GetFileName(screenshotPath)}");
                            
                            // Try to read pivot data from JSON file
                            var pivotData = LoadPivotDataFromJson(symbol, screenshotPath);
                            if (pivotData != null)
                            {
                                result.PivotData = pivotData;
                                Log($"📈 Pivot verileri yüklendi: P={pivotData.Pivot}");
                            }
                        }
                        else
                        {
                            Log("❌ Ekran görüntüsü dosyası oluşturulamadı.");
                        }
                    }
                }
                catch (Exception ex) { Log($"❌ Screenshot hatası: {ex.Message}"); }
                
                string priceContext = priceInfo != null 
                    ? $"Fiyat: {priceInfo.Price}, Değişim: %{priceInfo.Change24h}" 
                    : "Fiyat verisi alınamadı.";

                // PIVOT TARİH KONTEKSTI
                DateTime today = DateTime.Now;
                DateTime pivotDate = GetPreviousTradingDay(today);
                string pivotDateStr = pivotDate.ToString("dd.MM.yyyy");
                string todayStr = today.ToString("dd.MM.yyyy");
                
                string dateContext = $"\n📅 TARAMA TARİHİ: {todayStr}\n" +
                    $"📊 PIVOT VERİSİ TARİHİ: {pivotDateStr}\n";
                
                priceContext += dateContext;
                
                if (result.PivotData != null)
                {
                    var pd = result.PivotData;
                    string pivotValues = $"\n📍 {pd.IntervalLabel} TEKNİK SEVİYE ANALİZİ ({pd.CalculatedFromDate}):\n" +
                        $"  • P (Pivot): {pd.Pivot:N2} | R1: {pd.R1:N2} | S1: {pd.S1:N2}\n";
                    priceContext += pivotValues;
                }

                // 3. Search for influencer posts
                var influencerPosts = new List<InfluencerPost>();
                try
                {
                    Log("🐦 Sosyal medya (X) fenomen yorumları taranıyor...");
                    if (_socialIntel != null)
                    {
                        var vipHandles = _influencerControl?.GetTopInfluencers(symbol, 20);
                        influencerPosts = await _socialIntel.FindInfluencerAnalyses(symbol, marketType, vipHandles);
                        
                        string currentUser = ConfigManager.Current.XLoginUser?.Replace("@", "").Trim() ?? "";
                        string symUpper = symbol.ToUpperInvariant();

                        var cleanedPosts = influencerPosts
                            .Where(p => p.Handle.Equals("EFELERiiNEFESi3", StringComparison.OrdinalIgnoreCase) || 
                                       !ContentQualityGuard.ContainsPrivateLinks(p.Content))
                            .Where(p => {
                                string h = p.Handle?.Replace("@", "").Trim() ?? "";
                                if (!string.IsNullOrEmpty(currentUser) && h.Equals(currentUser, StringComparison.OrdinalIgnoreCase)) return false;

                                string content = p.Content?.ToUpperInvariant() ?? "";
                                
                                // Anti-Noise: SMART vs Smart Money
                                if (symUpper == "SMART")
                                {
                                    if (content.Contains("SMART MONEY") && !content.Contains("#SMART") && !content.Contains("$SMART"))
                                        return false;
                                }

                                // Word boundary match to avoid partial matches
                                return content.Contains($"#{symUpper}") || 
                                       content.Contains($"${symUpper}") || 
                                       System.Text.RegularExpressions.Regex.IsMatch(content, $@"\b{symUpper}\b");
                            })
                            .ToList();
                        
                        Log($"🔎 {cleanedPosts.Count} geçerli influencer yorumu bulundu.");
                        result.InfluencerPosts = cleanedPosts;
                        influencerPosts = cleanedPosts;
                    }
                }
                catch (Exception ex) { Log($"⚠️ Sosyal medya tarama hatası: {ex.Message}"); }

                // 4. EXTRACT INDICATORS FROM SCREENSHOT
                string indicatorContext = "";
                if (screenshotPath != null && File.Exists(screenshotPath))
                {
                    try
                    {
                        Log("👁️ Vision API: Görsel gösterge çıkarımı başlatıldı...");
                        var extractor = new IndicatorExtractor(_geminiService, Log);
                        var indicatorAnalysis = await extractor.ExtractIndicatorsFromScreenshot(screenshotPath);
                        
                        if (!string.IsNullOrEmpty(indicatorAnalysis.SummaryContext))
                        {
                            indicatorContext = indicatorAnalysis.SummaryContext;
                            Log("✅ Görsel göstergeler başarıyla analiz edildi.");
                            priceContext += "\n\n" + indicatorContext;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ Vision API hatası: {ex.Message}");
                    }
                }

                // 5. Generate AI Analysis
                string? analysis = null;
                
                Log("🧠 [CORTEX NIRVANA] Viral zeka ve stratejik sentez devreye alınıyor...");
                Log("🤖 Gemini AI: Nihai pazar analizi oluşturuluyor...");
                if (_geminiService == null) throw new Exception("Gemini servisi başlatılamadı.");

                string influencerContext = "";
                if (influencerPosts != null && influencerPosts.Count > 0)
                {
                    var citationList = new List<string>();
                    foreach (var post in influencerPosts)
                    {
                        citationList.Add($"• @{post.Handle}: {post.Content}");
                    }
                    influencerContext = string.Join("\n", citationList);
                }

                if (screenshotPath != null && File.Exists(screenshotPath))
                {
                    analysis = await _geminiService.GenerateMarketAnalysisWithChart(symbol, marketType, priceContext, screenshotPath, influencerContext);
                }
                else
                {
                    analysis = await _geminiService.GenerateMarketAnalysisWithPrice(symbol, marketType, priceContext, influencerContext);
                }
                
                if (string.IsNullOrEmpty(analysis))
                {
                    Log("❌ Gemini AI yanıt üretemedi.");
                    result.ReportText = $"AI analizi oluşturulamadı. (Hata: {_geminiService.LastError})";
                    return result;
                }

                Log("✅ AI analizi oluşturuldu.");
                result.ReportText = analysis;

                // === YENI: SHORT THREAD FORMAT (4 Tweet) ===
                try
                {
                    Log("🐦 [SHORT THREAD] 4 tweet'lik kısa thread oluşturuluyor...");
                    
                    // Get last week's successful analysis for history callback
                    string lastWeekAnalysis = _geminiService.GetLastWeekSuccessfulAnalysis(symbol);
                    if (!string.IsNullOrEmpty(lastWeekAnalysis))
                    {
                        Log($"📜 Geçmiş başarılı analiz bulundu, thread'e entegre edilecek.");
                    }

                    string? shortThread = await _geminiService.GenerateShortThreadWithHistory(
                        symbol,
                        marketType,
                        priceContext,
                        indicatorContext,
                        influencerContext,
                        timeFrame,
                        screenshotPath,
                        lastWeekAnalysis
                    );

                    if (!string.IsNullOrEmpty(shortThread))
                    {
                        result.ShortThread = shortThread;
                        Log("✅ Short thread başarıyla oluşturuldu (4 tweet).");
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Short thread oluşturma hatası: {ex.Message}");
                }

                // Save to Shared Memory
                string sentimentTag = "";
                if (analysis.Contains("Açığa Satış") || analysis.Contains("Düşüş Yönlü")) sentimentTag = "\n[SENTİMENT: SHORT]";
                else if (analysis.Contains("Uzun Pozisyon") || analysis.Contains("Yükseliş Yönlü")) sentimentTag = "\n[SENTİMENT: LONG]";

                _memory.StoreAnalysis(symbol, "MANUAL", analysis + sentimentTag);
                
                // result.ReportText += $"\n\n📊 Grafik: {tvLink}"; // REMOVED: Duplicates with UI/Telegram footer
                result.Success = true;
                Log("💾 [ANALİZ TAMAMLANDI] Hafızaya kaydedildi.");

                return result;
            }
            catch (Exception ex)
            {
                Log($"❌ PerformManualAnalysis Kritik Hata: {ex.Message}");
                result.ReportText = $"Analiz sırasında kritik hata oluştu: {ex.Message}";
                return result;
            }
        }

        public void EnrichSignalWithResult(SignalData signal, ManualAnalysisResult result, string basis)
        {
            if (signal == null || result == null) return;

            // Use the actual AI text (before adding grafik link if possible, or just parse)
            string text = result.ReportText;

            // Dynamic Strategy Naming
            if (basis == "XU100")
            {
                signal.Strategy = "Kompozit Analiz";
            }
            else if (text.Contains("Açığa Satış") || text.Contains("Düşüş Yönlü"))
            {
                signal.Strategy = "Teknik Analiz (Short)";
            }
            else if (text.Contains("Uzun Pozisyon") || text.Contains("Yükseliş Yönlü"))
            {
                signal.Strategy = "Teknik Analiz (Long)";
            }
            else
            {
                signal.Strategy = "Teknik Analiz"; // Default fallback
            }
        }
        
        /// <summary>
        /// Convert Turkish symbol names to English if needed, then to TradingView format
        /// </summary>
        private string TurkishToEnglishSymbol(string symbol)
        {
            // Avoid duplicate-key crashes by building map with TryAdd semantics
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Add(string key, string value)
            {
                if (!map.ContainsKey(key)) map[key] = value;
            }

            // Emtia - Turkish to English
            Add("PAMUK", "COTTON");
            Add("BUĞDAY", "WHEAT"); Add("BUGDAY", "WHEAT");
            Add("MİSIR", "CORN"); Add("MISIR", "CORN");
            Add("KAHVE", "COFFEE");
            Add("ŞEKER", "SUGAR"); Add("SEKER", "SUGAR");
            Add("KAKAO", "COCOA");
            Add("ALTIN", "XAUUSD");
            Add("GÜMÜŞ", "XAGUSD"); Add("GUMUS", "XAGUSD");
            Add("PLATINUM", "PLATINUM"); Add("PLATIN", "PLATINUM");
            Add("PALADYUM", "XPDUSD");
            Add("BAKIR", "COPPER");
            Add("ALÜMINYUM", "ALUMINUM"); Add("ALUMINYUM", "ALUMINUM");
            Add("ÇINKO", "ZINC"); Add("CINKO", "ZINC");
            Add("NIKEL", "NICKEL");
            Add("KURŞUN", "LEAD"); Add("KURSUN", "LEAD");
            Add("KALAY", "TIN");
            Add("KARBON", "CARBON");
            Add("URANYUM", "URANIUM");
            Add("LİTYUM", "LITHIUM"); Add("LITYUM", "LITHIUM");
            Add("PETROL", "USOIL");
            Add("BRENT", "UKOIL");
            Add("DOĞALGAZ", "NATGAS"); Add("DOGALGAZ", "NATGAS");
            Add("KERESTE", "LUMBER");

            // Forex - Turkish to English
            Add("EURODOLAR", "EURUSD"); Add("EURO/DOLAR", "EURUSD");
            Add("STERLIN/DOLAR", "GBPUSD");
            Add("DOLAR/YEN", "USDJPY");
            Add("DOLAR/FRANK", "USDCHF");
            Add("ALTIN/DOLAR", "XAUUSD");

            // Endeksler - Turkish to English
            Add("S&P500", "SPX"); Add("SP500", "SPX");
            Add("DOW JONES", "DJI"); Add("DOWJONES", "DJI");
            Add("NIKKEI 225", "NIKKEI225"); Add("NIKKEI225", "NIKKEI225");
            Add("HONG KONG 50", "HANGSENG"); Add("HONGKONG50", "HANGSENG");
            Add("BREZILYA", "BOVESPA");
            Add("ARJANTIN", "MERVAL");
            Add("VIX", "VIX"); Add("KORKU", "VIX");
            Add("DXY", "DXY"); Add("DOLAR ENDEKSI", "DXY");

            string upperSym = symbol.ToUpperInvariant().Trim();
            if (map.TryGetValue(upperSym, out var mapped)) return mapped;
            return upperSym;
        }

        /// <summary>
        /// Convert our market symbol to TradingView symbol format
        /// </summary>
        private string ConvertToTradingViewSymbol(string symbol, string marketType, string basis = "TL")
        {
            // 0. Power User Mode: If symbol contains ':', assume valid TV format (e.g. OANDA:XAUUSD)
            if (symbol.Contains(":")) return CheckBasis(symbol, basis);

            // 1. Try to get from SymbolData first
            string tvSymbol = SymbolData.GetTradingViewSymbol(marketType, symbol);
            
            // 2. If SymbolData returned the original symbol (not found), use fallback logic
            if (tvSymbol == symbol)
            {
                // Fallback: Manual conversion for edge cases
                string upperSym = symbol.ToUpperInvariant().Trim();
                
                // VIP/Futures Special Handling
                if (upperSym.StartsWith("VIP-"))
                {
                    string core = upperSym.Replace("VIP-", "");
                    if (core == "X030T") tvSymbol = "BIST:XU030D1"; 
                    else tvSymbol = $"BIST:{core}1!";
                }
                else
                {
                    // Default fallback based on market type
                    tvSymbol = marketType switch
                    {
                        "Kripto" => $"BINANCE:{(upperSym.EndsWith("USDT") ? upperSym : upperSym + "USDT")}",
                        "BIST" => $"BIST:{upperSym}",
                        "Forex" => $"FX_IDC:{upperSym}",
                        "ABD" => $"NASDAQ:{upperSym}",
                        "Emtia" => $"TVC:{upperSym}",
                        "Endeks" => $"TVC:{upperSym}",
                        _ => upperSym
                    };
                }
            }

            return CheckBasis(tvSymbol, basis);
        }

        private string CheckBasis(string baseSym, string basis)
        {
            if (basis == "USD") return $"{baseSym}/FX_IDC:USDTRY";
            if (basis == "EUR") return $"{baseSym}/FX_IDC:EURTRY";
            if (basis == "XU100") return $"{baseSym}/BIST:XU100";
            return baseSym;
        }

        private string GetCommodityTicker(string sym)
        {
            // Normalize Turkish to English first
            string normalized = sym;
            if (sym.Contains("PAMUK") || sym.Contains("Pamuk")) normalized = "COTTON";
            else if (sym.Contains("BUĞDAY") || sym.Contains("WHEAT") || sym.Contains("Buğday")) normalized = "WHEAT";
            else if (sym.Contains("MÍSIR") || sym.Contains("CORN") || sym.Contains("Mısır")) normalized = "CORN";
            else if (sym.Contains("ALTIN") || sym.Contains("Altın")) normalized = "XAUUSD";
            else if (sym.Contains("GÜMÜŞ") || sym.Contains("GUMUS") || sym.Contains("Gümüş")) normalized = "XAGUSD";
            else if (sym.Contains("DOĞALGAZ") || sym.Contains("DOGALGAZ")) normalized = "NATGAS";
            else if (sym.Contains("SIGIR") || sym.Contains("CATTLE")) normalized = "CATTLE";
            
            // Common Turkish and English aliases (Priority: SPOT/CFD)
            if (normalized == "ALTIN" || normalized == "GOLD" || normalized == "XAUUSD" || normalized == "ONS") return "OANDA:XAUUSD"; 
            if (normalized == "GUMUS" || normalized == "SILVER" || normalized == "XAGUSD") return "OANDA:XAGUSD"; 
            
            if (normalized == "BAKIR" || normalized == "COPPER" || normalized == "HG") return "OANDA:XCUUSD"; 
            
            if (normalized == "PETROL" || normalized == "BRENT" || normalized == "UKOIL") return "TVC:UKOIL";
            if (normalized == "WTI" || normalized == "USOIL" || normalized == "HAM") return "TVC:USOIL";
            
            if (normalized == "PLATIN" || normalized == "PLATINUM") return "TVC:PLATINUM";
            
            if (normalized == "GAZ" || normalized == "DOGALGAZ" || normalized == "NATGAS" || normalized == "NG") return "FX_IDC:XNGUSD";
            
            if (normalized == "BUGDAY" || normalized == "WHEAT") return "CBOT:ZW1!"; 
            if (normalized == "MISIR" || normalized == "CORN") return "CBOT:ZC1!";
            if (normalized == "PAMUK" || normalized == "COTTON") return "ICEUS:CT1!";
            if (normalized == "SIGIR" || normalized == "CATTLE") return "CME:LC1!";
            if (normalized == "KERESTE" || normalized == "LUMBER") return "CME:LBS1!";

            // Default fallback
            return $"TVC:{normalized}"; 
        }

        private string GetIndexTicker(string sym)
        {
            // Normalize Turkish to English
            string normalized = sym;
            if (sym.Contains("S&P") || sym.Contains("500")) normalized = "SPX";
            else if (sym.Contains("NIKKEI")) normalized = "NIKKEI225";
            else if (sym.Contains("DOW")) normalized = "DJI";
            else if (sym.Contains("DAX")) normalized = "DAX";
            
            if (normalized == "SP500" || normalized == "SPX" || normalized == "S&P500") return "FOREXCOM:SPX500";
            if (normalized == "NASDAQ" || normalized == "NDX" || normalized == "NAS100") return "FOREXCOM:NAS100";
            if (normalized == "DOW" || normalized == "DJI" || normalized == "US30") return "FOREXCOM:DJI";
            if (normalized == "DAX" || normalized == "GER30" || normalized == "DE30") return "XETRA:DAX";
            if (normalized == "VIX") return "TVC:VIX";
            if (normalized == "DXY" || normalized == "DX") return "TVC:DXY";
            if (normalized == "FTSE" || normalized == "FTSE100") return "CURRENCYCOM:UK100";
            if (normalized == "CAC" || normalized == "CAC40") return "TVC:PX1";
            if (normalized == "HSI" || normalized == "HANGSENG") return "HSI:HSI";
            
            return $"TVC:{normalized}";
        }

        private async Task<bool> IsTradingViewSymbolValid(string tvSymbol)
        {
            try
            {
                // Clean input (remove Exchange:)
                // e.g. BIST:THYAO -> THYAO, BINANCE:BTCUSDT -> BTCUSDT
                // Actually the search API needs just text.
                
                // If it contains complex basis math (e.g. /FX_IDC:USDTRY), skip validation
                if (tvSymbol.Contains("/") || tvSymbol.Contains("+")) return true;

                string cleanSym = tvSymbol;
                string exchange = "";
                
                if (tvSymbol.Contains(":"))
                {
                    var parts = tvSymbol.Split(':');
                    exchange = parts[0];
                    cleanSym = parts[1];
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3); // Fast timeout
                
                // TradingView Symbol Search API
                string url = $"https://symbol-search.tradingview.com/symbol_search/v3/?text={cleanSym}&hl=tr&exchange={exchange}&lang=tr&domain=tr";
                
                var resp = await client.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return true; // Fail open if API down
                
                var json = await resp.Content.ReadAsStringAsync();
                
                // If result array contains "symbol": "THYAO" etc.
                return json.Contains($"\"symbol\":\"{cleanSym}\"") || json.Contains($"\"symbol\":\"{cleanSym.Replace("1!","")}\"");
            }
            catch 
            {
                return true; // Fail open (assume valid if check fails)
            }
        }

        /// <summary>
        /// Yfinance'ten hesaplanan pivot JSON dosyasını oku ve PivotData döndür
        /// Static method - tüm service'ler tarafından kullanılabilir
        /// </summary>
        public static PivotData? LoadPivotDataFromJson(string symbol, string screenshotPath)
        {
            try
            {
                string screenshotDir = Path.GetDirectoryName(screenshotPath);
                string dateStr = DateTime.Now.ToString("yyyyMMdd");
                string pivotJsonPath = Path.Combine(screenshotDir, $"{symbol}_pivots_{dateStr}.json");
                
                if (!File.Exists(pivotJsonPath))
                {
                    return null;
                }

                string jsonContent = File.ReadAllText(pivotJsonPath);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("pivots", out JsonElement pivots))
                    {
                        var pivotData = new PivotData
                        {
                            Pivot = pivots.GetProperty("pivot").GetDecimal(),
                            R1 = pivots.GetProperty("r1").GetDecimal(),
                            R2 = pivots.GetProperty("r2").GetDecimal(),
                            R3 = pivots.GetProperty("r3").GetDecimal(),
                            S1 = pivots.GetProperty("s1").GetDecimal(),
                            S2 = pivots.GetProperty("s2").GetDecimal(),
                            S3 = pivots.GetProperty("s3").GetDecimal(),
                            IntervalLabel = pivots.TryGetProperty("interval_label", out JsonElement label) ? label.GetString() ?? "GÜNLÜK" : "GÜNLÜK",
                            CalculatedFromDate = pivots.GetProperty("calculated_from_date").GetString() ?? "",
                            ValidForDate = pivots.GetProperty("valid_for_date").GetString() ?? ""
                        };
                        return pivotData;
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Verilen tarihten önceki iş gününü bulur (Cumartesi/Pazar/Tatil hariç)
        /// </summary>
        private DateTime GetPreviousTradingDay(DateTime date)
        {
            // Borsanın resmi tatil günleri (sabit)
            var officialHolidays = new HashSet<string> { "01.01", "04.23", "05.01", "05.19", "07.15", "08.30", "10.29" };

            DateTime current = date.AddDays(-1);

            while (true)
            {
                // Hafta sonu kontrol
                if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
                {
                    current = current.AddDays(-1);
                    continue;
                }

                // Resmi tatil kontrol
                string dateStr = current.ToString("MM.dd");
                if (officialHolidays.Contains(dateStr))
                {
                    current = current.AddDays(-1);
                    continue;
                }

                // İş günü bulundu
                return current;
            }
        }
    }
}
