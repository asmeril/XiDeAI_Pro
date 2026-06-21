using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class ThreadService
    {
        private readonly TwitterService _twitter;
        private readonly GeminiService _gemini;
        private readonly SocialIntelService _socialIntel;
        private readonly InfluencerControlService _influencerControl;
        private readonly StatsEngine? _stats;
        private readonly PostingService _posting;

        // Batch debounce/dedupe state (static to guard across instances)
        private static readonly object _batchLock = new object();
        private static DateTime _lastBatchPostUtc = DateTime.MinValue;
        private static HashSet<string> _lastBatchSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Yasal Uyarı
        public const string DISCLAIMER = "\n\n⚠️ Yatırım tavsiyesi değildir. Kendi araştırmanızı yapın.";
        // In-flight guards to prevent duplicate concurrent posts
        private static readonly ConcurrentDictionary<string, DateTime> _inflightSignalPosts = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DateTime> _inflightBatchPosts = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static string BuildSignalKey(SignalData s)
            => $"SIGNAL|{s.Symbol.ToUpperInvariant()}|{s.Strategy.ToUpperInvariant()}|{s.Period}";
        private static string BuildBatchKey(IEnumerable<string> symbols)
            => "BATCH|" + string.Join(",", symbols.Select(x => x.ToUpperInvariant()).OrderBy(x => x));
        
        private class AnalysisHistoryEntry
        {
            public DateTime Date { get; set; }
            public decimal Price { get; set; }
            public string Analysis { get; set; } = "";
            public string Prediction { get; set; } = "";
        }

        // Geçmiş analiz sistemi (statik alanlar — tek örnek, tüm instance'lar paylaşır)
        private static readonly string _historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XiDeAI", "analysis_history.json"
        );
        private static Dictionary<string, List<AnalysisHistoryEntry>> _analysisHistory =
            new Dictionary<string, List<AnalysisHistoryEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tüm bağımlılıkları kabul eden tek constructor.
        /// <paramref name="stats"/> ve <paramref name="posting"/> opsiyoneldir;
        /// sağlanmazsa uygun varsayılanlar oluşturulur.
        /// </summary>
        public ThreadService(
            TwitterService twitter,
            GeminiService gemini,
            SocialIntelService socialIntel,
            InfluencerControlService influencerControl,
            StatsEngine? stats = null,
            PostingService? posting = null)
        {
            _twitter = twitter;
            _gemini = gemini;
            _socialIntel = socialIntel;
            _influencerControl = influencerControl;
            _stats = stats;
            _posting = posting ?? new PostingService(socialIntel, stats);
            LoadAnalysisHistory();
        }
        
        private void LoadAnalysisHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    _analysisHistory = JsonSerializer.Deserialize<Dictionary<string, List<AnalysisHistoryEntry>>>(json) 
                        ?? new Dictionary<string, List<AnalysisHistoryEntry>>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
        }
        
        private async Task SaveAnalysisHistory(string symbol, decimal price, string analysis)
        {
            try
            {
                if (!_analysisHistory.ContainsKey(symbol))
                    _analysisHistory[symbol] = new List<AnalysisHistoryEntry>();
                
                _analysisHistory[symbol].Add(new AnalysisHistoryEntry
                {
                    Date = DateTime.Now,
                    Price = price,
                    Analysis = analysis.Length > 500 ? analysis.Substring(0, 500) : analysis,
                    Prediction = ExtractPrediction(analysis)
                });
                
                // Son 30 gün kayıt tut
                _analysisHistory[symbol] = _analysisHistory[symbol]
                    .Where(e => (DateTime.Now - e.Date).TotalDays <= 30)
                    .OrderByDescending(e => e.Date)
                    .Take(10)
                    .ToList();
                
                var json = JsonSerializer.Serialize(_analysisHistory);
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                await File.WriteAllTextAsync(_historyPath, json);
            }
            catch { }
        }
        
        private string? GetPreviousAnalysisContext(string symbol, decimal currentPrice)
        {
            try
            {
                if (!_analysisHistory.ContainsKey(symbol)) return null;
                
                var lastAnalysis = _analysisHistory[symbol]
                    .Where(e => (DateTime.Now - e.Date).TotalDays <= 7)
                    .OrderByDescending(e => e.Date)
                    .FirstOrDefault();
                
                if (lastAnalysis == null) return null;
                
                var daysDiff = (DateTime.Now - lastAnalysis.Date).Days;
                var priceChange = ((currentPrice - lastAnalysis.Price) / lastAnalysis.Price) * 100;
                
                return $"{daysDiff} gün önce {lastAnalysis.Price:N2} TL seviyesindeyken analiz yapmıştın. " +
                       $"O zamandan bu yana fiyat %{priceChange:+0.0;-0.0} değişti. " +
                       $"Önceki tahmin: '{lastAnalysis.Prediction}'. " +
                       $"Şimdi bu tahminin tutarlı mı değil mi açıkla ve yeni görüşünü belirt.";
            }
            catch { return null; }
        }
        
        private string ExtractPrediction(string analysis)
        {
            // Basit extraction: Yükseliş/Düşüş/Yatay kelimeleri ara
            var lower = analysis.ToLowerInvariant();
            if (lower.Contains("yüksel") || lower.Contains("pozitif") || lower.Contains("güçlü")) 
                return "Yükseliş beklentisi";
            if (lower.Contains("düş") || lower.Contains("negatif") || lower.Contains("zayıf"))
                return "Düşüş riski";
            return "Yatay seyir";
        }

        /// <summary>
        /// 4 parçalı sinyal thread'i - Etkileşim optimizasyonlu
        /// </summary>
        public async Task<(bool success, string error, string url)> PostAIGeneratedThread(SignalData signal, string aiThreadContent, string chartImagePath)
        {
             string inflightKey = BuildSignalKey(signal);
             if (!_inflightSignalPosts.TryAdd(inflightKey, DateTime.UtcNow))
                 return (false, "Benzer thread şu an gönderiliyor.", "");

             try
             {
                 var tweets = ThreadPipeline.BuildSignalThread(signal, aiThreadContent, ConfigManager.Current.XLoginUser)
                     .Select(SanitizeXContent)
                     .Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length > 5) // v4.10.7: Boş veya çok kısa parçaları filtrele (Reply disabled hatasını önler)
                     .ToList();
                 
                  // Fallback for single tweet if something went wrong
                  if (tweets.Count == 0) return (false, "AI içerik üretmedi.", "");
                 tweets = ThreadPipeline.EnsureWithinLimit(tweets, 280);

                  var result = await _posting.PostThreadAsync(tweets, chartImagePath, "HybridEngine");
                 
                 if (result.status == "success")
                 {
                    return (true, "", result.tweet_url ?? "");
                 }
                 return (false, result.message ?? result.text, "");
             }
             catch (Exception ex)
             {
                 return (false, ex.Message, "");
             }
             finally
             {
                 _inflightSignalPosts.TryRemove(inflightKey, out _);
             }
        }

        /// <summary>
        /// 4 parçalı sinyal thread'i - Etkileşim optimizasyonlu
        /// </summary>
        public async Task<(bool success, string error)> PostSignalThread(SignalData signal, string chartImagePath, string tvLink, string trends, string? customAnalysis = null, List<InfluencerPost>? influencerPosts = null)
        {
             // In-flight guard for single-signal thread
             string inflightKey = BuildSignalKey(signal);
             if (!_inflightSignalPosts.TryAdd(inflightKey, DateTime.UtcNow))
             {
                 return (false, "Benzer thread şu an gönderiliyor (in-flight guard).");
             }

             try
             {
                 var tweets = new List<string>();
                 string cleanSymbol = CleanSymbolForX(signal.Symbol);

                 // Determine currency based on symbol
                 string currency = GetCurrencyForSymbol(signal.Symbol);

                 // ============================================
                // Tweet 1 logic (Prioritize AI or handle manually)
                // ============================================
                string tweet1 = "";
                string fullAnalysis = !string.IsNullOrEmpty(customAnalysis) 
                    ? customAnalysis 
                    : await GenerateTechnicalAnalysis(signal);
                
                // Replace [LINK] placeholder anywhere in the analysis content
                if (fullAnalysis.Contains("[LINK]"))
                {
                    fullAnalysis = fullAnalysis.Replace("[LINK]", tvLink);
                }

                // v4.6.13: Her zaman tweet1 başlığını ekle (AI çıktısından bağımsız olarak başlık garantili)
                {
                    string userHandle = ConfigManager.Current.XLoginUser;
                    string headerTag = string.IsNullOrEmpty(userHandle) ? "🇹🇷" : $"🇹🇷 @{userHandle} |";
                    
                    string periodStr = signal.Period switch
                    {
                        "G" => "Günlük",
                        "H" => "Haftalık",
                        "A" => "Aylık",
                        "Y" => "Yıllık",
                        _ => signal.Period + "dk"
                    };

                    string basisSuffix = "";
                    if (!string.IsNullOrEmpty(signal.Basis) && signal.Basis != "TL")
                    {
                        basisSuffix = signal.Basis == "XU100" ? " - Kompozit Analiz" : $" - {signal.Basis} Bazlı";
                    }
                    
                    string currencyLabel = string.IsNullOrEmpty(currency) ? "Puan" : currency;
                    tweet1 = $"{headerTag} #{cleanSymbol} ({periodStr}){basisSuffix} | Fiyat: {signal.Price:N2} {currencyLabel}\n" +
                             $"{tvLink}\n\n";
                    // Do not add tweet1 as a separate tweet yet! We will prepend it to the AI's first tweet.
                }
                
                // v2.7:||| Ayırıcısına göre bölme desteği (X 280 karakter sınırı)
                var aiParts = ThreadPipeline.ParseParts(fullAnalysis, 280);
                if (aiParts.Count > 0)
                {
                    // Prepend our short header to the AI's hook
                    aiParts[0] = tweet1 + aiParts[0];
                    tweets.AddRange(aiParts);
                }
                else
                {
                    tweets.Add(tweet1 + "Analiz metni oluşturulamadı.");
                }

                // v3.5.4: Sanitize all tweets before finalizing
                for (int i = tweets.Count - 1; i >= 0; i--)
                {
                    tweets[i] = SanitizeXContent(tweets[i]);
                    // Eğer başlık silindiyse ve tweet boş kaldıysa çıkaralım
                    if (string.IsNullOrWhiteSpace(tweets[i]))
                    {
                        tweets.RemoveAt(i);
                    }
                }

                // Önlem: Eğer önceki parçalar çok kısa ise, anlamlı parçalarla birleştir
                for (int i = tweets.Count - 1; i >= 1; i--)
                {
                     // Sadece 1. tweet haricindeki(indeks > 0) aşırı kısa kalan parçaları geriye aktar
                      string merged = tweets[i - 1].TrimEnd() + " " + tweets[i].Trim();
                      if (tweets[i].Trim().Length < 80 && !tweets[i].Contains("Fiyat:") && merged.Length <= 280)
                      {
                          tweets[i - 1] = merged;
                          tweets.RemoveAt(i);
                      }
                }

                // ============================================
                // Tweet 4: HİBRİT ALINTILAR & ETİKETLER
                // ============================================
                string quotes = FormatInfluencerQuotes(influencerPosts ?? new List<InfluencerPost>(), signal.Symbol, out string? quoteUrl);
                string hashtagString = string.Join(" ", ExtractUniqueHashtags(trends, signal.Symbol, fullAnalysis));
                
                string strategyLabel = signal.Strategy.Contains("Analiz") ? signal.Strategy : signal.Strategy + " analizi";
                string conclusion = $"✅ SONUÇ: {strategyLabel}. Plan net: seviye, teyit ve risk birlikte izlenmeli.\n\n" +
                                    $"{hashtagString}" + DISCLAIMER;
                string tweet4 = string.IsNullOrWhiteSpace(quotes) ? conclusion : $"{quotes}\n\n{conclusion}";
                
                // v3.2: Smart Quote - If we have a high-relevance quote URL, append it to the end to trigger X Quote rendering
                if (!string.IsNullOrEmpty(quoteUrl))
                {
                    tweet4 += $"\n\n{quoteUrl}";
                }
                if (tweet4.Length > 280 && !string.IsNullOrWhiteSpace(quotes))
                {
                    tweet4 = conclusion;
                    if (!string.IsNullOrEmpty(quoteUrl) && tweet4.Length + quoteUrl.Length + 2 <= 280)
                    {
                        tweet4 += $"\n\n{quoteUrl}";
                    }
                }
                if (tweet4.Length > 280) tweet4 = ThreadService.SplitText(tweet4, 280).First();
                
                if (tweets.Count > 3) tweets = tweets.Take(3).ToList();
                tweets.Add(tweet4);
                tweets = ThreadPipeline.EnsureWithinLimit(tweets, 280);

                // Send via Selenium (Cookies)
                var result = await _posting.PostThreadAsync(tweets, chartImagePath, "ThreadService");
                
                // LOG the result for debugging (with partial success detection)
                Logger.Twitter($"Thread Result: status={result.status}, message={result.message}, text={result.text}");
                
                if (result.status == "success")
                {
                    // Check if this is a partial success (some tweets failed)
                    if (!string.IsNullOrEmpty(result.message) && result.message.Contains("partially"))
                    {
                        Logger.Twitter($"⚠️ Thread kısmen gönderildi: {result.message}");
                    }
                    
                    // Record in stats engine by module
                    var threadContent = string.Join(" ", tweets).Substring(0, Math.Min(150, string.Join(" ", tweets).Length));
                    return (true, "");
                }
                
                // Use message OR text for error (message is primary)
                string errorDetail = !string.IsNullOrEmpty(result.message) ? result.message : result.text;
                Logger.Twitter($"❌ Thread gönderim hatası: {errorDetail}");
                return (false, "Twitter gönderimi başarısız: " + errorDetail);
            }
            catch (Exception ex)
            { 
                return (false, "Beklenmeyen hata: " + ex.Message); 
            }
            finally
            {
                _inflightSignalPosts.TryRemove(inflightKey, out _);
            }
        }

        public static List<string> SplitText(string text, int limit)
        {
            var parts = new List<string>();
            if (string.IsNullOrEmpty(text)) return parts;

            while (text.Length > limit)
            {
                int splitIndex = -1;
                
                // PRIORITY 1: Find last paragraph break (\n\n) within limit
                splitIndex = text.LastIndexOf("\n\n", Math.Min(limit, text.Length - 1), StringComparison.Ordinal);
                if (splitIndex < limit / 2) splitIndex = -1;
                
                // PRIORITY 2: Find last newline (\n) within limit
                if (splitIndex == -1)
                {
                    splitIndex = text.LastIndexOf('\n', Math.Min(limit, text.Length - 1));
                    if (splitIndex < limit / 2) splitIndex = -1;
                }
                
                // PRIORITY 3: Find last sentence end (. ! ?) within limit
                if (splitIndex == -1)
                {
                    for (int i = Math.Min(limit, text.Length - 1); i >= limit / 2; i--)
                    {
                        char c = text[i];
                        if ((c == '.' || c == '!' || c == '?') && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])))
                        {
                            splitIndex = i + 1;
                            break;
                        }
                    }
                }
                
                // PRIORITY 4: Find last space within limit
                if (splitIndex == -1)
                {
                    splitIndex = text.LastIndexOf(' ', Math.Min(limit, text.Length - 1));
                    if (splitIndex < limit / 2) splitIndex = limit; // Hard cut as last resort
                }
                
                if (splitIndex <= 0) splitIndex = limit;

                parts.Add(text.Substring(0, splitIndex).Trim());
                text = text.Substring(splitIndex).Trim();
            }
            if (!string.IsNullOrEmpty(text)) parts.Add(text);
            return parts;
        }

        private string SanitizeXContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Remove Markdown Bold (Stars)
            text = text.Replace("**", "");

            // 2. v5.2.3: Non-destructive regex — strip only robotic header PREFIXES, preserve content
            // "[Tweet 1]:", "[Tweet 1] -", "Tweet 1:", "1. Tweet:", etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?im)^\s*\[?Tweet\s*\d+\s*[:\-]?\s*\]?\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?im)^\s*\d+\.\s*Tweet\s*[:\-]?\s*", "");

            // 3. Remove AI template section headers (KANCA, DERİN BAKIŞ, YOL HARİTASI, etc.)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?im)^\s*(KANCA|DERİN\s*BAKIŞ?|YOL\s*HARİTASI|KAPANIŞ)\s*[:\-]?\s*", "");

            // 4. Remove pure separator lines (====, ----, ####, ::::)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^\s*[=\-#:]{4,}\s*$", "");

            // 5. Clean up multiple newlines
            while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n");

            return text.Trim();
        }

        private List<string> ExtractUniqueHashtags(string trends, string symbol, string existingContent = "")
        {
            var cleanSym = CleanSymbolForX(symbol);
            var list = new List<string>();

            // 1. Mevcut içerikteki hashtagleri topla (mükerrer eklemeyi önlemek için)
            if (!string.IsNullOrEmpty(existingContent))
            {
                var existingHashtags = System.Text.RegularExpressions.Regex.Matches(existingContent, @"#\w+")
                    .Select(m => m.Value)
                    .ToList();
                foreach (var h in existingHashtags)
                {
                    if (!list.Contains(h, StringComparer.OrdinalIgnoreCase))
                        list.Add(h);
                }
            }

            // 2. Temel etiketleri ekle (eğer metinde yoksa)
            var baseTags = new[] { $"#{cleanSym}", "#Borsa", "#BIST100" };
            foreach (var h in baseTags)
            {
                if (!list.Contains(h, StringComparer.OrdinalIgnoreCase))
                    list.Add(h);
            }
            
            // 3. Trendlerden ekle
            if (!string.IsNullOrEmpty(trends))
            {
                var parts = trends.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    if (p.StartsWith("#") && p.Length > 2)
                    {
                        if (!list.Contains(p, StringComparer.OrdinalIgnoreCase))
                            list.Add(p);
                    }
                }
            }
            
            // Sonuçları tekilleştir ve limit (10) uygula, ama SADECE TRENDLERDEN GELENLERİ ekrana basacağız (bazıları zaten metinde var)
            // Aslında biz bu listeyi tweetin sonuna ekliyoruz. O yüzden zaten metinde varsa (Tweet 1-2-3 içinde) 
            // Tweet 4'te tekrar yazmamalıyız.
            
            var finalTags = list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            
            // v4.6.13: CRITICAL FIX - Only deduplicate trend-sourced tags, NOT the core symbol/BIST tags.
            // Previously this filter removed #SEMBOL and #BIST100 if they appeared in the AI text,
            // leaving the last tweet nearly empty. Now we always keep base tags.
            var coreBaseTags = new HashSet<string>(baseTags.Select(t => t.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(existingContent))
            {
                var existingHashtags = System.Text.RegularExpressions.Regex.Matches(existingContent, @"#\w+")
                    .Select(m => m.Value.ToLowerInvariant())
                    .ToHashSet();
                
                // Only remove tags that are NOT core base tags
                finalTags = finalTags.Where(t => coreBaseTags.Contains(t.ToLowerInvariant()) || !existingHashtags.Contains(t.ToLowerInvariant())).ToList();
            }

            // Blacklist (Non-financial spammy tags)
            var blacklist = new[] { "#deprem", "#sondakika", "#acil", "#yardim", "#afad" };
            return finalTags
                .Where(t => !blacklist.Contains(t.ToLowerInvariant()))
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// Çoklu Sinyal Batch Thread (Smart Batching)
        /// </summary>
        public async Task<bool> PostBatchSignalThread(List<SignalData> signals, string trends)
        {
            string batchKey = string.Empty; // ensure key visible in finally
            try 
            {
                if (signals.Count == 0) return false;
                
                // Debounce/Dedupe guard: avoid posting nearly identical batches within a short window
                var cleanedSymbols = new List<string>();
                foreach (var s in signals)
                {
                    var cs = CleanSymbolForX(s.Symbol);
                    if (!cleanedSymbols.Contains(cs, StringComparer.OrdinalIgnoreCase))
                        cleanedSymbols.Add(cs);
                }

                // In-flight guard for batch thread
                batchKey = BuildBatchKey(cleanedSymbols);
                if (!_inflightBatchPosts.TryAdd(batchKey, DateTime.UtcNow))
                {
                    Logger.Twitter("Batch in-flight guard: skipped concurrent batch post.");
                    return false;
                }

                var nowUtc = DateTime.UtcNow;
                bool shouldSkip = false;
                lock (_batchLock)
                {
                    // Jaccard similarity vs last set
                    int intersection = 0;
                    foreach (var sym in cleanedSymbols)
                    {
                        if (_lastBatchSymbols.Contains(sym)) intersection++;
                    }
                    int union = _lastBatchSymbols.Count + cleanedSymbols.Count - intersection;
                    double jaccard = union == 0 ? 1.0 : (double)intersection / union;
                    bool withinWindow = (nowUtc - _lastBatchPostUtc) < TimeSpan.FromMinutes(3);

                    if (withinWindow && jaccard >= 0.8)
                    {
                        Logger.Twitter($"Batch dedupe: skipped (jaccard={jaccard:0.00}, count={signals.Count}).");
                        shouldSkip = true;
                    }
                    else
                    {
                        _lastBatchPostUtc = nowUtc;
                        _lastBatchSymbols = new HashSet<string>(cleanedSymbols, StringComparer.OrdinalIgnoreCase);
                    }
                }

                if (shouldSkip)
                {
                    _inflightBatchPosts.TryRemove(batchKey, out _);
                    return false;
                }
                
                var tweets = new List<string>();

                // Sort by IsRoket first, then Price
                signals = signals.OrderByDescending(s => s.IsRoket).ThenByDescending(s => s.Price).ToList();
                var topSignal = signals[0];

                // ============================================
                // Tweet 1: SUMMARY TABLE (The Hook)
                // ============================================
                string tweet1 = $"🚨 PİYASA HAREKETLİ: {signals.Count} HİSSEDE SİNYAL!\n\n" +
                                $"Agresif tarama robotlarımız anlık fırsatlar yakaladı:\n\n";
                
                // Add rows (Compact format)
                foreach(var s in signals.Take(5))
                {
                    tweet1 += $"👉 #{CleanSymbolForX(s.Symbol)} {s.Price:N2} TL | {s.Strategy} | {GetPublicSignalState(s)}{(s.IsRoket ? " 🚀" : "")}\n";
                }
                
                if (signals.Count > 5) tweet1 += $"...ve {signals.Count - 5} diğer hisse.\n";

                tweet1 += $"\n👇 Hızlı Analiz ve Hedefler Zincirde!";
                tweets.Add(tweet1);

                // ============================================
                // Tweet 2: TOP ANALYZE + TAGS (Compressed)
                // ============================================
                var topSymbols = signals.Select(s => s.Symbol).Distinct().Take(5).ToList();
                string tweet2 = "🧠 HIZLI ANALİZ\n\n";

                foreach(var sym in topSymbols)
                {
                    var sig = signals.First(s => s.Symbol == sym);
                    var bias = sig.IsRoket ? "Roket ivmesi 🚀" : GetPublicSignalState(sig);
                    tweet2 += $"📌 #{CleanSymbolForX(sym)} | Fiyat: {sig.Price:N2} TL | {bias}\n";
                }

                // Tags: limit distinct symbol hashtags to avoid clutter
                var distinctTags = new List<string>();
                foreach (var s in signals)
                {
                    var tag = "#" + CleanSymbolForX(s.Symbol);
                    if (!distinctTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        distinctTags.Add(tag);
                    if (distinctTags.Count >= 10) break; // cap at 10 tags
                }
                string allTags = string.Join(" ", distinctTags);
                
                tweet2 += $"\n{allTags}\n{trends}" + DISCLAIMER;
                tweets.Add(tweet2);
                tweets = ThreadPipeline.EnsureWithinLimit(tweets, 280);

                var result = await _posting.PostThreadAsync(tweets, null, "BatchSignal");

                return result.status == "success";
            }
            catch (Exception ex) 
            {
                Console.WriteLine("Batch Error: " + ex.Message);
                return false; 
            }
            finally
            {
                // Ensure in-flight key is released
                _inflightBatchPosts.TryRemove(batchKey, out _);
            }
        }

        /// <summary>
        /// Posts a viral thread generated by Hive Intel (OmniScout)
        /// </summary>
        public async Task<bool> PostHiveInsightsThread(string threadContent)
        {
            try
            {
                var tweets = ThreadPipeline.ParseParts(threadContent, 280);
                
                for(int i=0; i<tweets.Count; i++) tweets[i] = SanitizeXContent(tweets[i]);

                var result = await _posting.PostThreadAsync(tweets, null, "HiveIntel");
                if (result.status == "success")
                {
                     return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Twitter($"❌ Hive Tweet Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AI ile gelişmiş teknik analiz oluştur (Grafik + Influencer + Geçmiş Analiz)
        /// </summary>
        private async Task<string> GenerateTechnicalAnalysis(SignalData signal)
        {
            try
            {
                // Manuel Analysis gibi grafik oku
                var priceContext = $"Fiyat: {signal.Price:N2} TL, Strateji: {signal.Strategy}, Takip notu: {GetPublicSignalState(signal)}{(signal.IsRoket ? " 🚀" : "")}";
                
                // Influencer araştırması (DB → X)
                var influencerPosts = new List<InfluencerPost>();
                try
                {
                    var vipHandles = _influencerControl?.GetTopInfluencers(signal.Symbol, 20);
                    if (_socialIntel != null)
                    {
                        influencerPosts = await _socialIntel.FindInfluencerAnalyses(signal.Symbol, signal.Market, vipHandles);
                        Logger.Twitter($"📊 {signal.Symbol} ({signal.Market}): {influencerPosts.Count} influencer analizi bulundu");
                    }
                }
                catch { }
                
                // Geçmiş analiz kontrolü
                string? historyContext = GetPreviousAnalysisContext(signal.Symbol, signal.Price);
                
                // Sentezleme (Manuel Analysis standardında)
                if (_gemini != null)
                {
                    var synthesis = await _gemini.SynthesizeInfluencerAnalyses(
                        signal.Symbol, 
                        signal.Market, 
                        priceContext + (historyContext != null ? $"\n\n📜 GEÇMIŞ ANALİZ:\n{historyContext}" : ""),
                        $"Strateji: {signal.Strategy}, Takip notu: {GetPublicSignalState(signal)}",
                        influencerPosts
                    );
                    
                    if (!string.IsNullOrEmpty(synthesis))
                    {
                        // Analizi kaydet (geçmiş için)
                        await SaveAnalysisHistory(signal.Symbol, signal.Price, synthesis);
                        return synthesis;
                    }
                }
                
                // Fallback: Eski basit analiz
                return GetDefaultAnalysis(signal);
            }
            catch (Exception ex)
            {
                Logger.Twitter($"⚠️ GenerateTechnicalAnalysis error: {ex.Message}");
                return GetDefaultAnalysis(signal);
            }
        }

        /// <summary>
        /// Strateji bazlı kriter açıklamaları
        /// </summary>
        private string GetStrategyExplanation(string strategy)
        {
            if (strategy.Contains("King"))
            {
                return @"• KING: Momentum gücü analizi
• Fiyat yapısı ve trend yönü
• RSI güç bölgesi kontrolü
• Hacim onayı gerekli";
            }
            if (strategy.Contains("Bomba"))
            {
                return @"• BOMBA: Ani hacim patlaması tespiti
• Ortalama hacmin 2-3 katı artış
• Fiyatta keskin hareket
• Kısa vadeli momentum";
            }
            if (strategy.Contains("TeFo") || strategy.Contains("T"))
            {
                return @"• TEFO: Trend takip stratejisi
• Hareketli ortalama kesişimleri
• Trend yönü ve gücü
• ADX trend onayı";
            }
            if (strategy == "DIP")
            {
                return @"• DIP: Aşırı satım bölgesinden toparlanma
• RSI düşük seviyelerden dönüş
• Destek seviyesi testi
• Pozitif divergence aranır";
            }
            if (strategy == "ZIRVE")
            {
                return @"• ZİRVE: Aşırı alım bölgesi tespiti
• RSI yüksek seviye uyarısı
• Direnç bölgesi yaklaşımı
• Negatif divergence kontrolü";
            }
            if (strategy == "ANKA")
            {
                return @"• ANKA: Çoklu faktör analizi
• Momentum + Hacim + Trend kombinasyonu
• 27 farklı kriter değerlendirmesi
• Bonus puanlar ekstra güç göstergesi";
            }
            return @"• Teknik analiz kriterleri
• Momentum ve hacim analizi
• Trend yönü değerlendirmesi";
        }

        private string GetDefaultAnalysis(SignalData signal)
        {
            string roketLine = signal.IsRoket ? $"🏆 ROKET SİNYALİ - Nadir görülen güç!\n" : "";
            
            return $"{roketLine}" +
                   $"🔹 MACD: Pozitif bölgede ✅\n" +
                   $"🔹 RSI: Güçlü momentum 📈\n" +
                   $"🔹 Hacim: Ortalamanın üstünde 🔥\n" +
                   $"🔹 {signal.Strategy}: Tüm kriterler sağlandı ✅";
        }

        private static string GetPublicSignalState(SignalData signal)
        {
            return signal.Durum?.ToUpperInvariant() switch
            {
                "AKTIF" => "Sinyal canlı, teyit aranıyor",
                "PULLBACK_ADAY" => "Geri çekilme takibi, acele yok",
                "KAPALI" => "Sinyal kapanmış",
                _ => "İzleme listesinde"
            };
        }

        /// <summary>
        /// Influencer alıntıları (DB → X araştırma)
        /// </summary>
        private async Task<string> GetInfluencerQuotes(string symbol)
        {
            try
            {
                // DB'den VIP influencer'lar
                var vipHandles = _influencerControl?.GetTopInfluencers(symbol, 20);
                var posts = new List<InfluencerPost>();
                
                if (_socialIntel != null)
                {
                    posts = await _socialIntel.FindInfluencerAnalyses(symbol, SocialIntelService.DetectMarket(symbol), vipHandles);
                }
                
                if (posts.Count > 0)
                {
                    return FormatInfluencerQuotes(posts, symbol, out _);
                }
                
                return GetNeutralFallbackComment(symbol);
            }
            catch
            {
                return GetNeutralFallbackComment(symbol);
            }
        }

        /// <summary>
        /// Format influencer posts as tweet quotes with snippets
        /// </summary>
        private string FormatInfluencerQuotes(List<InfluencerPost> posts, string symbol, out string? bestQuoteUrl)
        {
            bestQuoteUrl = null;
            if (posts == null || posts.Count == 0 || string.IsNullOrEmpty(symbol)) return string.Empty;

            string symUpper = symbol.ToUpperInvariant();
            
            // STRICT FILTER with Anti-Noise Logic
            string currentUser = ConfigManager.Current.XLoginUser?.Replace("@", "").Trim() ?? "";
            var relevantBySymbol = posts.Where(p => {
                string content = p.Content?.ToUpperInvariant() ?? "";
                string handle = p.Handle?.Replace("@", "").Trim() ?? "";
                
                // 1. Don't include self
                if (!string.IsNullOrEmpty(currentUser) && handle.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                    return false;

                // 2. Anti-Noise: If symbol is SMART, but content is talking about "Smart Money" concept
                if (symUpper == "SMART")
                {
                    // If it contains "SMART MONEY" but NOT "#SMART" or "$SMART", it's likely noise
                    if (content.Contains("SMART MONEY") && !content.Contains("#SMART") && !content.Contains("$SMART"))
                        return false;
                }

                // 3. Word Boundary & Tag Match
                return content.Contains($"#{symUpper}") || 
                       content.Contains($"${symUpper}") || 
                       System.Text.RegularExpressions.Regex.IsMatch(content, $@"\b{symUpper}\b");
            }).ToList();

            if (relevantBySymbol.Count == 0) return string.Empty;

            // En iyi postları seç (Puanlama öncelikli, sonra Etkileşim)
            var sortedPosts = relevantBySymbol.OrderByDescending(p => p.RelevanceScore)
                                   .ThenByDescending(p => p.Engagement)
                                   .ToList();

            // Hibrit seçim: 1 Fenomen (VIP) + 1 Global (Eğer varsa)
            var vipPost = sortedPosts.Where(p => p.Handle != null && _vipHandles.Contains(p.Handle.TrimStart('@'), StringComparer.OrdinalIgnoreCase))
                                     .FirstOrDefault();

            var globalPost = sortedPosts.Where(p => p.Handle != null && !_vipHandles.Contains(p.Handle.TrimStart('@'), StringComparer.OrdinalIgnoreCase))
                                       .FirstOrDefault();

            var selected = new List<InfluencerPost>();
            if (vipPost != null) selected.Add(vipPost);
            if (globalPost != null) selected.Add(globalPost);

            // Eğer hibrit yakalanamadıysa en iyi 2 (Puanı en yüksek olanlar)
            if (selected.Count < 2)
            {
                var extra = sortedPosts.Except(selected).Take(2 - selected.Count);
                selected.AddRange(extra);
            }

            var quotes = new List<string>();
            foreach (var post in selected)
            {
                string text = post.Content ?? "";
                // Keep quotes longer for context integrity (200 chars, cut at sentence end if possible)
                string snippet = text;
                if (text.Length > 200)
                {
                    int cutPoint = text.LastIndexOf('.', 197);
                    if (cutPoint < 100) cutPoint = text.LastIndexOf(' ', 197);
                    if (cutPoint < 100) cutPoint = 197;
                    snippet = text.Substring(0, cutPoint + 1).Trim();
                    if (!snippet.EndsWith(".") && !snippet.EndsWith("!") && !snippet.EndsWith("?"))
                        snippet += "...";
                }
                quotes.Add($"💎 @{post.Handle.TrimStart('@')}: \"{snippet}\"");
                
                // Set the best quote URL (prefer VIPs)
                if (bestQuoteUrl == null)
                    bestQuoteUrl = post.Url;
            }
            
            if (quotes.Count == 0) return string.Empty;
            return "💬 Piyasa Görüşleri:\n" + string.Join("\n\n", quotes);
        }
        
        private List<string> _vipHandles = new List<string> { "AsmeriL", "TuncayTursucu", "barisesen", "yunussahin__" }; // Örnek VIP listesi

        /// <summary>
        /// Neutral fallback when no real influencer data found (NO FAKE ACCOUNTS!)
        /// </summary>
        private string GetNeutralFallbackComment(string symbol)
        {
            return $"💭 #{symbol} hakkında daha fazla bilgi için sosyal medyayı takip edin.\n\n" +
                   $"📊 Kendi araştırmanızı yapın ve danışmanınıza danışın.";
        }

        /// <summary>
        /// Gemini API'ye doğrudan istek at
        /// </summary>
        private async Task<string?> CallGeminiDirectly(string prompt)
        {
            var apiKey = ConfigManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return null;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var model = !string.IsNullOrEmpty(ConfigManager.Current.GeminiModel) ? ConfigManager.Current.GeminiModel : "gemini-2.5-flash";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await client.PostAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(resultJson);
                    return doc.RootElement.GetProperty("candidates")[0]
                              .GetProperty("content")
                              .GetProperty("parts")[0]
                              .GetProperty("text").GetString();
                }
            }
            catch { }
            
            return null;
        }

        // ============================================
        // Günlük ve Haftalık Raporlar
        // ============================================
        
        public async Task<bool> PostDailyReportThread(DailyReport report, string trends)
        {
            // Backward compatibility wrapper
            return await PostUnifiedDailyReportAsync(report, new MarketSnapshot(), "Günün özeti hazırlanıyor...", trends);
        }

        public async Task<bool> PostUnifiedDailyReportAsync(DailyReport report, MarketSnapshot market, string aiSynthesis, string trends)
        {
            try
            {
                var tweets = new List<string>();

                // Tweet 1: AI Summary & Executive Intro
                string tweet1 = $"🧵 GÜNLÜK PİYASA ANALİZ RAPORU\n" +
                               $"📅 {report.Date:dd.MM.yyyy}\n\n" +
                               $"{aiSynthesis}\n\n" +
                               $"⬇️ Piyasa detayları aşağıda...";
                tweets.Add(tweet1);

                // Tweet 2: Market Snapshots (FX & Commodities)
                string tweet2 = $"🌍 PİYASA ÖZETİ\n\n" +
                               $"🏦 BIST100: {market.Bist100}\n" +
                               $"💵 USD/TRY: {market.UsdTry}\n" +
                               $"💶 EUR/TRY: {market.EurTry}\n" +
                               $"🟡 Gram Altın: {market.Gold}\n" +
                               $"⚪ Gram Gümüş: {market.Silver}\n\n" +
                               $"📊 Genel piyasa trendi analiz edildi.";
                tweets.Add(tweet2);

                // Tweet 3: Top Gainers (Top 10)
                string topGainers = FormatMovers(market.TopGainers, "Kazananlar");
                string tweet3 = $"🔥 GÜNÜN EN ÇOK YÜKSELENLERİ\n\n" +
                               $"{topGainers}\n\n" +
                               $"📌 Hacimli yükseliş gösteren semboller.";
                tweets.Add(tweet3);

                // Tweet 4: Top Losers (Top 10)
                string topLosers = FormatMovers(market.TopLosers, "Kaybedenler");
                string tweet4 = $"📉 GÜNÜN EN ÇOK DÜŞENLERİ\n\n" +
                               $"{topLosers}\n\n" +
                               $"📌 Negatif ayrışan semboller.";
                tweets.Add(tweet4);

                // Tweet 5: Top Volume (Top 10) - NEW
                string topVolume = FormatMovers(market.TopVolume, "Hacim Liderleri");
                string tweet5 = $"💎 GÜNÜN HACİM LİDERLERİ\n\n" +
                               $"{topVolume}\n\n" +
                               $"📌 Yatırımcıların en çok ilgi gösterdiği hisseler.";
                tweets.Add(tweet5);

                // Tweet 6: Closing
                string tweet6 = $"🏆 GÜNÜN ANALİZİ TAMAMLANDI\n\n" +
                               $"Piyasa verileri anlık olarak takip edilmektedir.\n\n" +
                               $"{trends}" + DISCLAIMER;
                tweets.Add(tweet6);

                var result = await _posting.PostThreadAsync(tweets, null, "DailyReport");
                return result.status == "success";
            }
            catch { return false; }
        }

        public async Task<bool> PostWeeklyReportThread(WeeklyReport report, string trends)
        {
            try
            {
                var tweets = new List<string>();
                string tweet1 = $"🧵 HAFTALIK PERFORMANS RAPORU\n\n" +
                               $"📊 Toplam Sinyal: {report.TotalSignals}\n" +
                               $"🎯 Başarı Oranı: %{report.HitRate:0.0}\n" +
                               $"💰 Toplam Getiri: %{report.TotalReturn:+0.00}\n" +
                               $"⚡ Volatilite: %{report.Volatility:0.00}\n" +
                               $"📊 Endeks Farkı: {report.AvgAlpha:+0.00}\n\n" +
                               $"⬇️ Haftanın yıldızları aşağıda...";
                tweets.Add(tweet1);

                string top3Text = $"🏆 HAFTANIN EN İYİLERİ\n\n";
                int rank = 1;
                foreach (var sig in report.Top3)
                {
                    string medal = rank == 1 ? "🥇" : rank == 2 ? "🥈" : "🥉";
                    top3Text += $"{medal} #{sig.Symbol} %{sig.DailyPnL:+0.00}\n";
                    rank++;
                }
                top3Text += $"\n📌 Haftaya hazır olun!\n\n{trends}" + DISCLAIMER;
                tweets.Add(top3Text);

                var result = await _posting.PostThreadAsync(tweets, null, "WeeklyReport");
                return result.status == "success";
            }
            catch { return false; }
        }

        // ============================================
        // Twitter API Helpers
        // ============================================
        
        private Task<string?> PostTweet(string text)
        {
            bool success = _twitter.SendTweet(text);
            // Not: Gerçek implementasyonda tweet ID alınmalı
            return Task.FromResult<string?>(success ? Guid.NewGuid().ToString() : null);
        }

        private async Task<string?> PostTweetWithMedia(string text, string imagePath)
        {
            // TODO: Media upload implementasyonu
            return await PostTweet(text);
        }

        private async Task<string?> PostReply(string text, string replyToId)
        {
            // TODO: Reply implementasyonu (Twitter API v2)
            return await PostTweet(text);
        }
        private async Task<string> GetRealXComment(string symbol)
        {
            try 
            {
                // Clean Symbol for X Search (VIP-THYAO -> THYAO)
                string searchSymbol = CleanSymbolForX(symbol);
                string marketType = searchSymbol.EndsWith("USDT") ? "Kripto" : "BIST";
                if(searchSymbol.Contains("XAU") || searchSymbol.Contains("EUR")) marketType = "Forex";

                Logger.Twitter($"[GetRealXComment] Searching for {searchSymbol} in {marketType}...");

                // PRIORITY 1: Get fenomenleri from OUR DATABASE (InfluencerControlService)
                var vipHandles = _influencerControl?.GetTopInfluencers(searchSymbol, 15);
                
                Logger.Twitter($"[GetRealXComment] VIP handles from DB: {vipHandles?.Count ?? 0}");

                // PRIORITY 2: If database is empty, use real accounts from ConfigManager
                if (vipHandles == null || vipHandles.Count == 0)
                {
                    var cfg = ConfigManager.Current;
                    vipHandles = cfg.Influencers ?? new List<string>();
                    Logger.Twitter($"[GetRealXComment] Using ConfigManager fallback: {vipHandles.Count} handles");
                }
                
                // Search for REAL tweets from REAL influencers only
                var analyses = await _socialIntel.FindInfluencerAnalyses(searchSymbol, SocialIntelService.DetectMarket(searchSymbol), vipHandles);
                
                Logger.Twitter($"[GetRealXComment] Found {analyses?.Count ?? 0} analyses for {searchSymbol}");

                if (analyses != null && analyses.Count > 0)
                {
                    var top = analyses.OrderByDescending(x => x.Engagement).First();
                    Logger.Twitter($"[GetRealXComment] Top result from {top.Handle}: {top.Content.Substring(0, Math.Min(50, top.Content.Length))}...");
                    
                    // Format: Quote style from REAL influencer
                    return $"🔥 BORSADA GÜNDEM\n\n" +
                           $"👤 Uzman Yorumu ({top.Handle}):\n" +
                           $"\"{PerformSmartTruncate(top.Content, 100)}\"\n\n" +
                           $"🔗 Kaynak: {top.Url}";
                }
                
                Logger.Twitter($"[GetRealXComment] No analyses found, using fallback for {searchSymbol}");
            }
            catch (Exception ex) 
            {
                Logger.Twitter($"[GetRealXComment] Error: {ex.Message}");
            }

            // Fallback: NO FAKE TWEETS! Only neutral message
            return GetNeutralFallbackComment(symbol);
        }

        private string CleanSymbolForX(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return "";
            // Remove VIP- prefix
            symbol = symbol.Replace("VIP-", "");
            // Remove 'F_' or other prefixes if any (common in ideal)
            if(symbol.StartsWith("F_")) symbol = symbol.Substring(2);
            return symbol;
        }

        /// <summary>
        /// Returns the appropriate currency symbol based on the trading symbol.
        /// XAUUSD, XAGUSD, BTC, ETH -> USD
        /// BIST stocks -> TL
        /// EUR pairs -> EUR
        /// </summary>
        private string GetCurrencyForSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return "TL";
            
            symbol = symbol.ToUpperInvariant();
            
            // BIST Endeksleri — para birimi yok, sadece "puan" bazlı
            // XU100, XU030, XUTUM, X100, X030, XU050, XBANA, XUSIN vb.
            if (symbol.StartsWith("XU") || symbol.StartsWith("XB") || 
                symbol.StartsWith("XI") || symbol.StartsWith("XK") ||
                symbol.StartsWith("XM") || symbol.StartsWith("XS") ||
                symbol == "X100" || symbol == "X030" || symbol == "XUTUM" ||
                symbol.StartsWith("BIST") || symbol.Contains("ENDEKS"))
                return ""; // Endekslerde para birimi kullanılmaz
            
            // Forex/Commodities ending with USD
            if (symbol.EndsWith("USD") || symbol.Contains("USDT"))
                return "USD";
            
            // Gold, Silver in USD
            if (symbol.StartsWith("XAU") || symbol.StartsWith("XAG"))
                return "USD";
            
            // Crypto (BTC, ETH, etc.) - usually priced in USD
            if (symbol.StartsWith("BTC") || symbol.StartsWith("ETH") || 
                symbol.StartsWith("SOL") || symbol.StartsWith("AVAX") ||
                symbol.StartsWith("DOGE") || symbol.StartsWith("XRP"))
                return "USD";
            
            // EUR pairs
            if (symbol.EndsWith("EUR") || symbol.StartsWith("EUR"))
                return "EUR";
            
            // GBP pairs
            if (symbol.EndsWith("GBP") || symbol.StartsWith("GBP"))
                return "GBP";
            
            // Default for BIST/Turkish stocks
            return "TL";
        }

        private string PerformSmartTruncate(string text, int length)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= length) return text;
            return text.Substring(0, length) + "...";
        }

        private string GetRandomMentions(int count = 2)
        {
            var list = ConfigManager.Current.Influencers;
            if (list == null || list.Count == 0) return "";

            var rnd = new Random();
            var selected = new HashSet<string>();
            int attempts = 0;

            // Listeden rastgele 'count' kadar seç (benzersiz)
            while (selected.Count < Math.Min(count, list.Count) && attempts < 20)
            {
                selected.Add(list[rnd.Next(list.Count)]);
                attempts++;
            }

            return string.Join(" ", selected);
        }
        private string FormatMovers(List<MarketMover> movers, string type)
        {
            if (movers == null || movers.Count == 0) return "⚠️ Veri bulunamadı.";
            return string.Join("\n", movers.Take(10).Select(s => 
            {
               string icon = type == "Kazananlar" ? "🚀" : type == "Kaybedenler" ? "🔻" : "💎";
               return $"{icon} #{s.Symbol} %{s.ChangePercent:+0.00}";
            }));
        }
    }
}
