using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

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
                Log($"Analiz Başlatılıyor: {symbol} ({marketType}) [{timeFrame}]");

                // 0. Get Historical Context from Shared Memory
                string historicalContext = _memory.GetSymbolContext(symbol);
                if (!string.IsNullOrEmpty(historicalContext))
                {
                    Log("📜 Geçmiş analiz hafızası yüklendi (30 gün).");
                }

                // 1. Get Price Info
                var priceInfo = await _priceService.GetPriceAsync(symbol, marketType);
                if (priceInfo != null)
                {
                    result.PriceInfo = priceInfo;
                    Log($"Fiyat alındı: {priceInfo.Price}");
                }

                // 2. Pre-Check: Validate TradingView Symbol Existence
                string tvSymbol = ConvertToTradingViewSymbol(symbol, marketType, basis);
                if (!await IsTradingViewSymbolValid(tvSymbol)) 
                {
                     Log($"⚠️ UYARI: TradingView üzerinde '{tvSymbol}' bulunamadı! Grafik hatalı olabilir.");
                     // We don't stop execution, but we log a strong warning.
                     // Often 'FX_IDC' vs 'OANDA' confusion causes this.
                }

                // 3. Generate Screenshot & Link
                // 3. Generate Screenshot & Link
                // tvSymbol is already computed above
                string linkInterval = (timeFrame == "G" || timeFrame == "Daily") ? "D" : timeFrame;
                
                string tvLink = string.IsNullOrEmpty(chartId)
                    ? $"https://tr.tradingview.com/chart/?symbol={tvSymbol}&interval={linkInterval}"
                    : $"https://tr.tradingview.com/chart/{chartId}/?symbol={tvSymbol}&interval={linkInterval}";
                
                result.TvLink = tvLink;
                
                string? screenshotPath = null;
                try
                {
                    if (_screenshotService != null)
                    {
                        Log("📸 Ekran görüntüsü alınıyor...");
                        string period = timeFrame;
                        if(timeFrame == "G" || timeFrame == "Daily") period = "D"; 
                        
                        screenshotPath = await _screenshotService.CaptureChart(tvSymbol, period, chartId);
                        if (File.Exists(screenshotPath))
                        {
                            result.ScreenshotPath = screenshotPath;
                            Log("✅ Ekran görüntüsü alındı.");
                            
                            // Try to read pivot data from JSON file
                            var pivotData = LoadPivotDataFromJson(symbol, screenshotPath);
                            if (pivotData != null)
                            {
                                result.PivotData = pivotData;
                                Log($"✅ Pivot seviyeleri yüklendi: P={pivotData.Pivot}, R1={pivotData.R1}, S1={pivotData.S1}");
                            }
                        }
                    }
                }
                catch (Exception ex) { Log($"⚠️ Screenshot hatası: {ex.Message}"); }
                
                string priceContext = priceInfo != null 
                    ? $"Fiyat: {priceInfo.Price}, Değişim: %{priceInfo.Change24h}" 
                    : "Fiyat verisi alınamadı.";

                // PIVOT TARİH KONTEKSTI EKLE
                // Tarama sonuçları kapanan günün verilerine göre oluşuluyor, 
                // fakat sonraki iş günü için paylaşılıyor
                DateTime today = DateTime.Now;
                DateTime pivotDate = GetPreviousTradingDay(today);
                string pivotDateStr = pivotDate.ToString("dd.MM.yyyy");
                string todayStr = today.ToString("dd.MM.yyyy");
                
                string dateContext = $"\n📅 TARAMA TARİHİ: {todayStr}\n" +
                    $"📊 PIVOT VERİSİ TARİHİ: {pivotDateStr} (önceki iş günü kapanış verilerine göre hesaplanmıştır)\n" +
                    $"⚠️ AÇIKLAMA: Günlük pivot seviyeleri ({pivotDateStr}) ve Fibonacci oranları, " +
                    $"bu tarihte kapanan mumun (High, Low, Close) verilerine göre hesaplanmıştır. " +
                    $"Haftalık/Aylık pivotlar ise ilgili dönemin son kapanış verilerine göre oluşturulmuştur.\n";
                
                priceContext += dateContext;
                
                // 🎯 YFİNANCE VE VİSİON VERİLERİNİ SENTEZLE
                if (result.PivotData != null)
                {
                    var pd = result.PivotData;
                    string pivotValues = $"\n📍 TEKNİK SEVİYE ANALİZİ ({pd.CalculatedFromDate} Kapanış Verileriyle):\n" +
                        $"  • R3 Direnç: {pd.R3:N2}\n" +
                        $"  • R2 Direnç: {pd.R2:N2}\n" +
                        $"  • R1 Direnç: {pd.R1:N2}\n" +
                        $"  • P (Pivot): {pd.Pivot:N2}\n" +
                        $"  • S1 Destek: {pd.S1:N2}\n" +
                        $"  • S2 Destek: {pd.S2:N2}\n" +
                        $"  • S3 Destek: {pd.S3:N2}\n" +
                        $"\n⚠️ NOT: Yukarıdaki rakamlar yfinance verilerine dayalı HESAPLANMIŞ kesin seviyelerdir. " +
                        $"Aşağıdaki Vision API (Görsel Analiz) sonuçları ise bu seviyelerin grafikteki GÖRSEL doğrulamasıdır. " +
                        $"Eğer Vision 'Yok' diyorsa bile yukarıdaki hesaplanmış seviyeleri baz al.\n";
                    priceContext += pivotValues;
                    Log($"📊 Kesin pivot değerleri ve Vision sentez notu AI prompt'una enjekte edildi");
                }

                // Inject History & Basis into PriceContext
                if (!string.IsNullOrEmpty(basis) && basis != "TL")
                {
                    string basisNote = basis == "XU100" 
                        ? "⚠️ BU BİR KOMPOZİT ANALİZDİR. Hisseyi endeks bazlı (relatif) yorumla." 
                        : $"⚠️ BU BİR DÖVİZ BAZLI ANALİZDİR. Seviyeleri ve grafiği {basis} birimi üzerinden değerlendir.";
                    
                    priceContext = $"{basisNote}\n{priceContext}";
                }

                if (!string.IsNullOrEmpty(historicalContext))
                {
                    priceContext += "\nGEÇMİŞ ANALİZ:\n" + historicalContext;
                }

                // 3. Search for influencer posts
                var influencerPosts = new List<InfluencerPost>();
                try
                {
                    Log("Influencer yorumları taranıyor...");
                    if (_socialIntel != null)
                    {
                        var vipHandles = _influencerControl?.GetTopInfluencers(symbol, 20);
                        influencerPosts = await _socialIntel.FindInfluencerAnalyses(symbol, marketType, vipHandles);
                        
                        // Extra safety filter: Remove spam posts with private links (EXCEPT for Guru)
                        var cleanedPosts = influencerPosts
                            .Where(p => p.Handle.Equals("EFELERiiNEFESi3", StringComparison.OrdinalIgnoreCase) || 
                                       !ContentQualityGuard.ContainsPrivateLinks(p.Content))
                            .ToList();
                        if (cleanedPosts.Count < influencerPosts.Count)
                        {
                            Log($"🚫 {influencerPosts.Count - cleanedPosts.Count} spam post filtrelendi (telegram/discord linki)");
                            influencerPosts = cleanedPosts;
                        }
                        
                        Log($"🔎 {influencerPosts.Count} influencer yorumu bulundu.");
                        result.InfluencerPosts = influencerPosts;
                    }
                }
                catch (Exception ex) { Log($"⚠️ Influencer tarama hatası: {ex.Message}"); }

                // 4. EXTRACT INDICATORS FROM SCREENSHOT (Pre-populate Gemini context)
                string indicatorContext = "";
                if (screenshotPath != null && File.Exists(screenshotPath))
                {
                    try
                    {
                        Log("📊 Grafikteki göstergeler analiz ediliyor (RSI, MACD, Pivot, Fibo, Divergence)...");
                        var extractor = new IndicatorExtractor(_geminiService, Log);
                        var indicatorAnalysis = await extractor.ExtractIndicatorsFromScreenshot(screenshotPath);
                        
                        if (!string.IsNullOrEmpty(indicatorAnalysis.SummaryContext))
                        {
                            indicatorContext = indicatorAnalysis.SummaryContext;
                            Log("✅ Göstergeler çıkartıldı, Gemini prompt'una injekte ediliyor...");
                            
                            // Inject into priceContext so Gemini sees the numbers
                            priceContext += "\n\n" + indicatorContext;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ Gösterge çıkarma hatası (devam ediliyor): {ex.Message}");
                    }
                }

                // 5. Generate AI Analysis
                string? analysis = null;
                
                Log("🤖 Gemini AI analiz yazıyor...");
                if (_geminiService == null) throw new Exception("Gemini servisi başlatılamadı.");

                // Format influencer citations for passing to analysis
                string influencerContext = "";
                if (influencerPosts != null && influencerPosts.Count > 0)
                {
                    var citationList = new System.Collections.Generic.List<string>();
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
                    result.ReportText = $"AI analizi oluşturulamadı. (Hata: {_geminiService.LastError})";
                    return result;
                }

                // 4. Format output and SAVE TO MEMORY
                result.ReportText = analysis;
                result.InfluencerPosts = influencerPosts;

                // Save to Shared Memory with Sentiment Extraction
                string sentimentTag = "";
                if (analysis.Contains("Açığa Satış") || analysis.Contains("Düşüş Yönlü")) sentimentTag = "\n[SENTİMENT: SHORT]";
                else if (analysis.Contains("Uzun Pozisyon") || analysis.Contains("Yükseliş Yönlü")) sentimentTag = "\n[SENTİMENT: LONG]";

                _memory.StoreAnalysis(symbol, "MANUAL", analysis + sentimentTag);
                Log("💾 Analiz hafızaya kaydedildi.");

                string debugOutput = analysis;
                if (!string.IsNullOrEmpty(tvLink)) debugOutput += $"\n\n📊 Grafik: {tvLink}";
                result.ReportText = debugOutput;
                result.Success = true;

                return result;
            }
            catch (Exception ex)
            {
                result.ReportText = $"Analiz sırasında hata oluştu: {ex.Message}";
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
            Add("MÍSIR", "CORN"); Add("MISIR", "CORN");
            Add("KAHVE", "COFFEE");
            Add("ŞEKER", "SUGAR"); Add("SEKER", "SUGAR");
            Add("KAKAO", "COCOA");
            Add("ALTÍN", "XAUUSD"); Add("ALTIN", "XAUUSD");
            Add("GÜMÜŞ", "XAGUSD"); Add("GUMUS", "XAGUSD");
            Add("PLATINUM", "PLATINUM"); Add("PLATIN", "PLATINUM");
            Add("PALADIUM", "XPDUSD"); Add("PALADYUM", "XPDUSD");
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
            Add("ALTIN/DOLAR", "XAUUSD"); Add("ALTÍN/DOLAR", "XAUUSD");

            // Endeksler - Turkish to English
            Add("S&P500", "SPX"); Add("SP500", "SPX");
            Add("DOW JONES", "DJI"); Add("DOWJONES", "DJI");
            Add("NIKKEI 225", "NIKKEI225"); Add("NIKKEI225", "NIKKEI225");
            Add("HONG KONG 50", "HANGSENG"); Add("HONGKONG50", "HANGSENG");
            Add("BREZILYA", "BOVESPA");
            Add("ARJANTIN", "MERVAL");

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

            // 0.5 Convert Turkish names to English
            string englishSymbol = TurkishToEnglishSymbol(symbol);
            
            string upperSym = englishSymbol.ToUpperInvariant().Trim();
            string baseSym = "";
            
            // VIP/Futures Special Handling
            if (upperSym.StartsWith("VIP-"))
            {
                string core = upperSym.Replace("VIP-", "");
                if (core == "X030T") baseSym = "BIST:XU030D1"; 
                else baseSym = $"BIST:{core}1!";
                return CheckBasis(baseSym, basis);
            }

            switch (marketType)
            {
                case "Kripto":
                    // Default to Binance, but fallback to others if needed? No, usually Binance is liquid enough.
                    // Handle BTC/ETH without USDT suffix if user forgets
                    if (!upperSym.EndsWith("USDT") && !upperSym.EndsWith("TRY") && !upperSym.EndsWith("BTC"))
                        upperSym += "USDT";
                    baseSym = $"BINANCE:{upperSym}";
                    break;

                case "BIST":
                    baseSym = $"BIST:{upperSym}";
                    break;

                case "Forex":
                    baseSym = $"FX:{upperSym}";
                    // OANDA is often better for generic FX in TV
                    if (!upperSym.Contains("TRY")) // Major pairs often OANDA or FX_IDC
                        baseSym = $"FX_IDC:{upperSym}"; 
                    break;

                case "ABD":
                    baseSym = $"NASDAQ:{upperSym}"; // Fallback logic could be added for NYSE
                    break;

                case "Emtia":
                    baseSym = GetCommodityTicker(upperSym);
                    break;

                case "Endeks":
                    baseSym = GetIndexTicker(upperSym);
                    break;

                default:
                    baseSym = upperSym;
                    break;
            }

            return CheckBasis(baseSym, basis);
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
            else if (sym.Contains("ALTÍN") || sym.Contains("ALTIN") || sym.Contains("Altın")) normalized = "XAUUSD";
            else if (sym.Contains("GÜMÜŞ") || sym.Contains("GUMUS") || sym.Contains("Gümüş")) normalized = "XAGUSD";
            else if (sym.Contains("BAKIR") || sym.Contains("COPPER")) normalized = "COPPER";
            else if (sym.Contains("PETROL") || sym.Contains("HAM")) normalized = "USOIL";
            else if (sym.Contains("BRENT") || sym.Contains("UKOIL")) normalized = "BRENT";
            else if (sym.Contains("DOĞALGAZ") || sym.Contains("DOGALGAZ")) normalized = "NATGAS";
            
            // Common Turkish and English aliases (Priority: SPOT/CFD)
            if (normalized == "ALTIN" || normalized == "GOLD" || normalized == "XAUUSD" || normalized == "ONS") return "OANDA:XAUUSD"; // Spot Gold
            if (normalized == "GUMUS" || normalized == "SILVER" || normalized == "XAGUSD") return "OANDA:XAGUSD"; // Spot Silver
            
            // Copper Spot (CFD) instead of Futures
            if (normalized == "BAKIR" || normalized == "COPPER" || normalized == "HG") return "OANDA:XCUUSD"; 
            
            // Oil Spot (CFD)
            if (normalized == "PETROL" || normalized == "BRENT" || normalized == "UKOIL") return "TVC:UKOIL";
            if (normalized == "WTI" || normalized == "USOIL" || normalized == "HAM") return "TVC:USOIL";
            
            if (normalized == "PLATIN" || normalized == "PLATINUM") return "TVC:PLATINUM";
            
            // Natural Gas Spot (CFD) instead of Futures
            if (normalized == "GAZ" || normalized == "DOGALGAZ" || normalized == "NATGAS" || normalized == "NG") return "FX_IDC:XNGUSD";
            
            if (normalized == "BUGDAY" || normalized == "WHEAT") return "CBOT:ZW1!"; // Some agros are best as Futures
            if (normalized == "MISIR" || normalized == "CORN") return "CBOT:ZC1!";
            
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
                            CalculatedFromDate = pivots.GetProperty("calculated_from_date").GetString() ?? "",
                            ValidForDate = pivots.GetProperty("valid_for_date").GetString() ?? ""
                        };
                        return pivotData;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
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
