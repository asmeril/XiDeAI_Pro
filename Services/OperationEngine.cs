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
        private readonly PerformanceTracker _performance;
        private readonly ThreadService _threadSvc;
        private readonly PriceFetchService _priceFetch;

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
            StatsEngine stats,
            PerformanceTracker performance,
            ThreadService threadSvc,
            PriceFetchService priceFetch)
        {
            _gemini = gemini;
            _socialIntel = socialIntel;
            _twitter = twitter;
            _spam = spam;
            _prompts = prompts;
            _stats = stats;
            _performance = performance;
            _threadSvc = threadSvc;
            _priceFetch = priceFetch;
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
                    _stats.RecordTweet("Motivation", 1, "", tweet);
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
            // MARKET CLOSE is critical, ignore hourly/daily limits
            if (!_spam.CanPostGeneral(out string reason, ignoreLimits: true))
            {
                OnLog?.Invoke($"🛡️ Spam Protection (Kapanış): {reason}", "Operation");
                return;
            }

            try
            {
                OnStatusUpdate?.Invoke("🌆 Piyasa kapanış özeti hazırlanıyor...");
                
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

                bool anySent = false;

                var tweets = tweetSet.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(x => x.Trim())
                                   .Where(x => !string.IsNullOrEmpty(x))
                                   .ToList();

                if (tweets.Count > 0)
                {
                    OnStatusUpdate?.Invoke($"🚀 Market Close Summary: {tweets.Count} tweets thread identifying. Posting...");
                    var result = await _socialIntel.PostThreadAsync(tweets);
                    
                    if (result != null && result.status == "success")
                    {
                        OnLog?.Invoke("✅ Piyasa Kapanış Raporu (zincir) başarıyla paylaşıldı.", "Operation");
                        anySent = true;
                    }
                    else
                    {
                        OnLog?.Invoke($"❌ Piyasa Kapanış Raporu hatası: {result?.ErrorMessage ?? "Bilinmeyen hata"}", "Operation");
                    }
                }

                if (anySent)
                {
                    _closeRetry = 0;
                    OnLog?.Invoke("✅ Piyasa Kapanış Raporu (zincir) başarıyla paylaşıldı.", "Operation");
                }
                else if (tweets.Count > 0) OnLog?.Invoke("⚠️ Hiçbir tweet gönderilemedi (spam veya hata).", "Operation");
                else throw new Exception("No valid content generated.");
            }
            catch (Exception ex)
            {
                _closeRetry++;
                LogRetry("Market Close", _closeRetry, ex.Message, RunMarketCloseSummary);
            }
        }

        public async Task RunDailyReport()
        {
            try
            {
                OnLog?.Invoke("📊 Mega-Thread: Günlük Birleşik Rapor Hazırlanıyor...", "Operation");
                
                // 1. Piyasa Özetini Çek (FX, Altın, Gümüş, BIST100)
                var financials = await _socialIntel.GetFinancialSummaryAsync();
                var market = new MarketSnapshot();
                if (financials != null)
                {
                    market.Bist100 = financials.GetValueOrDefault("BIST100", "N/A");
                    market.UsdTry = financials.GetValueOrDefault("USD", "N/A");
                    market.EurTry = financials.GetValueOrDefault("EUR", "N/A");
                    market.Gold = financials.GetValueOrDefault("Gold", "N/A");
                    market.Silver = financials.GetValueOrDefault("Silver", "N/A");
                }

                // 2. En Çok Yükselenler/Düşenler
                var gainers = await _socialIntel.GetTopGainersAsync();
                if (gainers != null && gainers.Any())
                {
                    market.TopGainers = gainers.Select(d => new MarketMover { Symbol = d.Symbol, Price = d.Close, ChangePercent = d.ChangePercent }).ToList();
                }

                var losers = await _socialIntel.GetTopLosersAsync();
                if (losers != null && losers.Any())
                {
                    market.TopLosers = losers.Select(d => new MarketMover { Symbol = d.Symbol, Price = d.Close, ChangePercent = d.ChangePercent }).ToList();
                }

                // 2.1 En Çok Hacim Yapanlar (YENİ)
                var volume = await _socialIntel.GetTopVolumeAsync();
                if (volume != null && volume.Any())
                {
                    market.TopVolume = volume.Select(d => new MarketMover { Symbol = d.Symbol, Price = d.Close, ChangePercent = d.ChangePercent }).ToList();
                    OnLog?.Invoke($"✅ Hacim verileri alındı: {market.TopVolume.Count} hisse.", "Operation");
                }

                // 3. Sinyal Fiyatlarını Güncelle
                var todaySignals = _performance.GetDailyReport(DateTime.Now).Signals;
                if (todaySignals.Count > 0)
                {
                    var symbols = todaySignals.Select(s => s.Symbol).Distinct();
                    OnLog?.Invoke($"🔍 {symbols.Count()} hisse için güncel fiyatlar sorgulanıyor (PriceFetch)...", "Operation");
                    
                    var tasks = symbols.Select(async sym => 
                    {
                        // Auto-detect market type (Borsami? Kripto mu?)
                        string mType = "BIST";
                        if (sym.EndsWith("USDT") || sym.Length > 5) mType = "Kripto";
                        
                        var pInfo = await _priceFetch.GetPriceAsync(sym, mType);
                        return (Symbol: sym, Price: pInfo?.Price ?? 0);
                    });

                    var results = await Task.WhenAll(tasks);
                    var signalPrices = results.Where(x => x.Price > 0).ToDictionary(x => x.Symbol, x => x.Price);

                    if (signalPrices.Count > 0)
                    {
                        _performance.UpdateClosingPrices(signalPrices);
                        OnLog?.Invoke($"✅ {signalPrices.Count} hisse fiyatı güncellendi.", "Operation");
                    }
                }

                // 4. Raporu Sentezle
                decimal marketReturn = 0;
                var report = _performance.GetDailyReport(DateTime.Now, marketReturn);
                
                if (report.TotalSignals == 0 && market.Bist100 == "N/A")
                {
                    OnLog?.Invoke("ℹ️ Rapor için yeterli veri yok, atlanıyor.", "Operation");
                    return;
                }

                // 5. AI Sentezi
                OnLog?.Invoke("🤖 AI Performans Analizi Hazırlanıyor...", "Operation");
                string aiSummary = await _gemini.GeneratePerformanceSynthesis(report) ?? "Günün özeti hazırlandı.";

                // 6. Yayınla
                // MEGA-THREAD is critical, ignore hourly/daily limits
                if (XiDeAI_Pro.Config.ConfigManager.Current.SpamProtectReports && !_spam.CanPostGeneral(out string reason, ignoreLimits: true))
                {
                    OnLog?.Invoke($"🛡️ Spam koruması aktif: {reason}", "Operation");
                    return;
                }

                OnLog?.Invoke("📊 Mega-Thread paylaşılıyor...", "Operation");
                var ok = await _threadSvc.PostUnifiedDailyReportAsync(report, market, aiSummary, XiDeAI_Pro.Config.ConfigManager.Current.DailyTrends);

                if (ok)
                {
                    _spam.RecordTweet("REPORT", "DAILY_UNIFIED");
                    _stats.RecordTweet("DailyReport", 1, "", "Unified Daily Report Thread");
                    OnLog?.Invoke("✅ Birleşik Günlük Rapor başarıyla yayınlandı.", "Operation");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Raporlama Hatası: {ex.Message}", "Operation");
            }
        }

        public async Task RunWeeklyReport()
        {
            try
            {
                var report = _performance.GetWeeklyReport();
                if (report.TotalSignals > 0)
                {
                    if (XiDeAI_Pro.Config.ConfigManager.Current.SpamProtectReports && !_spam.CanPostGeneral(out string reason))
                    {
                        OnLog?.Invoke($"🛡️ Spam protection (Rapor/Haftalık): {reason}", "Operation");
                        return;
                    }
                    OnLog?.Invoke("📅 Posting weekly report...", "Operation");
                    var ok = await _threadSvc.PostWeeklyReportThread(report, XiDeAI_Pro.Config.ConfigManager.Current.DailyTrends);
                    if (ok) 
                    {
                        _spam.RecordTweet("REPORT", "WEEKLY");
                        _stats.RecordTweet("WeeklyReport", 1, "", "Weekly Report Thread");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Haftalık Rapor Hatası: {ex.Message}", "Operation");
            }
        }

        private async Task<bool> ExecuteScheduledTweet(string content, string category)
        {
            // TwitterService centrally handles both Selenium/WebView2 and API Fallback
            var res = await _twitter.SendTweetAsync(content);
            return !string.IsNullOrEmpty(res);
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

