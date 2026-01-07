// OPERATION_ENGINE_VERSION: 1.0
// PURPOSE: Handles scheduled operations (Morning Motivation, Market Close, Reports).
// Implements retry logic and decouples operational tasks from the UI.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public class OperationEngine
    {
        private readonly GeminiService _gemini;
        private readonly SocialIntelService _socialIntel;
        private readonly TwitterService _twitter;
        private readonly SpamProtection _spam;
        private readonly PromptManager _prompts;
        private readonly StatsEngine _stats;

        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;

        private int _motivationRetry = 0;
        private int _closeRetry = 0;

        public OperationEngine(
            GeminiService gemini,
            SocialIntelService socialIntel,
            TwitterService twitter,
            SpamProtection spam,
            PromptManager prompts,
            StatsEngine stats)
        {
            _gemini = gemini;
            _socialIntel = socialIntel;
            _twitter = twitter;
            _spam = spam;
            _prompts = prompts;
            _stats = stats;
        }

        public async Task RunMorningMotivation()
        {
            if (_spam.IsAlreadyPostedToday("MOTIVATION", "DAILY"))
            {
                OnLog?.Invoke("🛡️ Spam Protection (Motivation): Bugün zaten motivasyon tweeti atılmış. Atlanıyor.", "Operation");
                return;
            }

            if (!_spam.CanPostGeneral(out string reason))
            {
                OnLog?.Invoke($"🛡️ Spam Protection (Motivation): {reason}", "Operation");
                return;
            }

            try
            {
                OnStatusUpdate?.Invoke("☀️ Sabah motivasyonu hazırlanıyor...");
                string? tweet = await _gemini.GenerateMotivationTweet();

                if (string.IsNullOrEmpty(tweet))
                {
                    tweet = "☀️ Günaydın!\n\n\"Başarı, son değil; başarısızlık, ölümcül değil. Önemli olan devam etme cesareti.\"\n- Winston Churchill\n\n#Motivasyon #Borsa #XideAI";
                }

                bool sent = await ExecuteScheduledTweet(tweet, "Motivation");
                if (sent)
                {
                    OnLog?.Invoke("✅ Sabah Motivasyonu başarıyla paylaşıldı.", "Operation");
                    _stats.RecordActivity("Operation", "Morning Motivation Posted", true);
                    _motivationRetry = 0;
                    _spam.RecordTweet("MOTIVATION", "DAILY");
                }
                else throw new Exception("Motivation tweet failed to post.");
            }
            catch (Exception ex)
            {
                _motivationRetry++;
                LogRetry("Morning Motivation", _motivationRetry, ex.Message, RunMorningMotivation);
            }
        }

        public async Task RunMarketCloseSummary()
        {
            if (!_spam.CanPostGeneral(out string reason))
            {
                OnLog?.Invoke($"🛡️ Spam Protection (Kapanış): {reason}", "Operation");
                return;
            }

            try
            {
                OnStatusUpdate?.Invoke("🌆 Pazar kapanış özeti hazırlanıyor...");
                
                var financials = await _socialIntel.GetFinancialSummaryAsync();
                var gainers = await _socialIntel.GetTopGainersAsync();
                var losers = await _socialIntel.GetTopLosersAsync();
                var volume = await _socialIntel.GetTopVolumeAsync();

                string indices = FormatIndices(financials);
                string gTable = FormatStockTable(gainers, "Kazananlar");
                string lTable = FormatStockTable(losers, "Kaybedenler");
                string vTable = FormatStockTable(volume, "Hacim");

                string? tweetSet = await _gemini.GenerateMarketCloseTableTweet(indices, gTable, lTable, vTable);
                if (string.IsNullOrEmpty(tweetSet)) throw new Exception("Gemini report generation failed.");

                var tweets = tweetSet.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                bool anySent = false;
                foreach (var t in tweets.Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                {
                    // SPAM/KALİTE KONTROLÜ
                    if (ContentQualityGuard.IsSpamOrLowQuality(t, out string spamReason))
                    {
                        OnLog?.Invoke($"⚠️ İçerik kalitesi düşük, atlanıyor: {spamReason}", "Operation");
                        continue;
                    }

                    if (await ExecuteScheduledTweet(t, "MarketClose"))
                    {
                        anySent = true;
                        _stats.RecordActivity("Operation", "Market Close Report Tweet Posted", true);
                        _spam.RecordTweet("REPORT", "CLOSE");
                    }
                    await Task.Delay(3000);
                }

                if (anySent)
                {
                    _closeRetry = 0;
                    OnLog?.Invoke("✅ Piyasa Kapanış Raporu (zincir) başarıyla paylaşıldı.", "Operation");
                }
                else if (tweets.Length > 0) OnLog?.Invoke("⚠️ Hiçbir tweet gönderilemedi (spam veya hata).", "Operation");
                else throw new Exception("No valid content generated.");
            }
            catch (Exception ex)
            {
                _closeRetry++;
                LogRetry("Market Close", _closeRetry, ex.Message, RunMarketCloseSummary);
            }
        }

        private async Task<bool> ExecuteScheduledTweet(string content, string category)
        {
            // Web First
            var web = await _socialIntel.PostTweet(content);
            if (web.status == "success") return true;

            OnLog?.Invoke($"⚠️ Web {category} failed ({web.message}), switching to API...", "Operation");
            
            // API Fallback
            return await _twitter.SendTweetAsync(content);
        }

        private void LogRetry(string name, int count, string error, Func<Task> retryAction)
        {
            if (count <= 3)
            {
                OnLog?.Invoke($"❌ {name} Hatası ({count}/3): {error}. 10 dk sonra tekrar denenecek.", "Operation");
                _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(_ => retryAction());
            }
            else
            {
                OnLog?.Invoke($"❌ {name} KRİTİK HATA: Max deneme sayısına ulaşıldı.", "Operation");
            }
        }

        private string FormatIndices(Dictionary<string, string> data)
        {
            if (data == null || data.Count == 0) return "Veri alınamadı.";
            return string.Join(" | ", data.Select(x => $"{x.Key}: {x.Value}"));
        }

        private string FormatStockTable(List<StockData> stocks, string title)
        {
            if (stocks == null || stocks.Count == 0) return $"{title}: Veri yok.";
            return title + ":\n" + string.Join("\n", stocks.Take(5).Select(s => $"• ${s.Symbol}: {s.Close} (%{s.ChangePercent})"));
        }
    }
}

