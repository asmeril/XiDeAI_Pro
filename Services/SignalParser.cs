using System;
using System.Collections.Generic;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    // v4.3.0: Content Tier for Hybrid Signal Intelligence
    public enum ContentTier
    {
        Premium,    // 85-100: 4-5 tweet, detaylı analiz
        Standard,   // 70-84: 3 tweet, standart thread
        Summary,    // 55-69: 1-2 tweet, özet
        Notification // < 55: Sadece log/telegram bildirim
    }

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
        
        // v4.3.0: Common Scan Flag
        public bool IsCommonScan { get; set; } = false;
        
        // Rich Report Data (V3)
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal IndexClose { get; set; } // XU100 Close
        public string? Analysis { get; set; } // Manual/AI Analysis Content

        // v4.3.0: Hybrid Signal Intelligence Properties
        
        /// <summary>
        /// Normalize edilmiş skor (0-100 arası)
        /// </summary>
        public int NormalizedScore => MaxScore > 0 ? (Score * 100 / MaxScore) : 0;
        
        /// <summary>
        /// Strateji bazlı bonus (0-100)
        /// </summary>
        public int StrategyBonus
        {
            get
            {
                string strat = Strategy.ToUpperInvariant();
                if (strat.Contains("KING") || strat == "K") return 100;
                if (strat.Contains("BOMBA") || strat == "B") return 90;
                if (strat.Contains("TEFO") || strat == "T") return 85;
                if (strat.Contains("ANKA")) return 80;
                if (strat.Contains("DIP")) return 75;
                if (strat.Contains("ZIRVE")) return 70;
                if (strat.Contains("ALPHA")) return 72;
                if (strat.Contains("PREMOVE")) return 68;
                if (strat.Contains("ZIRVE")) return 70;
                return 50; // Varsayılan
            }
        }
        
        /// <summary>
        /// Periyot bazlı bonus (0-100)
        /// </summary>
        public int PeriodBonus
        {
            get
            {
                string p = Period.ToUpperInvariant().Trim();
                if (p == "G" || p == "D" || p == "GÜNLÜK" || p == "1440") return 100;
                if (p == "240" || p == "4H") return 80;
                if (p == "60" || p == "1H") return 60;
                if (p == "15" || p == "15M") return 40;
                return 30; // Daha kısa periyotlar
            }
        }
        
        /// <summary>
        /// Kompozit Final Skor (0-100)
        /// Formula: (NormalizedScore × 0.5) + (PeriodBonus × 0.2) + (StrategyBonus × 0.2) + (CommonScanBonus × 0.1)
        /// </summary>
        public int FinalScore
        {
            get
            {
                int commonBonus = IsCommonScan ? 100 : 0;
                double final = (NormalizedScore * 0.5) + (PeriodBonus * 0.2) + (StrategyBonus * 0.2) + (commonBonus * 0.1);
                return (int)Math.Round(final);
            }
        }
        
        /// <summary>
        /// FinalScore'a göre içerik seviyesi
        /// </summary>
        public ContentTier Tier
        {
            get
            {
                if (FinalScore >= 85) return ContentTier.Premium;
                if (FinalScore >= 70) return ContentTier.Standard;
                if (FinalScore >= 55) return ContentTier.Summary;
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
        public List<SignalData> Parse(string content, string source)
        {
            if (source == "KING") return ParseKingFormat(content, DateTime.Now);
            if (source == "DIP") return ParseDipZirveFormat(content, DateTime.Now, "DIP");
            if (source == "ANKA") return ParseAnkaFormat(content, DateTime.Now);
            if (source == "ALPHA") return ParseAlphaDbLine(content, "ALPHA");
            if (source == "PREMOVE") return ParseAlphaDbLine(content, "PREMOVE");
            return new List<SignalData>();
        }

        /// <summary>
        /// Alpha/PreMove DB satır formatı: SEMBOL|ALPHA|60|datetime_iso|fiyat|durum
        /// Örnek: THYAO|ALPHA|60|2025-01-15T10:30:00.0000000|95.60|AKTIF
        /// </summary>
        public List<SignalData> ParseAlphaDbLine(string line, string strategyOverride = "")
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
                string period = parts[2].Trim(); // "60" veya "G"

                // Sadece AKTIF sinyalleri al; PULLBACK_ADAY vs. de geçsin
                string durum = parts.Length >= 6 ? parts[5].Trim().ToUpperInvariant() : "AKTIF";
                // Tüm durumlar işlenir ama AKTIF olanlara daha yüksek skor verilir
                int baseScore = durum == "AKTIF" ? 20 : 12;

                decimal price = 0;
                decimal.TryParse(parts[4].Trim().Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out price);

                DateTime detectedAt = DateTime.Now;
                if (parts.Length >= 4)
                    DateTime.TryParse(parts[3].Trim(), out detectedAt);

                var data = new SignalData
                {
                    Symbol = symbol,
                    Strategy = strategy,
                    Period = period,
                    Price = price,
                    Score = baseScore,
                    MaxScore = 20,
                    Source = strategy,
                    DetectedAt = detectedAt,
                    IsRepeat = false,
                    Basis = "TL"
                };

                results.Add(data);
            }
            catch { }

            return results;
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

