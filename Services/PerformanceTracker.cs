using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XiDeAI_Pro.Services
{
    public class PerformanceTracker
    {
        private readonly string _dataPath;
        private List<SignalRecord> _signals;

        public PerformanceTracker(string dataPath)
        {
            _dataPath = dataPath;
            _signals = new List<SignalRecord>();
            Load();
        }

        /// <summary>
        /// Yeni sinyal kaydı ekle
        /// </summary>
        /// <summary>
        /// Yeni sinyal kaydı ekle
        /// </summary>
        public void RecordSignal(SignalData sig)
        {
            _signals.Add(new SignalRecord
            {
                Symbol = sig.Symbol,
                Strategy = sig.Strategy,
                Period = sig.Period,
                EntryPrice = sig.Price,
                Score = sig.Score,
                EntryTime = DateTime.Now,
                
                // Rich Data
                Open = sig.Open,
                High = sig.High,
                Low = sig.Low,
                Close = sig.Close,
                Volume = sig.Volume,
                IndexCloseAtSignal = sig.IndexClose,
                Source = sig.Source // Added Source
            });
            Save();
        }

        /// <summary>
        /// Get recent signals for UI population
        /// </summary>
        public List<SignalRecord> GetRecentSignals(int count = 100)
        {
            return _signals.OrderByDescending(s => s.EntryTime).Take(count).Reverse().ToList();
        }

        /// <summary>
        /// Kapanış fiyatını güncelle (18:30'da çağrılır)
        /// </summary>
        public void UpdateClosingPrices(Dictionary<string, decimal> closingPrices)
        {
            int updatedCount = 0;
            foreach (var sig in _signals.Where(s => s.EntryTime.Date == DateTime.Now.Date && s.ClosingPrice == 0))
            {
                // Try direct match OR symbol without .IS/.HE (BIST normalization)
                string? symbolKey = sig.Symbol;
                if (!closingPrices.ContainsKey(symbolKey))
                {
                    // Fallback: Check if we have symbols like "THYAO.IS" in dictionary but "THYAO" in signal
                    symbolKey = closingPrices.Keys.FirstOrDefault(k => k.StartsWith(sig.Symbol + "."));
                }

                if (symbolKey != null && closingPrices.TryGetValue(symbolKey, out decimal closePrice))
                {
                    sig.ClosingPrice = closePrice;
                    sig.DailyPnL = ((closePrice - sig.EntryPrice) / sig.EntryPrice) * 100;
                    updatedCount++;
                }
                else
                {
                    Logger.Sys($"⚠️ [Performance] {sig.Symbol} için kapanış fiyatı bulunamadı.");
                }
            }
            if (updatedCount > 0)
            {
                Logger.Sys($"✅ [Performance] {updatedCount} sinyal için kapanış fiyatı güncellendi.");
                Save();
            }
        }

        /// <summary>
        /// Günlük performans raporu oluştur
        /// </summary>
        public DailyReport GetDailyReport(DateTime date, decimal marketReturn = 0)
        {
            var todaySignals = _signals.Where(s => s.EntryTime.Date == date.Date).ToList();
            
            foreach (var sig in todaySignals)
            {
                if (sig.DailyPnL == 0 && sig.ClosingPrice == 0)
                {
                    if (sig.Close > 0 && sig.EntryPrice > 0)
                    {
                        sig.DailyPnL = ((sig.Close - sig.EntryPrice) / sig.EntryPrice) * 100;
                    }
                }
            }

            // CALCULATE STATS ON FULL DATASET (Honesty)
            var fullSet = todaySignals.ToList();

            // CURATED LIST FOR DISPLAY: Top 3 Best ONLY
            // User Request: "Sadece en başarılı 3 analiz sonucunu paylaş"
            var displayList = fullSet.OrderByDescending(s => s.DailyPnL).Take(3).ToList();

            var report = new DailyReport
            {
                Date = date,
                TotalSignals = fullSet.Count,
                TotalReturn = fullSet.Sum(s => s.DailyPnL),
                Winners = fullSet.Count(s => s.DailyPnL > 0),
                Losers = fullSet.Count(s => s.DailyPnL < 0),
                BestPerformer = fullSet.OrderByDescending(s => s.DailyPnL).FirstOrDefault(),
                WorstPerformer = fullSet.OrderBy(s => s.DailyPnL).FirstOrDefault(),
                AvgReturn = fullSet.Any() ? fullSet.Average(s => s.DailyPnL) : 0,
                MarketReturn = marketReturn,
                Volatility = fullSet.Any() ? (decimal)Math.Sqrt((double)fullSet.Average(s => Math.Pow((double)(s.DailyPnL - (fullSet.Average(x=>x.DailyPnL))), 2))) : 0,
                Signals = displayList
            };

            // Calculate Profit Factor (Honest Stats)
            decimal grossWin = fullSet.Where(s => s.DailyPnL > 0).Sum(s => s.DailyPnL);
            decimal grossLoss = Math.Abs(fullSet.Where(s => s.DailyPnL < 0).Sum(s => s.DailyPnL));
            report.ProfitFactor = grossLoss > 0 ? grossWin / grossLoss : (grossWin > 0 ? 99 : 0);

            // Calculate Avg Hold Time (Closed signals only)
            var closedSignals = fullSet.Where(s => s.ExitTime != default).ToList();
            if (closedSignals.Any())
            {
                report.AvgHoldTimeMinutes = closedSignals.Average(s => (s.ExitTime - s.EntryTime).TotalMinutes);
            }

            // Calculate Market Sync (Signals matching market direction)
            if (fullSet.Any())
            {
                int syncCount = fullSet.Count(s => (marketReturn >= 0 && s.DailyPnL >= 0) || (marketReturn < 0 && s.DailyPnL < 0));
                report.MarketSyncScore = (decimal)syncCount / fullSet.Count * 100;
            }

            report.AvgAlpha = report.AvgReturn - report.MarketReturn;

            // Strategy Karnesi
            var groups = fullSet.GroupBy(s => s.Strategy);
            foreach (var g in groups)
            {
                var total = g.Count();
                var wins = g.Count(s => s.DailyPnL > 0);
                var gGrossWin = g.Where(s => s.DailyPnL > 0).Sum(s => s.DailyPnL);
                var gGrossLoss = Math.Abs(g.Where(s => s.DailyPnL < 0).Sum(s => s.DailyPnL));
                
                var stat = new StrategyStat
                {
                    StrategyName = g.Key,
                    TotalSignals = total,
                    WinRate = (decimal)wins / total * 100,
                    AvgReturn = g.Average(s => s.DailyPnL),
                    Alpha = g.Average(s => s.DailyPnL) - marketReturn,
                    ProfitFactor = gGrossLoss > 0 ? gGrossWin / gGrossLoss : (gGrossWin > 0 ? 99 : 0)
                };
                report.StrategyStats[g.Key] = stat;
            }

            return report;
        }

        /// <summary>
        /// Haftalık performans raporu
        /// </summary>
        public WeeklyReport GetWeeklyReport()
        {
            var weekStart = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);
            var weekSignals = _signals.Where(s => s.EntryTime >= weekStart).ToList();

            return new WeeklyReport
            {
                WeekStart = weekStart,
                TotalSignals = weekSignals.Count,
                HitRate = weekSignals.Any() ? (decimal)weekSignals.Count(s => s.DailyPnL > 0) / weekSignals.Count * 100 : 0,
                TotalReturn = weekSignals.Sum(s => s.DailyPnL),
                AvgReturn = weekSignals.Any() ? weekSignals.Average(s => s.DailyPnL) : 0,
                
                // Rich Stats (Weekly)
                AvgAlpha = weekSignals.Any() ? weekSignals.Average(s => s.DailyPnL) : 0, // Market verisi olmadigi icin Alpha=PnL
                Volatility = weekSignals.Any() ? (decimal)Math.Sqrt((double)weekSignals.Average(s => Math.Pow((double)(s.DailyPnL - (weekSignals.Average(x=>x.DailyPnL))), 2))) : 0,
                
                Top3 = weekSignals.OrderByDescending(s => s.DailyPnL).Take(3).ToList()
            };
        }

        /// <summary>
        /// Genel başarı oranı
        /// </summary>
        public OverallStats GetOverallStats()
        {
            var completed = _signals.Where(s => s.ClosingPrice > 0).ToList();
            return new OverallStats
            {
                TotalSignals = completed.Count,
                HitRate = completed.Any() ? Math.Round((decimal)completed.Count(s => s.DailyPnL > 0) / completed.Count * 100, 1) : 0,
                AvgWin = completed.Where(s => s.DailyPnL > 0).DefaultIfEmpty().Average(s => s?.DailyPnL ?? 0),
                AvgLoss = completed.Where(s => s.DailyPnL < 0).DefaultIfEmpty().Average(s => s?.DailyPnL ?? 0),
                MaxWin = completed.Any() ? completed.Max(s => s.DailyPnL) : 0,
                MaxLoss = completed.Any() ? completed.Min(s => s.DailyPnL) : 0
            };
        }

        private void Load()
        {
            _signals = new List<SignalRecord>();
            if (File.Exists(_dataPath))
            {
                try
                {
                    string json = File.ReadAllText(_dataPath);
                    _signals = JsonSerializer.Deserialize<List<SignalRecord>>(json) ?? new List<SignalRecord>();
                }
                catch { }
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_signals, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataPath, json);
            }
            catch { }
        }
    }

    public class SignalRecord
    {
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Period { get; set; } = "";
        public decimal EntryPrice { get; set; }
        public decimal ClosingPrice { get; set; }
        public int Score { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal DailyPnL { get; set; }
        
        // Rich Data Steps
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; } // Sinyal anindaki close (robot tarafindan gonderilen)
        public decimal Volume { get; set; }
        public decimal IndexCloseAtSignal { get; set; }
        public string Source { get; set; } = ""; // Added Source
    }

    public class DailyReport
    {
        public DateTime Date { get; set; }
        public int TotalSignals { get; set; }
        public int Winners { get; set; }
        public int Losers { get; set; }
        public decimal WinRate => TotalSignals > 0 ? (decimal)Winners / TotalSignals * 100 : 0;
        public decimal TotalReturn { get; set; } // Added for completeness
        public decimal AvgReturn { get; set; }
        public decimal MarketReturn { get; set; }
        public decimal AvgAlpha { get; set; }
        public decimal Volatility { get; set; }
        
        // New Metric: Profit Factor (Gross Win / Gross Loss)
        public decimal ProfitFactor { get; set; }
        // New Metric: Avg Hold Time (in minutes)
        public double AvgHoldTimeMinutes { get; set; }
        // New Metric: Market Correlation (How many signals match index direction)
        public decimal MarketSyncScore { get; set; }

        public SignalRecord? BestPerformer { get; set; }
        public SignalRecord? WorstPerformer { get; set; }
        public Dictionary<string, StrategyStat> StrategyStats { get; set; } = new Dictionary<string, StrategyStat>();
        public List<SignalRecord> Signals { get; set; } = new List<SignalRecord>();
    }

    public class StrategyStat
    {
        public string StrategyName { get; set; } = "";
        public int TotalSignals { get; set; }
        public decimal WinRate { get; set; }
        public decimal AvgReturn { get; set; }
        public decimal Alpha { get; set; }
        public decimal ProfitFactor { get; set; }
    }

    public class MarketSnapshot
    {
        public string Bist100 { get; set; } = "N/A";
        public string UsdTry { get; set; } = "N/A";
        public string EurTry { get; set; } = "N/A";
        public string Gold { get; set; } = "N/A";
        public string Silver { get; set; } = "N/A";
        public List<MarketMover> TopGainers { get; set; } = new();
        public List<MarketMover> TopLosers { get; set; } = new();
        public List<MarketMover> TopVolume { get; set; } = new();
    }

    public class MarketMover
    {
        public string Symbol { get; set; } = "";
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
    }

    public class WeeklyReport
    {
        public DateTime WeekStart { get; set; }
        public int TotalSignals { get; set; }
        public decimal HitRate { get; set; }
        public decimal TotalReturn { get; set; }

        public decimal AvgReturn { get; set; }
        public decimal AvgAlpha { get; set; }
        public decimal Volatility { get; set; }
        public List<SignalRecord> Top3 { get; set; } = new();
    }

    public class OverallStats
    {
        public int TotalSignals { get; set; }
        public decimal HitRate { get; set; }
        public decimal AvgWin { get; set; }
        public decimal AvgLoss { get; set; }
        public decimal MaxWin { get; set; }
        public decimal MaxLoss { get; set; }
    }
}

