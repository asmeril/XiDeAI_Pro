using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XiDeAI_Pro.Services
{
    public class SpamProtection
    {
        private readonly string _logPath;
        private List<TweetRecord> _records;
        
        // Limitler
        public int MaxTweetsPerHour { get; set; } = 5;
        public int MaxTweetsPerDay { get; set; } = 30;
        public int SameSymbolCooldownHours { get; set; } = 4;
        public TimeSpan QuietStart { get; set; } = new TimeSpan(22, 0, 0); // 22:00
        public TimeSpan QuietEnd { get; set; } = new TimeSpan(7, 30, 0);   // 07:30
        
        // 2.0 Controls
        public bool GlobalLock { get; set; } = false; // Emergency stop
        public bool ForceAllow { get; set; } = false; // Emergency override
        
        
        public event Action<string, string>? OnTweetRecorded;

        public SpamProtection(string logPath)
        {
            _logPath = logPath;
            _records = new List<TweetRecord>();
            Load();
        }

        public bool CanTweet(string symbol, string strategy, out string reason)
        {
            reason = "";
            if (ForceAllow) return true;
            if (GlobalLock) { reason = "Global lock active"; return false; }
            
            var now = DateTime.Now;

            // 1. Gece Sessizlik Modu
            if (IsQuietHours(now))
            {
                reason = $"Quiet hours ({QuietStart:hh\\:mm}-{QuietEnd:hh\\:mm})";
                return false;
            }

            // 2. Saatlik Limit
            int lastHourCount = _records.Count(r => r.Timestamp > now.AddHours(-1));
            if (lastHourCount >= MaxTweetsPerHour)
            {
                reason = $"Hourly limit reached ({MaxTweetsPerHour}/hour)";
                return false;
            }

            // 3. Günlük Limit
            int todayCount = _records.Count(r => r.Timestamp.Date == now.Date);
            if (todayCount >= MaxTweetsPerDay)
            {
                reason = $"Daily limit reached ({MaxTweetsPerDay}/day)";
                return false;
            }

            // 4. Aynı Hisse Cooldown
            var lastSameSymbol = _records
                .Where(r => r.Symbol == symbol)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefault();

            if (lastSameSymbol != null && lastSameSymbol.Timestamp > now.AddHours(-SameSymbolCooldownHours))
            {
                var remaining = lastSameSymbol.Timestamp.AddHours(SameSymbolCooldownHours) - now;
                reason = $"Same symbol cooldown ({remaining.TotalMinutes:0} min remaining)";
                return false;
            }

            return true;
        }

        // General-purpose gate for non-symbol posts (news, reports, batches, manual)
        public bool IsAlreadyPostedToday(string symbol, string strategy)
        {
            return _records.Any(r => r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) 
                                 && r.Strategy.Equals(strategy, StringComparison.OrdinalIgnoreCase) 
                                 && r.Timestamp.Date == DateTime.Now.Date);
        }

        // General-purpose gate for non-symbol posts (news, reports, batches, manual)
        public bool CanPostGeneral(out string reason)
        {
            reason = "";
            var now = DateTime.Now;

            if (IsQuietHours(now))
            {
                reason = $"Quiet hours ({QuietStart:hh\\:mm}-{QuietEnd:hh\\:mm})";
                return false;
            }

            int lastHourCount = _records.Count(r => r.Timestamp > now.AddHours(-1));
            if (lastHourCount >= MaxTweetsPerHour)
            {
                reason = $"Hourly limit reached ({MaxTweetsPerHour}/hour)";
                return false;
            }

            int todayCount = _records.Count(r => r.Timestamp.Date == now.Date);
            if (todayCount >= MaxTweetsPerDay)
            {
                reason = $"Daily limit reached ({MaxTweetsPerDay}/day)";
                return false;
            }

            return true;
        }

        public void RecordTweet(string symbol, string strategy, string tweetId = "")
        {
            _records.Add(new TweetRecord
            {
                Symbol = symbol,
                Strategy = strategy,
                Timestamp = DateTime.Now,
                TweetId = tweetId
            });
            Save();
            OnTweetRecorded?.Invoke(symbol, strategy);
        }

        public (int hourly, int daily, int monthly) GetStats()
        {
            var now = DateTime.Now;
            int hourly = _records.Count(r => r.Timestamp > now.AddHours(-1));
            int daily = _records.Count(r => r.Timestamp.Date == now.Date);
            int monthly = _records.Count(r => r.Timestamp.Month == now.Month && r.Timestamp.Year == now.Year);
            return (hourly, daily, monthly);
        }

        private bool IsQuietHours(DateTime dt)
        {
            var time = dt.TimeOfDay;
            // 22:00 - 09:00 arası
            return time >= QuietStart || time < QuietEnd;
        }

        private void Load()
        {
            _records = new List<TweetRecord>();
            if (File.Exists(_logPath))
            {
                try
                {
                    string json = File.ReadAllText(_logPath);
                    _records = JsonSerializer.Deserialize<List<TweetRecord>>(json) ?? new List<TweetRecord>();
                    
                    // Eski kayıtları temizle (30 günden eski)
                    _records = _records.Where(r => r.Timestamp > DateTime.Now.AddDays(-30)).ToList();
                }
                catch { }
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_logPath, json);
            }
            catch (Exception ex)
            {
                // Log hatayı görmek için
                System.Diagnostics.Debug.WriteLine($"SpamProtection.Save() Error: {ex.Message}");
                Logger.Sys($"⚠️ SpamProtection kayıt hatası: {ex.Message}");
            }
        }
    }

    public class TweetRecord
    {
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string TweetId { get; set; } = "";
    }
}

