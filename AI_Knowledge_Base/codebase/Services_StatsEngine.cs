using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public class ModuleStats
    {
        public string ModuleName { get; set; } = "";
        public int TotalOperations { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.MinValue;
        public double SuccessRate => TotalOperations > 0 ? (SuccessCount * 100.0 / TotalOperations) : 0;
        public string Status => GetStatus();

        private string GetStatus()
        {
            if (LastActivity == DateTime.MinValue) return "⚪ Idle";
            var elapsed = DateTime.Now - LastActivity;
            if (elapsed.TotalMinutes < 5) return "🟢 Active";
            if (elapsed.TotalHours < 1) return "🟡 Recent";
            return "⚪ Idle";
        }
    }

    public class EngagementRecord
    {
        public string Module { get; set; } = "";
        public string Link { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Replies { get; set; }
        public int Retweets { get; set; }
        public int Likes { get; set; }
    }

    public class StatsEngine
    {
        private readonly string _logPath;
        private readonly Dictionary<string, ModuleStats> _moduleStats = new Dictionary<string, ModuleStats>();
        private readonly Dictionary<string, int> _moduleTweetCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<EngagementRecord> _tweetLog = new List<EngagementRecord>();
        private const int MaxTweetLog = 200;
        private readonly object _lock = new object();

        public StatsEngine(string logDirectory)
        {
            _logPath = Path.Combine(logDirectory, "activity_log.txt");
            InitializeModules();
        }

        private void InitializeModules()
        {
            var modules = new[] { "SignalEngine", "NewsEngine", "InteractionEngine", 
                                  "GeminiService", "TwitterService", "MemoryEngine", 
                                  "SchedulerService", "TelegramService", "ThreadService", "ManualAnalysis" };
            
            lock (_lock)
            {
                foreach (var module in modules)
                {
                    _moduleStats[module] = new ModuleStats { ModuleName = module };
                    _moduleTweetCounts[module] = 0;
                }
            }
        }

        public void RecordActivity(string moduleName, string activity, bool success, string details = "")
        {
            lock (_lock)
            {
                if (!_moduleStats.ContainsKey(moduleName))
                {
                    _moduleStats[moduleName] = new ModuleStats { ModuleName = moduleName };
                    _moduleTweetCounts[moduleName] = 0;
                }

                var stats = _moduleStats[moduleName];
                stats.TotalOperations++;
                if (success) stats.SuccessCount++;
                else stats.ErrorCount++;
                stats.LastActivity = DateTime.Now;

                // Log to file
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{moduleName}] {activity} | Success: {success} | {details}";
                    File.AppendAllText(_logPath, logEntry + Environment.NewLine);
                }
                catch { /* Ignore logging errors */ }
            }
        }

        public void RecordTweet(string moduleName, int tweetCount, string link = "", string text = "")
        {
            lock (_lock)
            {
                if (!_moduleTweetCounts.ContainsKey(moduleName))
                {
                    _moduleTweetCounts[moduleName] = 0;
                }
                _moduleTweetCounts[moduleName] += tweetCount;

                if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(link))
                {
                    _tweetLog.Add(new EngagementRecord
                    {
                        Module = moduleName,
                        Link = link,
                        Text = text,
                        CreatedAt = DateTime.Now
                    });
                    if (_tweetLog.Count > MaxTweetLog)
                    {
                        _tweetLog.RemoveRange(0, _tweetLog.Count - MaxTweetLog);
                    }
                }
            }
        }

        public void UpdateEngagement(IEnumerable<SocialIntelEngagementItem> items)
        {
            lock (_lock)
            {
                foreach (var item in items)
                {
                    var match = _tweetLog.LastOrDefault(t =>
                        (!string.IsNullOrWhiteSpace(item.text) &&
                         !string.IsNullOrWhiteSpace(t.Text) &&
                         (t.Text.StartsWith(item.text, StringComparison.OrdinalIgnoreCase) ||
                          item.text.StartsWith(t.Text, StringComparison.OrdinalIgnoreCase) ||
                          item.text.Contains(t.Text, StringComparison.OrdinalIgnoreCase) ||
                          t.Text.Contains(item.text, StringComparison.OrdinalIgnoreCase))));

                    if (match != null)
                    {
                        match.Replies = item.replies;
                        match.Retweets = item.retweets;
                        match.Likes = item.likes;
                    }
                }
            }
        }

        public (Dictionary<string, int> TweetCounts, List<EngagementRecord> RecentTweets) GetTweetSnapshot(int limit = 50)
        {
            lock (_lock)
            {
                var counts = new Dictionary<string, int>(_moduleTweetCounts, StringComparer.OrdinalIgnoreCase);
                var recent = _tweetLog.OrderByDescending(t => t.CreatedAt).Take(limit).ToList();
                return (counts, recent);
            }
        }

        public Dictionary<string, ModuleStats> GetAllModuleStats()
        {
            lock (_lock)
            {
                return new Dictionary<string, ModuleStats>(_moduleStats);
            }
        }

        public ModuleStats? GetModuleStats(string moduleName)
        {
            lock (_lock)
            {
                return _moduleStats.ContainsKey(moduleName) ? _moduleStats[moduleName] : null;
            }
        }

        public string GetFormattedSummary()
        {
            lock (_lock)
            {
                var summary = "📊 MODULE STATISTICS SUMMARY\n";
                summary += "═══════════════════════════════════════\n\n";

                foreach (var kvp in _moduleStats.OrderByDescending(x => x.Value.TotalOperations))
                {
                    var stats = kvp.Value;
                    summary += $"{stats.Status} {stats.ModuleName}\n";
                    summary += $"   Operations: {stats.TotalOperations} | Success: {stats.SuccessCount} | Errors: {stats.ErrorCount}\n";
                    summary += $"   Success Rate: {stats.SuccessRate:F1}% | Last: {(stats.LastActivity == DateTime.MinValue ? "Never" : stats.LastActivity.ToString("HH:mm:ss"))}\n\n";
                }

                summary += "🧵 TWEET COUNTS BY MODULE\n";
                summary += "──────────────────────────\n";
                foreach (var kvp in _moduleTweetCounts.OrderByDescending(x => x.Value))
                {
                    summary += $"- {kvp.Key}: {kvp.Value} tweet\n";
                }

                var topEng = _tweetLog.OrderByDescending(t => t.Likes + t.Retweets + t.Replies).Take(5).ToList();
                if (topEng.Count > 0)
                {
                    summary += "\n🔥 TOP ENGAGEMENT (Last posts)\n";
                    summary += "──────────────────────────\n";
                    foreach (var t in topEng)
                    {
                        var snippet = string.IsNullOrEmpty(t.Text) ? "(no text)" : (t.Text.Length > 80 ? t.Text.Substring(0, 77) + "..." : t.Text);
                        summary += $"- {t.Module} | ❤️{t.Likes} 🔁{t.Retweets} 💬{t.Replies} | {snippet}\n";
                    }
                }

                return summary;
            }
        }

        public void ResetStats(string? moduleName = null)
        {
            lock (_lock)
            {
                if (moduleName == null)
                {
                    InitializeModules();
                }
                else if (_moduleStats.ContainsKey(moduleName))
                {
                    _moduleStats[moduleName] = new ModuleStats { ModuleName = moduleName };
                }
            }
        }
    }
}

