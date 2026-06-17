// OPERATION_ENGINE_VERSION: 1.0
// PURPOSE: Handles scheduled operations (Morning Motivation, Market Close, Reports).
// Implements retry logic and decouples operational tasks from the UI.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

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
        private readonly PostingService _posting;

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
            PriceFetchService priceFetch,
            PostingService? posting = null)
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
            _posting = posting ?? new PostingService(socialIntel, stats);
        }

        public async Task RunMorningMotivation()
        {
            if (_spam.IsAlreadyPostedToday("MOTIVATION", "DAILY"))
            {
                OnLog?.Invoke("🛡️ Spam Protection (Motivation): Bugün zaten motivasyon tweeti atılmış. Atlanıyor.", "Operation");
                return;
            }

            if (!_spam.CanPostGeneral(out string reason, isCritical: true))
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
            // MARKET CLOSE is critical, ignore hourly/daily limits
            if (!_spam.CanPostGeneral(out string reason, ignoreLimits: true, isCritical: true))
            {
                OnLog?.Invoke($"🛡️ Spam Protection (Kapanış): {reason}", "Operation");
                return;
            }

            try
            {
                OnStatusUpdate?.Invoke("🌆 Piyasa kapanış özeti hazırlanıyor...");
                
                // iDeal log dosyalarından birincil veri kaynağı
                var (ideaLIndices, eodSnapshot, nabizUyarilari) = ReadIDeaLMarketData();

                // iDeal verisi yoksa internet kaynağına fallback
                string indices;
                if (!string.IsNullOrWhiteSpace(ideaLIndices))
                {
                    indices = ideaLIndices;
                    OnLog?.Invoke("✅ iDeal'den endeks verisi okundu.", "Operation");
                }
                else
                {
                    var financials = await _socialIntel.GetFinancialSummaryAsync();
                    indices = FormatIndices(financials);
                    OnLog?.Invoke("⚠️ iDeal verisi yok, internet kaynağı kullanılıyor.", "Operation");
                }

                var gainers = await _socialIntel.GetTopGainersAsync();
                var losers  = await _socialIntel.GetTopLosersAsync();
                var volume  = await _socialIntel.GetTopVolumeAsync();

                string gTable = FormatStockTable(gainers, "Kazananlar");
                string lTable = FormatStockTable(losers,  "Kaybedenler");
                string vTable = FormatStockTable(volume,  "Hacim");

                string? tweetSet = await _gemini.GenerateMarketCloseTableTweet(indices, gTable, lTable, vTable, nabizUyarilari, eodSnapshot);
                if (string.IsNullOrEmpty(tweetSet)) throw new Exception("Gemini report generation failed.");

                bool anySent = false;

                var tweets = ThreadPipeline.BuildCompactThread(tweetSet, 240, maxTweets: 5);
                if (tweets.Count > 0)
                {
                    var additions = new List<string>();
                    if (!tweets[^1].Contains("YTD", StringComparison.OrdinalIgnoreCase) &&
                        !tweets[^1].Contains("Yatırım tavsiyesi", StringComparison.OrdinalIgnoreCase)) additions.Add("⚠️ YTD");
                    if (!tweets[^1].Contains("#BIST100", StringComparison.OrdinalIgnoreCase)) additions.Add("#BIST100");
                    if (!tweets[^1].Contains("#Borsa", StringComparison.OrdinalIgnoreCase)) additions.Add("#Borsa");

                    string suffix = additions.Count > 0 ? "\n\n" + string.Join(" ", additions) : string.Empty;
                    var baseText = tweets[^1].Trim();
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        if (baseText.Length + suffix.Length > 240) baseText = baseText.Substring(0, Math.Max(0, 237 - suffix.Length)).TrimEnd() + "...";
                        tweets[^1] = baseText + suffix;
                    }
                }

                if (tweets.Count > 0)
                {
                    OnStatusUpdate?.Invoke($"🚀 Market Close Summary: {tweets.Count} tweets thread identifying. Posting...");
                    var result = await _posting.PostThreadAsync(tweets, null, "OperationEngine.MarketClose");
                    
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
                if (XiDeAI_Pro.Config.ConfigManager.Current.SpamProtectReports && !_spam.CanPostGeneral(out string reason, ignoreLimits: true, isCritical: true))
                {
                    OnLog?.Invoke($"🛡️ Spam koruması aktif: {reason}", "Operation");
                    return;
                }

                OnLog?.Invoke("📊 Mega-Thread paylaşılıyor...", "Operation");
                var ok = await _threadSvc.PostUnifiedDailyReportAsync(report, market, aiSummary, XiDeAI_Pro.Config.ConfigManager.Current.DailyTrends);

                if (ok)
                {
                    _spam.RecordTweet("REPORT", "DAILY_UNIFIED");
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
                    if (XiDeAI_Pro.Config.ConfigManager.Current.SpamProtectReports && !_spam.CanPostGeneral(out string reason, isCritical: true))
                    {
                        OnLog?.Invoke($"🛡️ Spam protection (Rapor/Haftalık): {reason}", "Operation");
                        return;
                    }
                    OnLog?.Invoke("📅 Posting weekly report...", "Operation");
                    var ok = await _threadSvc.PostWeeklyReportThread(report, XiDeAI_Pro.Config.ConfigManager.Current.DailyTrends);
                    if (ok) 
                    {
                        _spam.RecordTweet("REPORT", "WEEKLY");
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
            var res = await _posting.PostTweetAsync(content, null, $"OperationEngine.{category}");
            return res.status == "success";
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
            return title + ":\n" + string.Join("\n", stocks.Take(5).Select(s =>
            {
                string price = s.Close > 0 ? s.Close.ToString("0.00") : "-";
                string volume = s.Volume > 0 ? FormatCompactVolume(s.Volume) : "-";
                return $"• ${s.Symbol}: {price} (%{s.ChangePercent:0.0}) Hacim:{volume}";
            }));
        }

        private static string FormatCompactVolume(long volume)
        {
            if (volume >= 1_000_000_000) return (volume / 1_000_000_000m).ToString("0.0") + "B";
            if (volume >= 1_000_000) return (volume / 1_000_000m).ToString("0.0") + "M";
            if (volume >= 1_000) return (volume / 1_000m).ToString("0") + "K";
            return volume.ToString();
        }

        /// <summary>
        /// iDeal log dosyalarından anlık endeks durumu, gün sonu kapanış özeti ve nabız uyarılarını okur.
        /// Market_Status.txt → anlık durum | Market_Pulse_Alarm.txt → EOD_SNAPSHOT + alarm satırları
        /// </summary>
        internal static (string indicesData, string eodSnapshot, string nabizUyarilari) ReadIDeaLMarketData()
        {
            string indicesData   = "";
            string eodSnapshot   = "";
            string nabizUyarilari = "";

            static string FieldValue(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return "";
                string v = raw.Trim();
                int idx = v.IndexOf(':');
                if (idx >= 0 && idx + 1 < v.Length) v = v.Substring(idx + 1).Trim();
                return v;
            }

            static string NormalizeMoverLine(string raw)
            {
                string s = raw.Trim();
                s = s.Replace("^TAVAN", "TAVAN").Replace("�TAVAN", "TAVAN").Replace("�TABAN", "TABAN");
                s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
                return s;
            }

            try
            {
                // ── 1. Market_Status.txt → anlık endeks durumu ──────────────────────────────
                string statusFile = @"C:\iDeal\TARAMA_LOG\Market_Status.txt";
                if (System.IO.File.Exists(statusFile))
                {
                    // Format: datetime|MOD|YON|GunlukDeg%|Score|XU030%|XU050%|XU100_Fiyat|VolKat
                    var cols = System.IO.File.ReadAllText(statusFile).Split('|');
                    if (cols.Length >= 8)
                    {
                        string tarih = cols[0].Length >= 10 ? cols[0].Substring(0, 10) : cols[0];
                        if (DateTime.TryParse(tarih, out DateTime dt) && dt.Date == DateTime.Today)
                        {
                            string gunluk = FieldValue(cols[3]);
                            string xu030 = cols.Length >= 6 ? FieldValue(cols[5]) : "";
                            string xu050 = cols.Length >= 7 ? FieldValue(cols[6]) : "";
                            string fiyat = cols.Length >= 8 ? FieldValue(cols[7]) : "";
                            string volKat = cols.Length >= 9 ? FieldValue(cols[8]) : "";
                            indicesData = $"XU100 | Fiyat: {fiyat} | Günlük: {gunluk} | Trend: {cols[2]} | Mod: {cols[1]}";
                            if (!string.IsNullOrWhiteSpace(volKat)) indicesData += $" | Hacim Katı: {volKat}";
                            if (!string.IsNullOrWhiteSpace(xu030) || !string.IsNullOrWhiteSpace(xu050)) indicesData += $"\nXU030: {xu030} | XU050: {xu050}";
                        }
                    }
                }
            }
            catch { }

            try
            {
                // ── 2. Market_Pulse_Alarm.txt → EOD_SNAPSHOT + nabız uyarıları ────────────
                string pulseFile = @"C:\iDeal\TARAMA_LOG\Market_Pulse_Alarm.txt";
                if (System.IO.File.Exists(pulseFile))
                {
                    string todayPrefix = DateTime.Today.ToString("yyyy-MM-dd");
                    var alarmLines = new System.Collections.Generic.List<string>();
                    string eodLine = "";

                    foreach (var line in System.IO.File.ReadAllLines(pulseFile))
                    {
                        if (!line.StartsWith(todayPrefix)) continue;
                        if (line.Contains("EOD_SNAPSHOT"))
                            eodLine = line;
                        else
                            alarmLines.Add(line);
                    }

                    // EOD_SNAPSHOT parse (v5.4.7):
                    // datetime|EOD_SNAPSHOT|MOD|GunlukDeg:X%|XU100_Kapanis:X|GunYuksek:X|GunDusuk:X|GunRange:X%|XU030Deg:X%|XU050Deg:X%|PuanScore:X
                    //   |GunHacim:X|Ort10gHacim:X|HacimKar:Xx|XGLD:X|XGLD_Deg:X%|USDTRY:X|USDTRY_Deg:X%|BRENT:X|BRENT_Deg:X%|XSLV:X|XSLV_Deg:X%
                    if (!string.IsNullOrEmpty(eodLine))
                    {
                        var ep = eodLine.Split('|');
                        // Build structured EOD summary with all new fields
                        var eodSb = new System.Text.StringBuilder();
                        eodSb.AppendLine("=== GUN SONU OZET ===");
                        
                        // Mod Türkçeleştirme
                        string modRaw = ep[2].Trim();
                        string modTr = modRaw switch
                        {
                            "BULL" => "YÜKSELİŞ",
                            "CRASH" => "ÇÖKÜŞ",
                            "DIKKATLI" => "DİKKATLİ",
                            _ => modRaw
                        };

                        if (ep.Length >= 11)
                        {
                            eodSb.AppendLine($"Mod: {modTr} | Puan: {ep[10].Trim()}");
                            eodSb.AppendLine($"XU100: {ep[4].Trim()} | Gunluk: {ep[3].Trim()}");
                            eodSb.AppendLine($"Yuksek: {ep[5].Trim()} | Dusuk: {ep[6].Trim()} | Range: {ep[7].Trim()}");
                            eodSb.AppendLine($"XU030: {ep[8].Trim()} | XU050: {ep[9].Trim()}");
                        }
                        
                        // Yeni alanlar: Hacim karsilastirma (ep[11]-[13])
                        if (ep.Length >= 14)
                        {
                            eodSb.AppendLine($"Hacim: Gun={ep[11].Trim()} | 10g Ort={ep[12].Trim()} | Karsilastirma={ep[13].Trim()}");
                        }
                        
                        // Yeni alanlar: Global veriler (ep[14]-[21]) — Türkçe etiketlerle
                        if (ep.Length >= 22)
                        {
                            eodSb.AppendLine("GLOBAL VERİLER:");
                            eodSb.AppendLine($"  💰 Gram Altın (₺): {ep[14].Trim()} ({ep[15].Trim()})");
                            eodSb.AppendLine($"  🇺🇸 Dolar/TL: {ep[16].Trim()} ({ep[17].Trim()})");
                            eodSb.AppendLine($"  🛢️ Brent Petrol ($): {ep[18].Trim()} ({ep[19].Trim()})");
                            eodSb.AppendLine($"  ⚡ Gram Gümüş (₺): {ep[20].Trim()} ({ep[21].Trim()})");
                        }
                        
                        eodSnapshot = eodSb.ToString();
                    }

                    if (alarmLines.Count > 0)
                        nabizUyarilari = string.Join("\n", alarmLines);
                }
            }
            catch { }

            try
            {
                // ── 3. Market_Movers.txt → iDeal nabız robotunun yükselen/düşen listesi ──
                string moversFile = @"C:\iDeal\TARAMA_LOG\Market_Movers.txt";
                if (System.IO.File.Exists(moversFile))
                {
                    string text;
                    try { text = System.IO.File.ReadAllText(moversFile, Encoding.GetEncoding(1254)); }
                    catch { text = System.IO.File.ReadAllText(moversFile); }

                    if (text.Contains(DateTime.Today.ToString("yyyy-MM-dd")))
                    {
                        var moverRows = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(NormalizeMoverLine)
                            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d+\.\s+[A-Z0-9]{2,}") && l.Contains("%") && l.Contains("Hacim:"))
                            .ToList();
                        var gainers = moverRows.Take(8).ToList();
                        var losers = moverRows.Skip(20).Take(8).ToList();
                        var moverParts = new List<string>();
                        if (gainers.Count > 0) moverParts.Add("YUKSELENLER:\n" + string.Join("\n", gainers));
                        if (losers.Count > 0) moverParts.Add("DUSENLER:\n" + string.Join("\n", losers));
                        string moversText = string.Join("\n", moverParts);
                        if (!string.IsNullOrWhiteSpace(moversText))
                        {
                            eodSnapshot += (string.IsNullOrWhiteSpace(eodSnapshot) ? "" : "\n") + "IDEAL MARKET MOVERS:\n" + moversText;
                        }
                    }
                }
            }
            catch { }

            return (indicesData, eodSnapshot, nabizUyarilari);
        }
    }
}
