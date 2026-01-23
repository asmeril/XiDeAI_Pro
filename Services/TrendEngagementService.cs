// TREND_ENGAGEMENT_VERSION: 1.0
// PURPOSE: Dynamic trend tracking and posting at X peak hours. Fenerbahçe identity.
// RUNS: 3x daily (08:30, 13:00, 19:00 Turkey time) alongside Round-Robin system.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class TrendEngagementService
    {
        private readonly SocialIntelService _socialIntel;
        private readonly GeminiService _gemini;
        private readonly TelegramService _telegram;
        private readonly StatsEngine _stats;
        private readonly PromptManager _prompts = new PromptManager();
        
        private DateTime _lastRunTime = DateTime.MinValue;
        private int _todayPostCount = 0;
        private DateTime _lastResetDate = DateTime.MinValue;

        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;

        // Prime time windows (Turkey time)
        private readonly TimeSpan[] _primeHours = new[]
        {
            new TimeSpan(8, 30, 0),   // Sabah 08:30
            new TimeSpan(13, 0, 0),   // Öğle 13:00
            new TimeSpan(19, 0, 0)    // Akşam 19:00
        };

        private const int MAX_POSTS_PER_SESSION = 3;
        private const int MAX_POSTS_PER_DAY = 9;
        private const int COOLDOWN_MINUTES = 30; // Minimum time between runs

        public TrendEngagementService(
            SocialIntelService socialIntel,
            GeminiService gemini,
            TelegramService telegram,
            StatsEngine stats)
        {
            _socialIntel = socialIntel;
            _gemini = gemini;
            _telegram = telegram;
            _stats = stats;
        }

        /// <summary>
        /// Check if we should run and execute trend engagement
        /// </summary>
        public async Task<bool> TryEngageAsync()
        {
            var cfg = Config.ConfigManager.Current;
            if (!cfg.TrendEngagementEnabled)
            {
                OnLog?.Invoke("⏸️ Trend Engagement devre dışı", "Trend");
                return false;
            }

            // Reset daily counter
            if (_lastResetDate.Date != DateTime.Today)
            {
                _todayPostCount = 0;
                _lastResetDate = DateTime.Today;
            }

            // Check daily limit
            if (_todayPostCount >= MAX_POSTS_PER_DAY)
            {
                OnLog?.Invoke($"📊 Günlük trend limiti doldu ({_todayPostCount}/{MAX_POSTS_PER_DAY})", "Trend");
                return false;
            }

            // Check cooldown
            if ((DateTime.Now - _lastRunTime).TotalMinutes < COOLDOWN_MINUTES)
            {
                return false;
            }

            // Check if near prime time
            if (!IsNearPrimeTime())
            {
                return false;
            }

            return await ExecuteTrendEngagementAsync();
        }

        /// <summary>
        /// Core engagement logic: Fetch trends, filter, generate, post
        /// </summary>
        private async Task<bool> ExecuteTrendEngagementAsync()
        {
            try
            {
                OnStatusUpdate?.Invoke("🔥 Trend taraması başlıyor...");
                OnLog?.Invoke("🔥 Dinamik Trend Taraması başlatıldı", "Trend");

                var trendsArray = await _socialIntel.GetTrendsAsync();
                if (trendsArray == null || trendsArray.Length == 0)
                {
                    OnLog?.Invoke("⚠️ Trend bulunamadı", "Trend");
                    return false;
                }

                OnLog?.Invoke($"📊 {trendsArray.Length} trend alındı", "Trend");
                var trends = trendsArray.ToList();

                // 2. AI Filter: Select suitable trends
                OnStatusUpdate?.Invoke("🤖 AI trend filtresi çalışıyor...");
                var selectedTopics = await FilterTrendsWithAI(trends);
                
                if (selectedTopics.Count == 0)
                {
                    OnLog?.Invoke("⚠️ Uygun trend bulunamadı (AI filtre)", "Trend");
                    return false;
                }

                OnLog?.Invoke($"✅ {selectedTopics.Count} trend seçildi: {string.Join(", ", selectedTopics.Select(t => t.Topic))}", "Trend");

                // 3. Generate and post tweets
                int posted = 0;
                foreach (var topic in selectedTopics.Take(MAX_POSTS_PER_SESSION))
                {
                    if (_todayPostCount >= MAX_POSTS_PER_DAY) break;

                    OnStatusUpdate?.Invoke($"✍️ Tweet oluşturuluyor: {topic.Topic}...");
                    
                    var tweet = await GenerateTrendTweet(topic.Topic, topic.Category);
                    if (string.IsNullOrEmpty(tweet))
                    {
                        OnLog?.Invoke($"⚠️ Tweet üretilemedi: {topic.Topic}", "Trend");
                        continue;
                    }

                    // Post the tweet
                    OnStatusUpdate?.Invoke($"📤 Paylaşılıyor: {topic.Topic}...");
                    var result = await _socialIntel.PostTweet(tweet, null);
                    
                    if (result.status == "success")
                    {
                        posted++;
                        _todayPostCount++;
                        OnLog?.Invoke($"✅ Trend Tweet Paylaşıldı [{topic.Category}]: {topic.Topic}", "Trend");
                        _stats.RecordActivity("TrendPost", topic.Topic, true, tweet.Substring(0, Math.Min(50, tweet.Length)));
                        
                        // Notify Telegram
                        await _telegram.SendMessageAsync($"🔥 TREND TWEET [{topic.Category}]\n\n{tweet}\n\n📊 Konu: {topic.Topic}");
                        
                        // Delay between posts
                        await Task.Delay(5000);
                    }
                    else
                    {
                        OnLog?.Invoke($"❌ Tweet paylaşılamadı: {result.text}", "Trend");
                    }
                }

                _lastRunTime = DateTime.Now;
                OnLog?.Invoke($"🏁 Trend Engagement tamamlandı: {posted} tweet paylaşıldı", "Trend");
                return posted > 0;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Trend Engagement hatası: {ex.Message}", "System");
                return false;
            }
        }

        /// <summary>
        /// Filter trends using Gemini AI
        /// </summary>
        private async Task<List<(string Topic, string Category)>> FilterTrendsWithAI(List<string> trends)
        {
            var result = new List<(string Topic, string Category)>();
            
            try
            {
                string prompt = _prompts.GetTrendFilterPrompt(trends);
                var response = await _gemini.GenerateGenericContent(prompt);
                
                if (string.IsNullOrEmpty(response))
                    return result;

                // Parse JSON response
                // Expected: [{"topic": "#Borsa", "category": "FINANS"}, ...]
                var jsonStart = response.IndexOf('[');
                var jsonEnd = response.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var items = JsonSerializer.Deserialize<List<TrendSelection>>(json);
                    
                    if (items != null)
                    {
                        result = items.Where(i => !string.IsNullOrEmpty(i.topic))
                                      .Select(i => (i.topic!, i.category ?? "GENEL"))
                                      .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ AI filtre hatası: {ex.Message}", "Trend");
            }

            return result;
        }

        /// <summary>
        /// Generate a tweet for the given trend topic
        /// </summary>
        private async Task<string?> GenerateTrendTweet(string topic, string category)
        {
            try
            {
                string prompt = _prompts.GetTrendTweetPrompt(topic, category);
                var response = await _gemini.GenerateGenericContent(prompt);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                // Clean and validate
                string tweet = response.Trim();
                
                // Remove any markdown or quotes
                tweet = tweet.Trim('"', '\'', '`');
                
                // Enforce character limit
                if (tweet.Length > 280)
                    tweet = tweet.Substring(0, 277) + "...";

                return tweet;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Tweet üretim hatası: {ex.Message}", "Trend");
                return null;
            }
        }

        /// <summary>
        /// Check if current time is within 15 minutes of a prime hour
        /// </summary>
        private bool IsNearPrimeTime()
        {
            var now = DateTime.Now.TimeOfDay;
            
            foreach (var primeHour in _primeHours)
            {
                var diff = (now - primeHour).TotalMinutes;
                // Within 15 minutes window (±15)
                if (diff >= -5 && diff <= 15)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Force run for testing (bypasses time check)
        /// </summary>
        public async Task<bool> ForceRunAsync()
        {
            OnLog?.Invoke("🔧 Manuel Trend Engagement tetiklendi", "Trend");
            return await ExecuteTrendEngagementAsync();
        }

        private class TrendSelection
        {
            public string? topic { get; set; }
            public string? category { get; set; }
        }
    }
}
