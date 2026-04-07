using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public class SymbolInfo
    {
        public string Symbol { get; set; } = "";
        public string TurkishName { get; set; } = "";
        public string YahooSymbol { get; set; } = "";
        public string TradingViewSymbol { get; set; } = "";
        public string Category { get; set; } = "";
    }

    public static class SymbolData
    {
        private static Dictionary<string, List<SymbolInfo>> _symbolCache = new Dictionary<string, List<SymbolInfo>>();

        public static string[] GetSymbols(string market)
        {
            var symbols = LoadSymbols(market);
            var result = new List<string>();
            
            foreach (var symbol in symbols)
            {
                result.Add(symbol.Symbol);
                if (!string.IsNullOrEmpty(symbol.TurkishName))
                    result.Add(symbol.TurkishName);
            }
            
            return result.ToArray();
        }

        public static List<SymbolInfo> LoadSymbols(string market)
        {
            // Check cache first
            if (_symbolCache.ContainsKey(market))
                return _symbolCache[market];

            string? filename = market switch
            {
                "BIST" => "symbols_bist.txt",
                "Forex" => "symbols_forex.txt",
                "Kripto" => "symbols_crypto.txt",
                "Endeks" => "symbols_indices.txt",
                "Emtia" => "symbols_commodities.txt",
                _ => null
            };

            if (filename == null)
                return new List<SymbolInfo>();

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", filename);
                if (!File.Exists(configPath))
                    return new List<SymbolInfo>();

                var symbols = new List<SymbolInfo>();
                var lines = File.ReadAllLines(configPath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    // Format: SYMBOL|Turkish Name|Yahoo Symbol|TradingView Symbol|Category
                    var parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        symbols.Add(new SymbolInfo
                        {
                            Symbol = parts[0].Trim(),
                            TurkishName = parts[1].Trim(),
                            YahooSymbol = parts[2].Trim(),
                            TradingViewSymbol = parts[3].Trim(),
                            Category = parts[4].Trim()
                        });
                    }
                }

                _symbolCache[market] = symbols;
                return symbols;
            }
            catch
            {
                return new List<SymbolInfo>();
            }
        }

        public static SymbolInfo? GetSymbolInfo(string market, string symbol)
        {
            var symbols = LoadSymbols(market);
            return symbols.FirstOrDefault(s => 
                s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
                s.TurkishName.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetYahooSymbol(string market, string symbol)
        {
            var info = GetSymbolInfo(market, symbol);
            return info?.YahooSymbol ?? symbol;
        }

        public static string GetTradingViewSymbol(string market, string symbol)
        {
            var info = GetSymbolInfo(market, symbol);
            return info?.TradingViewSymbol ?? symbol;
        }
        public static string DetectMarket(string symbol)
        {
            var markets = new[] { "Kripto", "BIST", "Forex", "Emtia", "Endeks", "ABD" };
            foreach (var market in markets)
            {
                if (GetSymbolInfo(market, symbol) != null) return market;
            }
            
            // Heuristics fallback
            string upper = symbol.ToUpper();
            if (upper.Contains("USD") || upper.Contains("EUR") || upper.Contains("JPY")) return "Forex";
            if (upper == "XU100" || upper == "XU030") return "Endeks";
            if (upper.Length == 5 && !upper.Contains(".")) return "BIST";
            
            return "Forex"; // Default safe fallback
        }
    }
}
