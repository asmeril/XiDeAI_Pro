using System;
using System.Collections.Generic;

namespace XiDeAI_Pro.Services
{
    // v5.1.0: Simplified for Alpha/PreMove only
    public enum ContentTier
    {
        Premium,      // AKTIF (Roket) sinyalleri
        Standard,     // AKTIF normal sinyaller
        Summary,      // PULLBACK_ADAY sinyalleri
        Notification  // Diger
    }

    public class SignalData
    {
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = ""; // ALPHA, PREMOVE
        public string Period { get; set; } = "";   // 60, G
        public decimal Price { get; set; }
        public string Durum { get; set; } = "";    // AKTIF, PULLBACK_ADAY
        public bool IsRoket { get; set; } = false; // Alpha: volRatio>=3 + barSize>=1%
        public string Source { get; set; } = "";   // ALPHA, PREMOVE
        public DateTime DetectedAt { get; set; }
        public bool IsRepeat { get; set; }
        public string Basis { get; set; } = "TL";
        public string? Analysis { get; set; }

        // --- Geriye Dönük Uyum Saplamaları ---
        // Eski robotlar kaldırıldı; bu alanlar sabitlere çevrildi
        public int Score { get; set; } = 100;
        public int MaxScore { get; set; } = 100;
        public int FinalScore => 100;
        public bool IsCommonScan => false;
        // OHLC alanları (eski robotlardan geliyordu, artık dolu değil)
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal IndexClose { get; set; }

        public ContentTier Tier
        {
            get
            {
                if (IsRoket) return ContentTier.Premium;
                if (Durum == "AKTIF") return ContentTier.Standard;
                if (Durum == "PULLBACK_ADAY") return ContentTier.Summary;
                return ContentTier.Notification;
            }
        }

        public string Market
        {
            get
            {
                if (string.IsNullOrEmpty(Symbol)) return "BIST";
                string upper = Symbol.ToUpperInvariant();
                if (upper.Contains("USDT") || upper.Contains("BTC") || upper.Contains("ETH")) return "Kripto";
                if (upper.Contains("XAU") || upper.Contains("EUR") || upper.Contains("GBP")) return "Forex";
                return "BIST";
            }
        }
    }

    public class SignalParser
    {
        public List<SignalData> Parse(string line, string source)
        {
            if (source == "ALPHA" || source == "PREMOVE")
                return ParseDbLine(line, source);
            return new List<SignalData>();
        }

        public List<SignalData> ParseDbLine(string line, string strategyOverride = "")
        {
            var results = new List<SignalData>();
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) return results;

            var parts = line.Split('|');
            if (parts.Length < 5) return results;

            try
            {
                string symbol = parts[0].Trim();
                string strategy = strategyOverride.Length > 0 ? strategyOverride : parts[1].Trim().ToUpperInvariant();
                string period = parts[2].Trim();
                string rawDurum = parts.Length >= 6 ? parts[5].Trim().ToUpperInvariant() : "AKTIF";
                bool isRoket = rawDurum.Contains("ROKET");
                string durum = isRoket ? "AKTIF" : rawDurum;

                decimal price = 0;
                decimal.TryParse(parts[4].Trim().Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out price);

                DateTime detectedAt = DateTime.Now;
                if (parts.Length >= 4)
                {
                    if (!DateTime.TryParse(parts[3].Trim(), out var parsedDate))
                        parsedDate = DateTime.Now;
                    detectedAt = parsedDate;
                }

                results.Add(new SignalData
                {
                    Symbol = symbol,
                    Strategy = strategy,
                    Period = period,
                    Price = price,
                    Durum = durum,
                    IsRoket = isRoket,
                    Source = strategy,
                    DetectedAt = detectedAt,
                    IsRepeat = false,
                    Basis = "TL"
                });
            }
            catch { }

            return results;
        }
    }
}
