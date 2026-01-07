using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Fetches real-time prices from public APIs 
    /// Supports: Crypto (Binance), BIST (Yahoo), Forex (Yahoo), Commodities (Yahoo), US Stocks (Yahoo)
    /// </summary>
    public class PriceFetchService
    {
        private static readonly HttpClient _client;
        
        static PriceFetchService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
            _client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        /// <summary>
        /// Fetch current price for a symbol based on market type
        /// </summary>
        public async Task<PriceInfo?> GetPriceAsync(string symbol, string marketType)
        {
            // Remove try-catch to allow debugging errors in ManualAnalysisService
            return marketType switch
            {
                "Kripto" => await GetCryptoPriceAsync(symbol),
                "BIST" => await GetYahooFinancePriceAsync(symbol, ".IS", "TL"),
                "Forex" => await GetForexOrCommodityPriceAsync(symbol),
                "ABD" => await GetYahooFinancePriceAsync(symbol, "", "USD"),
                "Almanya" => await GetYahooFinancePriceAsync(symbol, ".DE", "EUR"),
                "İngiltere" => await GetYahooFinancePriceAsync(symbol, ".L", "GBP"),
                "Japonya" => await GetYahooFinancePriceAsync(symbol, ".T", "JPY"),
                "Çin" => await GetYahooFinancePriceAsync(symbol, ".SS", "CNY"),
                _ => await GetYahooFinancePriceAsync(symbol, "", "USD") // Default to Yahoo
            };
        }

        /// <summary>
        /// Get crypto price from Binance API
        /// </summary>
        private async Task<PriceInfo?> GetCryptoPriceAsync(string symbol)
        {
            // Normalize symbol format for Binance (e.g., BTC -> BTCUSDT, BTCUSD -> BTCUSDT)
            string binanceSymbol = symbol.ToUpper().Replace("/", "").Replace(" ", "");
            
            // Handle common suffixes
            if (binanceSymbol.EndsWith("USD") && !binanceSymbol.EndsWith("USDT"))
            {
                // BTCUSD -> BTCUSDT
                binanceSymbol = binanceSymbol.Substring(0, binanceSymbol.Length - 3) + "USDT";
            }
            else if (!binanceSymbol.EndsWith("USDT") && !binanceSymbol.EndsWith("BUSD") && !binanceSymbol.EndsWith("BTC") && !binanceSymbol.EndsWith("ETH"))
            {
                // BTC -> BTCUSDT
                binanceSymbol = binanceSymbol + "USDT";
            }

            Console.WriteLine($"[DEBUG] Crypto price request: {symbol} -> {binanceSymbol}");

            string url = $"https://api.binance.com/api/v3/ticker/24hr?symbol={binanceSymbol}";
            
            var response = await _client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            
            var root = doc.RootElement;
            
            return new PriceInfo
            {
                Symbol = symbol.ToUpper(),
                Price = ParseDecimal(root.GetProperty("lastPrice").GetString()),
                Change24h = ParseDecimal(root.GetProperty("priceChangePercent").GetString()),
                High24h = ParseDecimal(root.GetProperty("highPrice").GetString()),
                Low24h = ParseDecimal(root.GetProperty("lowPrice").GetString()),
                Volume24h = ParseDecimal(root.GetProperty("volume").GetString()),
                Currency = "USD"
            };
        }

        /// <summary>
        /// Get Forex or Commodity price from Yahoo Finance
        /// Supports: XAUUSD, EURUSD, USDTRY, etc.
        /// </summary>
        private async Task<PriceInfo?> GetForexOrCommodityPriceAsync(string symbol)
        {
            // Normalize Turkish chars and remove separators so "Euro/Dolar" -> "EURODOLAR", "Doğalgaz" -> "DOGALGAZ"
            string raw = symbol.ToUpperInvariant()
                .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C")
                .Replace("/", "").Replace(" ", "").Replace("-", "");

            // Map common symbols (English + Turkish aliases) to Yahoo format
            string yahooSymbol = raw switch
            {
                // FX Majors
                "EURUSD" or "EURODOLAR" => "EURUSD=X",
                "GBPUSD" or "STERLINDOLAR" => "GBPUSD=X",
                "USDJPY" or "DOLARYEN" => "USDJPY=X",
                "USDCHF" or "DOLARFRANK" => "USDCHF=X",
                "USDCAD" or "DOLARKANADA" or "DOLARKANADADOLARI" => "USDCAD=X",
                "AUDUSD" or "AVUSTRALYADOLARI" => "AUDUSD=X",
                "NZDUSD" or "YENIZELANDADOLARI" => "NZDUSD=X",
                "USDTRY" or "DOLARTL" or "DOLARTRY" => "USDTRY=X",
                "EURTRY" or "EUROTL" or "EUROTRY" => "EURTRY=X",
                "GBPTRY" or "STERLINTL" or "STERLINTRY" => "GBPTRY=X",

                // Crypto (via Yahoo)
                "BTCUSD" or "BTCUSDT" or "BTC" => "BTC-USD",
                "ETHUSD" or "ETHUSDT" or "ETH" => "ETH-USD",

                // Precious metals
                "XAUUSD" or "GOLD" or "ALTIN" or "ALTINUSD" => "GC=F",
                "XAGUSD" or "SILVER" or "GUMUS" or "GUMUSUSD" => "SI=F",
                "XPTUSD" or "PLATINUM" or "PLATIN" => "PL=F",
                "XPDUSD" or "PALLADIUM" or "PALADYUM" => "PA=F",

                // Energy
                "WTI" or "CRUDE" or "CL" or "PETROL" or "HAM" => "CL=F",
                "BRENT" or "UKOIL" => "BZ=F",
                "NG" or "NATGAS" or "DOGALGAZ" => "NG=F",

                // Industrial metals
                "BAKIR" or "COPPER" or "XCUUSD" => "HG=F",
                "ALUMINUM" or "ALUMINYUM" or "ALUMINYUM" or "ALUMINYUM" or "ALUMINYUM" => "ALI=F",
                "ZINC" or "CINKO" => "ZNC=F",
                "NICKEL" or "NIKEL" => "NID=F",
                "LEAD" or "KURSUN" => "LDS=F",
                "TIN" or "KALAY" => "TIN=F",

                // Softs & grains
                "COCOA" or "KAKAO" => "CC=F",
                "COFFEE" or "KAHVE" => "KC=F",
                "COTTON" or "PAMUK" => "CT=F",
                "SUGAR" or "SEKER" => "SB=F",
                "WHEAT" or "BUGDAY" => "ZW=F",
                "CORN" or "MISIR" or "MISIR" => "ZC=F",
                "SOYBEAN" or "SOYA" or "SOYAFASULYESI" => "ZS=F",
                "LUMBER" or "KERESTE" => "LB=F",

                // ETF fallbacks when futures unavailable
                "URANYUM" or "URANIUM" => "URA",
                "LITHIUM" or "LITYUM" => "LIT",
                "CARBON" or "KARBON" => "KRBN",

                _ => raw.Contains("=") ? raw : raw + "=X"  // Default Forex format
            };
            
            return await GetYahooFinancePriceAsync(yahooSymbol, "", "USD", skipSuffix: true);
        }

        /// <summary>
        /// Universal Yahoo Finance price fetcher
        /// </summary>
        private async Task<PriceInfo?> GetYahooFinancePriceAsync(string symbol, string suffix, string currency, bool skipSuffix = false)
        {
            string yahooSymbol = skipSuffix ? symbol : (symbol.ToUpper() + suffix);
            
            string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}?interval=1d&range=5d";
            
            try 
            {
                var response = await _client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {response.StatusCode} for URL: {url}");
                }

                string content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                
                var chart = doc.RootElement.GetProperty("chart");
                
                // Check for error
                if (chart.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    throw new Exception($"Yahoo API Error: {error.GetProperty("description").GetString()} (URL: {url})");
                }
                
                var result = chart.GetProperty("result")[0];
                var meta = result.GetProperty("meta");
                
                decimal currentPrice = meta.GetProperty("regularMarketPrice").GetDecimal();
                decimal previousClose = meta.TryGetProperty("chartPreviousClose", out var prev) ? prev.GetDecimal() : currentPrice;
                decimal change = previousClose > 0 ? ((currentPrice - previousClose) / previousClose) * 100 : 0;
                
                // Try to get high/low from quotes
                decimal high24h = currentPrice;
                decimal low24h = currentPrice;
                
                if (result.TryGetProperty("indicators", out var indicators) &&
                    indicators.TryGetProperty("quote", out var quotes) &&
                    quotes.GetArrayLength() > 0)
                {
                    var quote = quotes[0];
                    if (quote.TryGetProperty("high", out var highs))
                    {
                        foreach (var h in highs.EnumerateArray())
                        {
                            if (h.ValueKind == JsonValueKind.Number)
                            {
                                var hVal = h.GetDecimal();
                                if (hVal > high24h) high24h = hVal;
                            }
                        }
                    }
                    if (quote.TryGetProperty("low", out var lows))
                    {
                        foreach (var l in lows.EnumerateArray())
                        {
                            if (l.ValueKind == JsonValueKind.Number)
                            {
                                var lVal = l.GetDecimal();
                                if (lVal < low24h || low24h == currentPrice) low24h = lVal;
                            }
                        }
                    }
                }

                return new PriceInfo
                {
                    Symbol = !string.IsNullOrEmpty(suffix) ? symbol.ToUpper().Replace(suffix, "") : symbol.ToUpper(),
                    Price = currentPrice,
                    Change24h = Math.Round(change, 2),
                    High24h = high24h,
                    Low24h = low24h,
                    Currency = meta.TryGetProperty("currency", out var curr) ? curr.GetString() ?? currency : currency
                };
            }
            catch (Exception ex)
            {
                // Helpful hints for common user errors
                string extraHint = "";
                if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    if (yahooSymbol.Contains("XAUUSD.IS") || yahooSymbol.Contains("GOLD.IS"))
                        extraHint = " (İpucu: 'BIST' yerine 'Forex' seçin)";
                    else if (yahooSymbol.Contains("BTC") && yahooSymbol.Contains(".IS"))
                        extraHint = " (İpucu: 'BIST' yerine 'Kripto' seçin)";
                }

                // Re-throw with URL context if not already present
                if (!ex.Message.Contains(url))
                    throw new Exception($"{ex.Message}{extraHint} (URL: {url})");
                
                if (!string.IsNullOrEmpty(extraHint))
                     throw new Exception($"{ex.Message}{extraHint}");

                throw;
            }
        }
        
        private static decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
    }

    public class PriceInfo
    {
        public string Symbol { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Change24h { get; set; }
        public decimal High24h { get; set; }
        public decimal Low24h { get; set; }
        public decimal Volume24h { get; set; }
        public string Currency { get; set; } = "";

        public string FormatPrice()
        {
            if (Currency == "USDT" || Currency == "USD")
                return $"${Price:N2}";
            else if (Currency == "TRY" || Currency == "TL")
                return $"₺{Price:N2}";
            else if (Currency == "EUR")
                return $"€{Price:N2}";
            else if (Currency == "GBP")
                return $"£{Price:N2}";
            else
                return $"{Price:N2} {Currency}";
        }

        public string FormatChange()
        {
            string arrow = Change24h >= 0 ? "📈" : "📉";
            string sign = Change24h >= 0 ? "+" : "";
            return $"{arrow} %{sign}{Change24h:N2}";
        }
        
        public string FormatFullInfo()
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"💰 {Symbol}: {FormatPrice()}");
            if (Change24h != 0)
                parts.Add(FormatChange());
            if (High24h > 0 && Low24h > 0 && High24h != Low24h)
                parts.Add($"📊 Gün: {Low24h:N2} - {High24h:N2}");
            return string.Join(" | ", parts);
        }
    }
}

