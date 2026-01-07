// NEWS_ENGINE_VERSION: 1.0
// PURPOSE: Central hub for AI-driven news processing, filtering, and summarization.
// Decouples news intelligence from the UI and NewsTrackerService.

using System;
using System.Collections.Generic;
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
        private readonly object _lock = new object();

        public NewsEngine(GeminiService gemini, TwitterService twitter, SocialIntelService socialIntel, TelegramService telegram, SpamProtection spam, PromptManager prompts, StatsEngine stats, NewsPersistenceService persistence)
        {
            _gemini = gemini;
            _twitter = twitter;
            _socialIntel = socialIntel;
            _telegram = telegram;
            _spam = spam;
            _prompts = prompts;
            _stats = stats;
            _persistence = persistence;
        }

        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;
        public event Action<NewsItem, string, int, string>? OnNewsPendingApproval; // Item, Summary, Score, Reasoning
        public event Action<NewsItem, string>? OnNewsRejected;
        public event Action<NewsItem, string, int>? OnNewsProcessed; // News, Summary, Importance

        public async Task ProcessNews(NewsItem item)
        {
            try
            {
                OnLog?.Invoke($"🔍 [NewsEngine] Haber analiz ediliyor: {item.Title}", "News");
                _stats.RecordActivity("NewsEngine", $"Processing news: {item.Title}", true, item.Source);
                
                // v3.6.4: Safety Recency Check (Max 24h)
                if (item.PubDate < DateTime.Now.AddHours(-24))
                {
                    OnLog?.Invoke($"⏭️ Haber atlandı (Haber çok eski: {item.PubDate:g})", "NewsEngine");
                    return;
                }
                
                // v2.1: Pre-Filtering (Noise Reduction)
                if (item.Title.Length < 10 || item.Title.Split(' ').Length < 3) return;
                
                // v2.3: Deduplication (Title similarity + Persistence)
                if (_persistence.IsDuplicate(item.Title, item.Source, out string dupReason))
                {
                     OnLog?.Invoke($"⏭️ Haber atlandı ({dupReason})", "NewsEngine");
                     return;
                }

                // v2.2: Advanced Noise Filter (Quota Shield)
                bool isHighValue = IsHighValueNews(item.Title);
                if (!isHighValue)
                {
                    OnLog?.Invoke($"🛡️ Haber filtrelendi (Düşük Önem): {item.Title}", "NewsEngine");
                    return;
                }
                
                OnLog?.Invoke($"📰 Yeni Haber Analiz Ediliyor: {item.Title}", "NewsEngine");
                OnStatusUpdate?.Invoke($"{item.Title} analiz ediliyor...");

                // 1. AI Editor Analysis (New Prompt)
                string? analysisRaw = await _gemini.AnalyzeNewsImpact(item.Title, item.Source);
                
                if (string.IsNullOrEmpty(analysisRaw))
                {
                    OnLog?.Invoke("⚠️ AI analiz yapamadı (Boş yanıt).", "NewsEngine");
                    return;
                }

                // 2. Parse Structured Output
                // Format: CONFIDENCE: X, STATUS: Y, SUMMARY: Z
                var analysisData = ParseAnalysisData(analysisRaw);
                int score = analysisData.Confidence;
                string status = analysisData.Status;
                string summary = analysisData.Summary;
                string symbols = analysisData.Symbols;

                // 3. Action Decision based on Score & Status
                if (score <= 2 || status == "REJECT")
                {
                    string finalReason = status == "REJECT" ? $"AI Red Kararı: {analysisData.Reasoning}" : "Skor çok düşük";
                    OnLog?.Invoke($"🗑️ Haber Reddedildi (Skor: {score}): {item.Title} | Gerekçe: {finalReason}", "NewsEngine");
                    OnNewsRejected?.Invoke(item, finalReason);
                    return;
                }

                if (status == "PENDING" || (score >= 3 && score <= 4))
                {
                    // ONAY GEREKTİRİYOR
                    OnLog?.Invoke($"⚠️ Haber Onaya Düştü (Skor: {score}): {item.Title}", "NewsEngine");
                    
                    // Kalıcı hafızaya PENDING olarak kaydet
                    _persistence.AddParsedNews(item.Title, item.Source, item.Link, score, false); // false = not tweeted yet
                    // TODO: Update persistence to actually store 'PENDING' status explicitly if needed, currently bool WasTweeted=false implies pending/failed.
                    
                    OnNewsPendingApproval?.Invoke(item, summary, score, analysisData.Reasoning);
                    return;
                }

                if (score == 5 || status == "AUTO_POST")
                {
                    // OTOMATİK PAYLAŞ
                     OnLog?.Invoke($"🔥 KRİTİK HABER (Skor: 5): {item.Title}", "NewsEngine");
                     await PostNewsThreadToTwitter(item, summary, symbols);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ NewsEngine Error: {ex.Message}", "System");
            }
        }

        public async Task ForcePostNews(NewsItem item, string summary)
        {
            OnLog?.Invoke($"👤 Kullanıcı Onayladı: {item.Title}", "NewsEngine");
            await PostNewsThreadToTwitter(item, summary, "BIST100");
        }

        private async Task PostNewsThreadToTwitter(NewsItem item, string summary, string symbols)
        {
            // Config: SpamProtectNews Check
            if (ConfigManager.Current.SpamProtectNews)
            {
                if (!_spam.CanPostGeneral(out string reason)) return;
            }

            // Generate Thread Content
            string? threadContent = await _gemini.AnalyzeNewsForThread(item.Title, item.Source, item.Link);
            
            // v3.0: İçerik Kalite Kontrolü
            if (ContentQualityGuard.IsSpamOrLowQuality(threadContent ?? "", out string spamReason))
            {
                OnLog?.Invoke($"⚠️ Thread içeriği SPAM/KALİTESİZ ({spamReason}), atlanıyor.", "NewsEngine");
                return;
            }

            if (string.IsNullOrEmpty(threadContent) || threadContent.Contains("NO_IMPACT"))
            {
                OnLog?.Invoke("⚠️ AI thread içeriği üretemedi, işlem iptal.", "NewsEngine");
                return;
            }

            // Post as Thread
            string[] parts = threadContent.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
            var result = await _socialIntel.PostThreadAsync(parts.ToList()); 
            
            if (result.status == "success")
            {
                _stats.RecordActivity("NewsEngine", $"Posted CRITICAL NEWS: {item.Title}", true);
                _spam.RecordTweet("NEWS_THREAD", symbols);
                OnLog?.Invoke($"✅ Haber Paylaşıldı: {item.Title}", "Twitter");
                
                // Record tweets in stats engine
                int partCount = string.IsNullOrEmpty(threadContent) ? 1 : threadContent.Split(new[] { "|||" }, StringSplitOptions.None).Length;
                _stats.RecordTweet("NewsEngine", partCount, item.Link, item.Title);
                
                // Kalıcı hafızaya PUBLISHED olarak kaydet
                _persistence.AddParsedNews(item.Title, item.Source, item.Link, 5, true);
                
                // Event tetikle (UI güncellemesi için)
                OnNewsProcessed?.Invoke(item, summary, 5);
            }
            else
            {
                OnLog?.Invoke($"❌ Paylaşım Başarısız: {result.message}", "Twitter");
            }
        }


        private (int Confidence, string Status, string Summary, string Symbols, string Reasoning) ParseAnalysisData(string raw)
        {
            int confidence = 0;
            string status = "REJECT";
            string summary = "";
            string symbols = "BIST100";
            string reasoning = "";

            try
            {
                var lines = raw.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("CONFIDENCE:")) int.TryParse(line.Replace("CONFIDENCE:", "").Trim(), out confidence);
                    if (line.StartsWith("STATUS:")) status = line.Replace("STATUS:", "").Trim();
                    if (line.StartsWith("SUMMARY:")) summary = line.Replace("SUMMARY:", "").Trim();
                    if (line.StartsWith("SYMBOLS:")) symbols = line.Replace("SYMBOLS:", "").Trim();
                    if (line.StartsWith("REASONING:")) reasoning = line.Replace("REASONING:", "").Trim();
                }
            }
            catch { }

            // Fallback parsing if JSON-like structure failed
            if (confidence == 0 && raw.Contains("Önem: 5")) confidence = 5;

            return (confidence, status, summary, symbols, reasoning);
        }

        private bool IsHighValueNews(string title)
        {
            var lower = title.ToLower();

            // 1. Türkiye Ekonomisine Direkt Etki Kontrolü (Core Filter)
            // ✅ KABUL (Türkiye Ekonomisini doğrudan ilgilendir)
            string[] trEconomyKeywords = {
                "bist", "borsaistanbul", "borsa", "kap", "merkez bankası", "tcmb", "enflasyon", "dolar", "lira",
                "tbmm", "hazine", "vergi", "gümrük", "ticaret", "ihracat", "ithacat", "dis ticaret",
                "türk ekonomi", "türkiye ekonom", "ekonomik büyüme", "gdp", "gayri safi", "faiz oranı",
                "bankasıofisi", "banka", "sigorta", "emlak", "gayrimenkul", "çimento", "döner sermaye",
                "kaolin", "fosfor", "maden", "petrol", "doğalgaz", "enerji", "elektrik", "su",
                "atatürk", "istanbul", "kanal", "kaynarca", "selçuk", "efes", "troia", "göbekli",
                "kültür ve turizm", "turizm", "otel", "havaalanı", "liman", "gemi", "ulaştırma",
                "makine", "otomobil", "tekstil", "hazır giyim", "ayakkabı", "plastik", "kimya",
                "gıda", "tarım", "hayvancılık", "balık", "su ürünleri", "ilaç", "sağlık",
                "telekom", "internet", "teknoloji", "yazılım", "fintech", "kripto", "blockchain",
                "rize", "kahramanmaraş", "gaziantep", "ankara", "izmir", "antalya", "bursa",
                "kamu özel ortaklık", "kpo", "ihracat kredi", "kredisi", "stb", "fon"
            };

            // 🚫 REDDET (Türkiye ekonomisini ALAKASIZ veya spesifik harcama)
            string[] nonTrKeywords = {
                "apple", "microsoft", "google", "amazon", "netflix", "meta", "nvidia", "tesla",
                "wall street", "nasdaq", "s&p 500", "dow jones", "fed", "federal reserve", "powell",
                "avrupa", "ab", "ecb", "deutsche", "pbb", "hsbc", "barclays", "lloyds",
                "londra", "paris", "frankfurt", "zurich", "tokyo", "sydney", "singapur",
                "forex", "dolar euro", "gbp", "jpy", "eur", "usd", "chf",
                "çin", "hindistan", "rusya", "abd", "usa", "america", "usa", "europeanunion",
                "kuzey kore", "iran", "suudi", "dubai", "abd başkanı"
            };

            // 2. Non-Turkey Check (Aggressively reject non-Turkey news)
            // EXCEPT: Allow if it mentions Turkey + international company (e.g., "Apple Türkiye'de yatırım yapmaya hazırlanıyor")
            if (nonTrKeywords.Any(k => lower.Contains(k)))
            {
                bool hasTurkey = lower.Contains("turkey") || lower.Contains("türkiye") || lower.Contains("türk") || lower.Contains("istanbul");
                bool hasMajorCompany = nonTrKeywords.Any(k => lower.Contains(k));
                if (hasTurkey && hasMajorCompany)
                {
                    return true; // Allow popular companies IF Turkey is also mentioned
                }
                return false;
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
                "burcu", "astroloji", "fal", "reklam", "dış haberleri", "sosyal medya"
            };
            if (junkKeywords.Any(k => lower.Contains(k)))
            {
                // EXCEPTION: Allow Fenerbahçe sports news
                if (lower.Contains("fenerbahçe") || lower.Contains("fb"))
                {
                    return true; // Allow Fenerbahçe sports news
                }
                return false;
            }

            // 5. Mikro Hisse Hareketleri (Quota Shield)
            if (lower.Contains("dolarlık hisse satışı") || lower.Contains("dolarlık hisse alımı"))
            {
                bool isLargeAmount = lower.Contains("00.000") || lower.Contains("milyon") || lower.Contains("milyar");
                if (!isLargeAmount) return false;
            }

            // 6. Rutin Analist Notları
            string[] analystJunk = { "fiyat hedefini", "tavsiyesini yineledi", "tavsiye bildirdi", "görünüm nedeniyle al", "görünüm nedeniyle sat" };
            if (analystJunk.Any(k => lower.Contains(k)))
            {
                 string[] majorBanks = { "goldman", "morgan stanley", "jpmorgan", "tcmb", "msci", "fitch", "moody", "s&p", "merrill" };
                 if (!majorBanks.Any(b => lower.Contains(b))) return false;
            }

            // 7. Diğer Gürültü Kriterleri
            if (title.Count(c => c == '#') > 5) return false;
            if (lower.Contains("hisse satışını tamamladı") && !lower.Contains("milyon")) return false;

            // 8. Default: Reject if no TR keyword match AND not spiky enough
            return false;
        }
    }
}


