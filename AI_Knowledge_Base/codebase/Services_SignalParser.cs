using System;
using System.Collections.Generic;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public class SignalData
    {
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = ""; // K, B, T, DIP, ZIRVE, ANKA
        public string Period { get; set; } = ""; // 15, 60, 240, G
        public decimal Price { get; set; }
        public int Score { get; set; }
        public int MaxScore { get; set; }
        public string Source { get; set; } = ""; // KING, DIP, ANKA
        public DateTime DetectedAt { get; set; }
        public bool IsRepeat { get; set; }
        public string Basis { get; set; } = "TL"; // TL, USD, EUR, XU100
        
        // Rich Report Data (V3)
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal IndexClose { get; set; } // XU100 Close
        public string? Analysis { get; set; } // Manual/AI Analysis Content

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
        public List<SignalData> Parse(string content, string source)
        {
            if (source == "KING") return ParseKingFormat(content, DateTime.Now);
            if (source == "DIP") return ParseDipZirveFormat(content, DateTime.Now, "DIP");
            if (source == "ANKA") return ParseAnkaFormat(content, DateTime.Now);
            return new List<SignalData>();
        }

        /// <summary>
        /// King/Bomba/TeFo formatı: Sembol|Strateji|Periyot|Fiyat|Hacim%|Fiyat%|Skor/25|Tekrar
        /// </summary>
        public List<SignalData> ParseKingFormat(string content, DateTime fileTime)
        {
            var results = new List<SignalData>();
            var lines = content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 7)
                {
                    try
                    {
                        var scoreParts = parts[6].Split('/');
                        var data = new SignalData
                        {
                            Symbol = parts[0].Trim(),
                            Strategy = parts[1].Trim(),
                            Period = parts[2].Trim(),
                            Price = decimal.Parse(parts[3].Trim()),
                            Score = int.Parse(scoreParts[0].Trim()),
                            MaxScore = scoreParts.Length > 1 ? int.Parse(scoreParts[1].Trim()) : 25,
                            Source = "KING",
                            DetectedAt = fileTime,
                            IsRepeat = parts.Length > 7 && parts[7].Trim() == "T"
                        };

                        // V3: OHLC + XU100 Parsing (Optional)
                        if (parts.Length >= 14) 
                        {
                            try {
                                data.Open = decimal.Parse(parts[8].Trim());
                                data.High = decimal.Parse(parts[9].Trim());
                                data.Low = decimal.Parse(parts[10].Trim());
                                data.Close = decimal.Parse(parts[11].Trim());
                                data.Volume = decimal.Parse(parts[12].Trim());
                                data.IndexClose = decimal.Parse(parts[13].Trim());
                            } catch { }
                        }
                        
                        results.Add(data);
                    }
                    catch { }
                }
            }
            return results;
        }

        /// <summary>
        /// Dip/Zirve formatı: Sembol|Periyot|Fiyat|Hacim%|Fiyat%|Skor/15|Tekrar
        /// </summary>
        public List<SignalData> ParseDipZirveFormat(string content, DateTime fileTime, string type)
        {
            var results = new List<SignalData>();
            var lines = content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 6)
                {
                    try
                    {
                        var scoreParts = parts[5].Split('/');
                        var data = new SignalData
                        {
                            Symbol = parts[0].Trim(),
                            Strategy = type, // DIP or ZIRVE
                            Period = parts[1].Trim(),
                            Price = decimal.Parse(parts[2].Trim()),
                            Score = int.Parse(scoreParts[0].Trim()),
                            MaxScore = scoreParts.Length > 1 ? int.Parse(scoreParts[1].Trim()) : 15,
                            Source = "DIP",
                            DetectedAt = fileTime,
                            IsRepeat = parts.Length > 6 && parts[6].Trim() == "T"
                        };

                        // V3: OHLC + XU100 Parsing (Optional)
                        if (parts.Length >= 13) 
                        {
                            try {
                                data.Open = decimal.Parse(parts[7].Trim());
                                data.High = decimal.Parse(parts[8].Trim());
                                data.Low = decimal.Parse(parts[9].Trim());
                                data.Close = decimal.Parse(parts[10].Trim());
                                data.Volume = decimal.Parse(parts[11].Trim());
                                data.IndexClose = decimal.Parse(parts[12].Trim());
                            } catch { }
                        }

                        results.Add(data);
                    }
                    catch { }
                }
            }
            return results;
        }

        /// <summary>
        /// ANKA formatı: Sembol|Fiyat|Deg%|Skor/30|Detay|Tekrar
        /// </summary>
        public List<SignalData> ParseAnkaFormat(string content, DateTime fileTime)
        {
            var results = new List<SignalData>();
            var lines = content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    try
                    {
                        var scoreParts = parts[3].Split('/');
                        var data = new SignalData
                        {
                            Symbol = parts[0].Trim(),
                            Strategy = "ANKA",
                            Period = "60", // ANKA varsayılan 60dk
                            Price = decimal.Parse(parts[1].Trim()),
                            Score = int.Parse(scoreParts[0].Trim()),
                            MaxScore = scoreParts.Length > 1 ? int.Parse(scoreParts[1].Trim()) : 30,
                            Source = "ANKA",
                            DetectedAt = fileTime,
                            IsRepeat = parts.Length > 5 && parts[5].Trim() == "T"
                        };

                        // V3: OHLC + XU100 Parsing (Optional)
                        if (parts.Length >= 12) 
                        {
                            try {
                                data.Open = decimal.Parse(parts[6].Trim());
                                data.High = decimal.Parse(parts[7].Trim());
                                data.Low = decimal.Parse(parts[8].Trim());
                                data.Close = decimal.Parse(parts[9].Trim());
                                data.Volume = decimal.Parse(parts[10].Trim());
                                data.IndexClose = decimal.Parse(parts[11].Trim());
                            } catch { }
                        }

                        results.Add(data);
                    }
                    catch { }
                }
            }
            return results;
        }

        /// <summary>
        /// Ortak sinyalleri bul (birden fazla taramada çıkan semboller)
        /// </summary>
        public List<SignalData> FindCommonSignals(List<SignalData> allSignals, int minCount = 2)
        {
            return allSignals
                .GroupBy(s => s.Symbol)
                .Where(g => g.Select(x => x.Source).Distinct().Count() >= minCount)
                .SelectMany(g => g)
                .OrderByDescending(s => s.Score)
                .ToList();
        }
    }
}

