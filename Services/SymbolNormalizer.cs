using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace XiDeAI_Pro.Services
{
    public static class SymbolNormalizer
    {
        public static string NormalizeSignalSymbol(string rawSymbol)
        {
            if (string.IsNullOrWhiteSpace(rawSymbol)) return string.Empty;

            string symbol = rawSymbol.Trim().ToUpperInvariant();
            symbol = symbol.Replace("İ", "I").Replace("'", "").Replace("`", "").Replace("\"", "");
            symbol = Regex.Replace(symbol, @"\s+", "");

            while (symbol.StartsWith("VIPVIP-", StringComparison.OrdinalIgnoreCase))
                symbol = "VIP-" + symbol.Substring("VIPVIP-".Length);

            if (symbol.StartsWith("VIP-", StringComparison.OrdinalIgnoreCase))
                symbol = symbol.Substring(4);

            if (symbol.StartsWith("BIST:", StringComparison.OrdinalIgnoreCase))
                symbol = symbol.Substring(5);

            symbol = Regex.Replace(symbol, @"[^A-Z0-9]", "");
            return symbol;
        }

        public static bool IsKnownBistSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return false;
            string normalized = NormalizeSignalSymbol(symbol);
            if (normalized.Length < 3 || normalized.Length > 6) return false;

            var info = SymbolData.GetSymbolInfo("BIST", normalized);
            if (info != null) return true;

            // Config yoksa sert çökme yerine makul BIST formatına izin ver; kısa kırpılmış sembolleri engelle.
            var bistSymbols = SymbolData.LoadSymbols("BIST");
            if (bistSymbols.Count == 0)
                return Regex.IsMatch(normalized, @"^[A-Z]{4,6}$");

            return bistSymbols.Any(s => s.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
