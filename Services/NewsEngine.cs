// NEWS_ENGINE_VERSION: 1.0
// PURPOSE: Central hub for AI-driven news processing, filtering, and summarization.
// Decouples news intelligence from the UI and NewsTrackerService.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class NewsEngine
    {
        private readonly GeminiService _gemini;
        private readonly TwitterService _twitter;
        private readonly SocialIntelService _socialIntel;
        private readonly TelegramService _telegram;
        private readonly SpamProtection _spam;
        private readonly PromptManager _prompts;
        private readonly StatsEngine _stats;
        private readonly NewsPersistenceService _persistence;
        private readonly PostingService _posting;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(2, 2); // Max 2 eşzamanlı haber işlemi (VRAM güvenliği)
        private readonly ConcurrentDictionary<string, DateTime> _inflightNews = new(StringComparer.OrdinalIgnoreCase);
        
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; set; }

        public NewsEngine(GeminiService gemini, TwitterService twitter, SocialIntelService socialIntel, TelegramService telegram, SpamProtection spam, PromptManager prompts, StatsEngine stats, NewsPersistenceService persistence, PostingService? posting = null)
        {
            _gemini = gemini;
            _twitter = twitter;
            _socialIntel = socialIntel;
            _telegram = telegram;
            _spam = spam;
            _prompts = prompts;
            _stats = stats;
            _persistence = persistence;
            _posting = posting ?? new PostingService(socialIntel, stats);
        }

        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;
        public event Action<NewsItem, string, int, string, string, bool>? OnNewsPendingApproval; // Item, Summary, Score, Category, Reasoning, IncludesAnalysis
        public event Action<NewsItem, string>? OnNewsRejected;
        public event Action<NewsItem, string, int, string>? OnNewsProcessed; // News, Summary, Importance, Category

        public List<NewsHistoryItem> GetRecentHighImpactNews(int count = 3)
        {
             return _persistence.GetRecentImportantNews(count);
        }

        public async Task ProcessNews(NewsItem item)
        {
            await _semaphore.WaitAsync();
            string newsKey = BuildNewsKey(item);
            try
            {
                CleanupInflightNews();
                if (!_inflightNews.TryAdd(newsKey, DateTime.UtcNow))
                {
                    OnLog?.Invoke($"⏭️ Haber atlandı (aynı haber zaten işleniyor): {item.Title}", "NewsEngine");
                    return;
                }

                _stats.RecordActivity("NewsEngine", $"Processing news: {item.Title}", true, item.Source);
                
                // v3.6.4: Safety Recency Check (Max 48h - Extended for Weekends)
                if (item.PubDate < DateTime.Now.AddHours(-48))
                {
                    OnLog?.Invoke($"⏭️ Haber atlandı (Haber çok eski [>48h]: {item.PubDate:g})", "NewsEngine");
                    return;
                }
                
                // v2.1: Pre-Filtering (Noise Reduction)
                if (item.Title.Length < 10 || item.Title.Split(' ').Length < 3) return;

                if (_persistence.IsDuplicate(item.Title, item.Source, out string dupReason))
                {
                     OnLog?.Invoke($"⏭️ Haber atlandı ({dupReason})", "NewsEngine");
                     return;
                }

                if (!string.IsNullOrWhiteSpace(item.Link) && _persistence.IsKnownUrl(item.Link, out string urlReason))
                {
                    OnLog?.Invoke($"⏭️ Haber atlandı ({urlReason}): {item.Title}", "NewsEngine");
                    return;
                }

                // v5.0: Multi-Layer Filtering (Regex/Keyword -> Lightweight NLP)
                if (!PerformLightweightNLPFilter(item.Title, item.Description))
                {
                    OnLog?.Invoke($"🛡️ Haber filtrelendi (Hafif NLP/Regex Reddi): {item.Title}", "NewsEngine");
                    return;
                }

                OnLog?.Invoke($"🔍 [NewsEngine] Haber analiz ediliyor: {item.Title}", "News");

                // v2.2: Advanced Noise Filter (Quota Shield)
                bool isHighValue = IsHighValueNews(item.Title);
                if (!isHighValue)
                {
                    OnLog?.Invoke($"🛡️ Haber filtrelendi (Düşük Önem): {item.Title}", "NewsEngine");
                    return;
                }
                
                OnLog?.Invoke($"📰 Yeni Haber Analiz Ediliyor: {item.Title}", "NewsEngine");
                OnStatusUpdate?.Invoke($"{item.Title} analiz ediliyor...");

                // v4.10.3: AI çağrısından önce gecikme — yalnızca filtreyi geçen haberler bekler (Gemini kota koruması)
                await Task.Delay(5000);

                // v5.1.1: Unified single-call analysis (1 LM request instead of 2 serial)
                string? analysisRaw = await _gemini.AnalyzeNewsUnified(item.Title, item.Source);
                
                if (string.IsNullOrEmpty(analysisRaw))
                {
                    OnLog?.Invoke("⚠️ AI analiz yapamadı (Boş yanıt).", "NewsEngine");
                    return;
                }

                // 2. Parse Structured Output (1-10 scale)
                var analysisData = ParseAnalysisData(analysisRaw);
                int score = analysisData.Confidence;
                string status = analysisData.Status;
                string summary = analysisData.Summary;
                string symbols = analysisData.Symbols;
                string category = analysisData.Category;
                item.Category = category;

                // v4.7.3: Son Dakika skor boost kaldırıldı.
                // AI'a zaten "son dakika haberlere max 9" kuralı verildi, çelişkiyi yönettik.

                // v4.7.3: lowerTitle Fenerbahçe kontrolü için tanımlandı
                string lowerTitle = item.Title.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

                // v4.2.2: FENERBAHÇE BYPASS (Fanatik Modu) - Güncellendi 1-10 skalası
                if (lowerTitle.Contains("fenerbahçe") || lowerTitle.Contains("fenerbahce") || lowerTitle.Contains("sarı lacivert") || lowerTitle.Contains("ali koç") || lowerTitle.Contains("jose mourinho"))
                {
                    if (score < 7)
                    {
                        score = 7; // Force Pending (news only)
                        status = "PENDING_NEWS_ONLY";
                        analysisData = (score, status, summary, symbols, category, "💛💙 FAN ZONE: Fenerbahçe haberi, skor boost uygulandı.");
                        OnLog?.Invoke($"💛💙 Fenerbahçe Haberi Korundu: {item.Title}", "NewsEngine");
                    }
                }

                bool isBreaking = IsBreakingNews(item);

                // v4.8.0: ANTI-HALLUCINATION / SAFETY GUARDRAIL
                // Felaket, deprem, afet gibi konularda AI'ın otomatik 10/10 puan vermesini ezer.
                string[] riskKeywords = { "deprem", "sel ", " yangın", "felaket", "tatbikat", "terör", "şehit", "cinayet", "operasyon", "patlama", "saldırı", "bombalı", " kaza ", "can kaybı" };
                bool hasRisk = riskKeywords.Any(k => lowerTitle.Contains(k) || (item.Description ?? "").ToLower().Contains(k));
                if (hasRisk && score >= 9 && !isBreaking) // breaking/çok kritik haberler hariç hassas içerik onaya düşer
                {
                    OnLog?.Invoke($"🚨 GÜVENLİK FİLTRESİ: Hassas/Afet içerik tespit edildi. Otomatik paylaşım iptal, Onay havuzuna alındı. (Orijinal Skor: {score}/10)", "NewsEngine");
                    score = Math.Min(score, 9);
                    status = "PENDING_WITH_ANALYSIS";
                }

                // 3. Action Decision based on Score (1-10 Scale)
                // 7-8: SKIP/REVIEW history only
                // 9: high score but no Telegram approval spam; history only
                // 10 or breaking 9+: AUTO_POST_WITH_ANALYSIS

                if (score < 7 || status == "REJECT")
                {
                    string finalReason = status == "REJECT" ? $"AI Red Kararı: {analysisData.Reasoning}" : $"Skor çok düşük ({score}/10)";
                    OnLog?.Invoke($"🗑️ Haber Reddedildi (Skor: {score}/10): {item.Title}", "NewsEngine");
                    OnNewsRejected?.Invoke(item, finalReason);
                    return;
                }

                if (status == "AUTO_POST_WITH_ANALYSIS" || score >= 10 || (isBreaking && score >= 9))
                {
                    item.IncludesAnalysis = true;
                    // OTOMATİK PAYLAŞ - HABER + ANALİZ
                    OnLog?.Invoke($"🔥 KRİTİK HABER (Skor: {score}/10): {item.Title}", "NewsEngine");
                    
                    // Kategoriye özel analiz thread'i üret (description + isFlash ile zenginleştirilmiş)
                    string? analysisThread = await _gemini.GenerateNewsCategoryAnalysis(
                        category, item.Title, item.Source, item.Link,
                        description: item.Description,
                        isFlash: item.IsFlash);
                    
                    // v5.1.1: Pre-built analysisThread geçiliyor → PostNewsThreadToTwitter içindeki
                    // AnalyzeNewsForThread çağrısı atlanacak (duplicate 3. LM çağrısı önlendi)
                    var (success, msg) = await PostNewsThreadToTwitter(item, summary, symbols, isCritical: true, score: score, category: category, preBuiltThread: analysisThread);
                    if (!success)
                    {
                        OnLog?.Invoke($"⚠️ Otomatik paylaşım başarısız: {msg}", "NewsEngine");
                    }
                    return;
                }

                if (status == "PENDING_WITH_ANALYSIS" || score == 9)
                {
                    item.IncludesAnalysis = false;
                    OnLog?.Invoke($"🧹 Haber yüksek skor ama Telegram onayına düşmedi (Skor: {score}/10): {item.Title}", "NewsEngine");
                    _persistence.AddParsedNews(item.Title, item.Source, item.Link, score, false, "SKIPPED_REVIEW");
                    return;
                }

                if (status == "PENDING_NEWS_ONLY" || (score >= 7 && score <= 8))
                {
                    OnLog?.Invoke($"🧹 Haber düşük/orta önemde, onay kuyruğuna alınmadı (Skor: {score}/10): {item.Title}", "NewsEngine");
                    _persistence.AddParsedNews(item.Title, item.Source, item.Link, score, false, "SKIPPED_LOW_SCORE");
                    return;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ NewsEngine Error: {ex.Message}", "System");
            }
            finally
            {
                _inflightNews.TryRemove(newsKey, out _);
                _semaphore.Release();
            }
        }

        private static string BuildNewsKey(NewsItem item)
        {
            string link = (item.Link ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(link))
            {
                int qIdx = link.IndexOf('?');
                if (qIdx >= 0) link = link.Substring(0, qIdx);
                return "url:" + link.TrimEnd('/');
            }

            string title = System.Text.RegularExpressions.Regex.Replace(item.Title ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
            return $"title:{item.Source}:{title}";
        }

        private static bool IsBreakingNews(NewsItem item)
        {
            string text = $"{item.Title} {item.Description}".ToLowerInvariant();
            if (item.IsFlash) return true;
            string[] markers =
            {
                "son dakika", "breaking", "flash", "acil", "sıcak gelişme", "sicak gelisme",
                "füze saldırısı", "fuze saldirisi", "savaş başladı", "savas basladi",
                "ateşkes", "ateskes", "merkez bankası faiz", "tcmb faiz", "fed faiz",
                "devre kesti", "borsa kapandı", "piyasa kapandı"
            };
            return markers.Any(m => text.Contains(m));
        }

        private void CleanupInflightNews()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var kv in _inflightNews.Where(x => x.Value < cutoff).ToList())
                _inflightNews.TryRemove(kv.Key, out _);
        }

        public async Task<(bool success, string message)> ForcePostNews(NewsItem item, string summary, string? category = null, bool includeAnalysis = true)
        {
            OnLog?.Invoke($"👤 Kullanıcı Onayladı: {item.Title}", "NewsEngine");
            category = string.IsNullOrWhiteSpace(category) ? item.Category : category;
            if (string.IsNullOrWhiteSpace(category)) category = "EKONOMI";

            string? analysisThread = includeAnalysis
                ? await _gemini.GenerateNewsCategoryAnalysis(
                    category, item.Title, item.Source, item.Link,
                    description: item.Description, isFlash: item.IsFlash)
                : BuildMinimalNewsTweet(item, summary);
            // v5.1.1: preBuiltThread geçiliyor → içeride duplicate AnalyzeNewsForThread çağrısı atlanır
            return await PostNewsThreadToTwitter(item, summary, "BIST100", isCritical: true, score: 8, category: category, preBuiltThread: analysisThread);
        }

        private async Task<(bool success, string message)> PostNewsThreadToTwitter(NewsItem item, string summary, string symbols, bool isCritical = false, int score = 10, string category = "EKONOMI", string? preBuiltThread = null)
        {
            // Config: SpamProtectNews Check
            if (ConfigManager.Current.SpamProtectNews)
            {
                if (!_spam.CanPostGeneral(out string reason, isCritical: isCritical))
                {
                    OnLog?.Invoke($"🛡️ Spam koruması: {reason}", "NewsEngine");
                    return (false, $"Spam koruması: {reason}");
                }
            }

            // v5.1.1: preBuiltThread varsa AnalyzeNewsForThread çağrısını atla (duplicate LM çağrısı önlenir)
            string? threadContent = preBuiltThread;
            if (string.IsNullOrEmpty(threadContent))
            {
                // Generate Thread Content (description + isFlash ile zenginleştirilmiş)
                threadContent = await _gemini.AnalyzeNewsForThread(
                    item.Title, item.Source, summary, item.Link,
                    description: item.Description,
                    isFlash: item.IsFlash);
            }
            
            // v3.0: İçerik Kalite Kontrolü (sadece içerik varsa — null/boş durumda fallback'e geç)
            if (!string.IsNullOrEmpty(threadContent) && ContentQualityGuard.IsSpamOrLowQuality(threadContent, out string spamReason))
            {
                OnLog?.Invoke($"⚠️ Thread içeriği SPAM/KALİTESİZ ({spamReason}), atlanıyor.", "NewsEngine");
                return (false, $"Thread içeriği kalitesiz: {spamReason}");
            }

            List<string> normalizedParts;
            if (string.IsNullOrEmpty(threadContent) || threadContent.Contains("NO_IMPACT"))
            {
                // v5.1.3: Analiz üretilemese bile garantili minimum tweet: başlık + link
                // Flash/kritik haberler sessizce iptal edilmemeli
                OnLog?.Invoke("⚠️ AI thread içeriği üretemedi — minimum haber tweet'i gönderiliyor.", "NewsEngine");
                string minTweet = BuildMinimalNewsTweet(item, summary);
                normalizedParts = new List<string> { minTweet };
            }
            else
            {
                normalizedParts = ThreadPipeline.BuildNewsThread(item, threadContent).Take(3).ToList();
                if (normalizedParts.Count > 0 && !normalizedParts[^1].Contains("Yatırım tavsiyesi", StringComparison.OrdinalIgnoreCase) && !normalizedParts[^1].Contains("haber özetidir", StringComparison.OrdinalIgnoreCase))
                {
                    const string suffix = "\n\n⚠️ Haber özetidir, yatırım tavsiyesi değildir.";
                    var baseText = normalizedParts[^1].Trim();
                    if (baseText.Length + suffix.Length > 280) baseText = baseText.Substring(0, Math.Max(0, 277 - suffix.Length)).TrimEnd() + "...";
                    normalizedParts[^1] = baseText + suffix;
                }
            }
            if (normalizedParts.Count > 1 && threadContent != null && !threadContent.Contains("|||"))
            {
                OnLog?.Invoke("⚠️ AI thread separatoru (|||) kullanmadı. İçerik otomatik bölünüyor...", "NewsEngine");
            }

            


            // v4.6.18: Visual Hook - Haber görselini indir ve thread'e ekle
            string? localImagePath = null;
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    var imgBytes = await httpClient.GetByteArrayAsync(item.ImageUrl);
                    string ext = item.ImageUrl.Contains(".png") ? ".png" : ".jpg";
                    localImagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhive_news_{Guid.NewGuid()}{ext}");
                    await System.IO.File.WriteAllBytesAsync(localImagePath, imgBytes);
                    OnLog?.Invoke($"🖼️ Haber görseli indirildi: {localImagePath}", "NewsEngine");
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"⚠️ Görsel indirilemedi: {ex.Message}", "NewsEngine");
                    localImagePath = null;
                }
            }

            // v4.7.3: Görsel yoksa XiDeAI logolu dinamik kart oluştur
            if (string.IsNullOrEmpty(localImagePath))
            {
                try
                {
                    string cardPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhive_card_{Guid.NewGuid()}.png");
                    string scriptDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Scripts");
                    string generatorScript = System.IO.Path.Combine(scriptDir, "news_card_generator.py");
                    string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "xideai_icon.ico");
                    
                    if (System.IO.File.Exists(generatorScript))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "python",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        psi.ArgumentList.Add(generatorScript);
                        psi.ArgumentList.Add("--title");
                        psi.ArgumentList.Add(item.Title ?? string.Empty);
                        psi.ArgumentList.Add("--output");
                        psi.ArgumentList.Add(cardPath);
                        psi.ArgumentList.Add("--source");
                        psi.ArgumentList.Add(item.Source ?? string.Empty);
                        if (item.IsFlash) psi.ArgumentList.Add("--flash");
                        if (System.IO.File.Exists(iconPath))
                        {
                            psi.ArgumentList.Add("--logo");
                            psi.ArgumentList.Add(iconPath);
                        }
                        
                        using var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                        {
                            await proc.WaitForExitAsync();
                            if (proc.ExitCode == 0 && System.IO.File.Exists(cardPath))
                            {
                                localImagePath = cardPath;
                                OnLog?.Invoke($"🎨 Haber kartı oluşturuldu ({(item.IsFlash ? "FLAŞ" : "ÖNEMLİ")}): {cardPath}", "NewsEngine");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"⚠️ Haber kartı oluşturulamadı: {ex.Message}", "NewsEngine");
                }
            }


            var result = await _posting.PostThreadAsync(normalizedParts, localImagePath, "NewsEngine");

            // Cleanup temp image
            if (!string.IsNullOrEmpty(localImagePath) && System.IO.File.Exists(localImagePath))
            {
                try { System.IO.File.Delete(localImagePath); } catch { }
            }
            
            if (result.status == "success")
            {
                _stats.RecordActivity("NewsEngine", $"Posted CRITICAL NEWS: {item.Title}", true);
                _spam.RecordTweet("NEWS_THREAD", symbols);
                OnLog?.Invoke($"✅ Haber Paylaşıldı: {item.Title}", "Twitter");
                
                // Kalıcı hafızaya PUBLISHED olarak kaydet (gerçek skor ve kategori ile)
                _persistence.AddParsedNews(item.Title ?? string.Empty, item.Source ?? string.Empty, item.Link ?? string.Empty, score, true, "PUBLISHED");
                
                // Event tetikle (UI güncellemesi için)
                OnNewsProcessed?.Invoke(item, summary, score, category);
                
                return (true, "Haber başarıyla paylaşıldı!");
            }
            else
            {
                OnLog?.Invoke($"❌ Paylaşım Başarısız: {result.message}", "Twitter");
                // v4.6.15: Başarısız paylaşımda spam kotası HARCANMAZ
                return (false, $"Twitter hatası: {result.message}");
            }
        }


        private (int Confidence, string Status, string Summary, string Symbols, string Category, string Reasoning) ParseAnalysisData(string raw)
        {
            int confidence = 0;
            string status = "REJECT";
            string summary = "";
            string symbols = "BIST100";
            string category = "EKONOMI";
            string reasoning = "";

            try
            {
                var lines = raw.Split('\n');
                foreach (var line in lines)
                {
                    // v3.6.5: Robust parsing (strip markdown stars and be case-insensitive)
                    string cleanLine = line.Replace("**", "").Trim();
                    
                    if (cleanLine.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase)) 
                        int.TryParse(cleanLine.Substring("CONFIDENCE:".Length).Trim(), out confidence);
                    else if (cleanLine.StartsWith("STATUS:", StringComparison.OrdinalIgnoreCase)) 
                        status = cleanLine.Substring("STATUS:".Length).Trim().ToUpper().Replace(" ", "_");
                    else if (cleanLine.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase)) 
                        summary = cleanLine.Substring("SUMMARY:".Length).Trim();
                    else if (cleanLine.StartsWith("SYMBOLS:", StringComparison.OrdinalIgnoreCase)) 
                        symbols = cleanLine.Substring("SYMBOLS:".Length).Trim();
                    else if (cleanLine.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase)) 
                        category = cleanLine.Substring("CATEGORY:".Length).Trim().ToUpper();
                    else if (cleanLine.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase)) 
                        reasoning = cleanLine.Substring("REASONING:".Length).Trim();
                }
            }
            catch { }

            // v4.2.2: Fallback for old 1-5 scale responses (multiply by 2)
            if (confidence > 0 && confidence <= 5 && raw.Contains("CONFIDENCE:"))
            {
                confidence = confidence * 2; // Scale 1-5 to 2-10
            }

            return (confidence, status, summary, symbols, category, reasoning);
        }


        // v5.1.3: AI analiz üretemediğinde gönderilecek garantili minimum tweet
        private static string BuildMinimalNewsTweet(NewsItem item, string summary)
        {
            string title = item.Title?.Trim() ?? "Haber";
            if (title.Length > 140) title = title.Substring(0, 140).TrimEnd() + "...";
            string prefix = item.IsFlash ? "🚨 SON DAKİKA" : "📰 HABER";
            string descPart = !string.IsNullOrWhiteSpace(summary) && summary.Length > 10
                ? $"\n\n{(summary.Length > 160 ? summary.Substring(0, 160).TrimEnd() + "..." : summary)}"
                : "";
            string linkPart = !string.IsNullOrEmpty(item.Link) ? $"\n\n🔗 {item.Link}" : "";
            return $"{prefix}\n\n{title}{descPart}{linkPart}\n\nKaynak: {item.Source}";
        }

        private bool PerformLightweightNLPFilter(string title, string? description)
        {
            string text = (title + " " + (description ?? "")).ToLower();

            // 1. Regex/Keyword: Rapidly discard clearly irrelevant or spammy patterns
            // Discarding common non-news/spam patterns found in RSS feeds
            var spamPatterns = new[] { @"\b(magazin|şok iddia|tıkla|izle|video)\b", @"\b(reklam|sponsorlu)\b" };
            foreach (var pattern in spamPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern)) return false;
            }

            // 2. Lightweight NLP: Simple importance/sentiment indicators
            // If the title is just a list of names or very short fragments without verbs, it's likely noise.
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 4) return false;

            // Check for "action" indicators in news (verbs/notions related to movement/change)
            bool hasAction = words.Any(w =>
                // Türkçe eylem/hareket kelimeleri
                w.Contains("arttı") || w.Contains("düştü") || w.Contains("açıkladı") ||
                w.Contains("belirledi") || w.Contains("yaptı") || w.Contains("başladı") ||
                w.Contains("karar") || w.Contains("plan") || w.Contains("imza") ||
                w.Contains("rekor") || w.Contains("çöküş") || w.Contains("patlama") ||
                w.Contains("yükseliş") || w.Contains("düşüş") || w.Contains("kritik") ||
                // İngilizce eylem kelimeleri (BBC, WSJ, CNBC, TASS, Xinhua haberleri için)
                w.Contains("raises") || w.Contains("cuts") || w.Contains("hikes") ||
                w.Contains("falls") || w.Contains("rises") || w.Contains("drops") ||
                w.Contains("surges") || w.Contains("crashes") || w.Contains("warns") ||
                w.Contains("announces") || w.Contains("approves") || w.Contains("bans") ||
                w.Contains("record") || w.Contains("crisis") || w.Contains("critical") ||
                w.Contains("emergency") || w.Contains("sanctions") || w.Contains("rate") ||
                w.Contains("attack") || w.Contains("strike") || w.Contains("collapse"));
            
            // If it's a very "dry" title without any action/event indicator, we treat it as low priority for AI processing
            if (!hasAction && words.Length < 8) return false;

            return true;
        }

        private bool IsHighValueNews(string title)
        {
            // 0. DEBUG / TEST MODE
            if (ConfigManager.Current.NewsTestMode)
            {
                OnLog?.Invoke($"🧪 Test Modu Aktif: '{title}' filtresiz geçiriliyor.", "NewsEngine");
                return true;
            }

            var lower = title.ToLower();

            // 1. Türkiye Ekonomisine Direkt Etki Kontrolü (Core Filter - Expanded v3.8.2)
            string[] trEconomyKeywords = {
                // Kurumlar
                "bist", "borsaistanbul", "borsa", "kap", "merkez bankası", "tcmb", "tbmm", "hazine", "spk", "bddk", "sgk",
                // Makro
                "enflasyon", "dolar", "lira", "tl", "faiz", "gsyh", "büyüme", "cari açık", "dış ticaret", "ihracat", "ithacat", "resesyon",
                // Şirket/Finans
                "temettü", "sermaye", "bilanço", "kar payı", "halka arz", "pay alım", "pay satım", "geri alım", "bedelli", "bedelsiz",
                "hisse", "kredi", "finansman", "borç", "yapılandırma", "konkordato", "satın alma", "birleşme", "yatırım",
                // Sektörler
                "banka", "sigorta", "holding", "enerji", "otomotiv", "havacılık", "savunma", "teknoloji", "gyo", "gayrimenkul",
                "petrol", "doğalgaz", "elektrik", "turizm", "perakende"
            };

            // v4.7.3: nonTrKeywords listesi güncellendi - global ajans kaynaklı haberler (TASS, BBC, WSJ, Xinhua)
            // bloklanmamalı. Sadece gerçekten alakasız forex/deriştirme haberleri reddediliyor.
            string[] nonTrKeywords = {
                "nasdaq", "s&p 500", "dow jones", "dax", "cac 40",
                "forex majors", "dolar euro", "gbp/usd", "eur/usd", "chf/jpy",
                "cricket", "baseball", "nfl", "nba stats" // Sporu alakasız kategoriler
            };

            // v3.6.5: Allow high-impact Global Tech News as they affect BIST technology sector
            string[] globalImpactTech = { "apple", "nvidia", "tesla", "microsoft", "google", "fed", "openai", "chatgpt", "btc", "bitcoin", "kripto" };
            if (globalImpactTech.Any(k => lower.Contains(k)) && (lower.Contains("kritik") || lower.Contains("rekor") || lower.Contains("yeni") || lower.Contains("faiz") || lower.Contains("karar")))
            {
                return true; 
            }

            // v4.6.18: Allow high-impact Geopolitical News that directly affect Turkey/markets
            string[] geopoliticalKeywords = {
                "türkiye", "turkey", "türk", "ankara", "istanbul",
                "füze", "saldırı", "savaş", "çatışma", "askeri", "operasyon", "nato", "savunma",
                "msb", "milli savunma", "hava sahası", "balistik", "yaptırım", "ambargo",
                "iran", "rusya", "ukrayna", "suriye", "irak", "suudi arabistan", "körfez",
                "petrol", "doğalgaz", "enerji koridoru", "doğu akdeniz", "kanal istanbul",
                "faiz kararı", "fed kararı", "merkez bankası faiz", "rezerv", "döviz müdahale"
            };
            if (geopoliticalKeywords.Any(k => lower.Contains(k)))
            {
                OnLog?.Invoke($"🌍 Jeopolitik/Kritik haber geçti: {title}", "NewsEngine");
                return true;
            }

            // 2. Non-Turkey Check
            if (nonTrKeywords.Any(k => lower.Contains(k)))
            {
                // Kurtarıcı kelimeler (Yurt dışı haberi ama Türkiye'yi ilgilendiriyor olabilir)
                bool hasTurkey = lower.Contains("turkey") || lower.Contains("türkiye") || lower.Contains("türk") || lower.Contains("istanbul") || lower.Contains("etkisi");
                if (!hasTurkey) return false;
            }

            // 3. Zorunlu Geçiş (Kritik konular)
            if (trEconomyKeywords.Any(k => lower.Contains(k)))
            {
                return true;
            }

            // 4. Gürültü Filtresi (Spam ve Magazin)
            string[] junkKeywords = { 
                "galatasaray", "beşiktaş", "trabzonspor", "süper lig", "transfer", "futbol", "oyuncu", 
                "magazin", "ünlü oyuncu", "şok iddia", "günaydın", "hayırlı cumalar", "iyi akşamlar",
                "burcu", "astroloji", "fal", "reklam", "dış haberleri", "sosyal medya", "tiktok", "instagram",
                "okullar tatil", "kar tatili", "eğitime ara", "hava durumu", "meteoroloji", "fırtına", "yağış",
                "şampiyon oldu", "kupa finali", "gol attı", "personel alımı", "sınav sonuçları", "kpss", "ales"
            };
            if (junkKeywords.Any(k => lower.Contains(k)))
            {
                // EXCEPTION: Fenerbahçe finansal veya çok kritik haberleri (Ali Koç açıklaması vb. kalsın)
                bool isFenerbahce = lower.Contains("fenerbahçe") || lower.Contains("fenerbahce") || lower.Contains("ali koç");
                if (isFenerbahce && (lower.Contains("borsa") || lower.Contains("hisse") || lower.Contains("kap") || lower.Contains("sermaye")))
                {
                    return true;
                }
                return false;
            }

            // 5. Mikro Hisse Hareketleri (Quota Shield)
            // ÇOK KATIDAN ESNEĞE: "00.000" şartını kaldırdım. "Milyon/Milyar" varsa kalsın.
            if (lower.Contains("dolarlık hisse") || lower.Contains("lot hisse"))
            {
                bool isLargeAmount = lower.Contains("milyon") || lower.Contains("milyar") || lower.Contains("yüklü");
                if (!isLargeAmount) return false;
            }

            // 6. Rutin Analist Notları
            string[] analystJunk = { "fiyat hedefini", "tavsiyesini yineledi", "tavsiye bildirdi", "görünüm nedeniyle al", "görünüm nedeniyle sat" };
            if (analystJunk.Any(k => lower.Contains(k)))
            {
                 // Büyük kurumlar kalsın
                 string[] majorBanks = { "goldman", "morgan stanley", "jpmorgan", "tcmb", "msci", "fitch", "moody", "s&p", "merrill", "hsbc", "ak yatırım", "iş yatırım", "yapı kredi" };
                 if (!majorBanks.Any(b => lower.Contains(b))) return false;
            }

            // 7. Diğer Gürültü Kriterleri
            if (title.Count(c => c == '#') > 5) return false;

            // 8. Default: Reject if no TR keyword match
             OnLog?.Invoke($"🛡️ Filtreye Takıldı (Keyword Yok): {title}", "NewsEngine");
            return false;
        }
    }
}
