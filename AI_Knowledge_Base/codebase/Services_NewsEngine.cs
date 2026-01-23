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
        
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; set; }

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
                // v3.8.5: Rate Limiting - Prevent TooManyRequests when bulk processing
                await Task.Delay(2000); // 2 second delay between news items
                
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
                
                // v3.0 FIX: FENERBAHÇE BYPASS (Fanatik Modu)
                string lowerTitle = item.Title.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
                if (lowerTitle.Contains("fenerbahçe") || lowerTitle.Contains("fenerbahce") || lowerTitle.Contains("sarı lacivert") || lowerTitle.Contains("ali koç") || lowerTitle.Contains("jose mourinho"))
                {
                    if (score < 3)
                    {
                        score = 4; // Force Approval
                        status = "PENDING";
                        analysisData.Reasoning = "⚠️ FAN ZONe BYPASS: Fenerbahçe haberi tespit edildi, AI düşük skoru geçersiz kılındı.";
                        OnLog?.Invoke($"💛💙 Fenerbahçe Haberi Tespit Edildi ve Korundu: {item.Title}", "NewsEngine");
                    }
                }

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
                     var (success, msg) = await PostNewsThreadToTwitter(item, summary, symbols);
                     if (!success)
                     {
                         OnLog?.Invoke($"⚠️ Otomatik paylaşım başarısız: {msg}", "NewsEngine");
                     }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ NewsEngine Error: {ex.Message}", "System");
            }
        }

        public async Task<(bool success, string message)> ForcePostNews(NewsItem item, string summary)
        {
            OnLog?.Invoke($"👤 Kullanıcı Onayladı: {item.Title}", "NewsEngine");
            return await PostNewsThreadToTwitter(item, summary, "BIST100");
        }

        private async Task<(bool success, string message)> PostNewsThreadToTwitter(NewsItem item, string summary, string symbols)
        {
            // Config: SpamProtectNews Check
            if (ConfigManager.Current.SpamProtectNews)
            {
                if (!_spam.CanPostGeneral(out string reason))
                {
                    OnLog?.Invoke($"🛡️ Spam koruması: {reason}", "NewsEngine");
                    return (false, $"Spam koruması: {reason}");
                }
            }

            // Generate Thread Content
            string? threadContent = await _gemini.AnalyzeNewsForThread(item.Title, item.Source, item.Link);
            
            // v3.0: İçerik Kalite Kontrolü
            if (ContentQualityGuard.IsSpamOrLowQuality(threadContent ?? "", out string spamReason))
            {
                OnLog?.Invoke($"⚠️ Thread içeriği SPAM/KALİTESİZ ({spamReason}), atlanıyor.", "NewsEngine");
                return (false, $"Thread içeriği kalitesiz: {spamReason}");
            }

            if (string.IsNullOrEmpty(threadContent) || threadContent.Contains("NO_IMPACT"))
            {
                OnLog?.Invoke("⚠️ AI thread içeriği üretemedi, işlem iptal.", "NewsEngine");
                return (false, "AI thread içeriği üretemedi");
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
                
                return (true, "Haber başarıyla paylaşıldı!");
            }
            else
            {
                OnLog?.Invoke($"❌ Paylaşım Başarısız: {result.message}", "Twitter");
                return (false, $"Twitter hatası: {result.message}");
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
                    // v3.6.5: Robust parsing (strip markdown stars and be case-insensitive)
                    string cleanLine = line.Replace("**", "").Trim();
                    
                    if (cleanLine.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase)) 
                        int.TryParse(cleanLine.Substring("CONFIDENCE:".Length).Trim(), out confidence);
                    else if (cleanLine.StartsWith("STATUS:", StringComparison.OrdinalIgnoreCase)) 
                        status = cleanLine.Substring("STATUS:".Length).Trim().ToUpper();
                    else if (cleanLine.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase)) 
                        summary = cleanLine.Substring("SUMMARY:".Length).Trim();
                    else if (cleanLine.StartsWith("SYMBOLS:", StringComparison.OrdinalIgnoreCase)) 
                        symbols = cleanLine.Substring("SYMBOLS:".Length).Trim();
                    else if (cleanLine.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase)) 
                        reasoning = cleanLine.Substring("REASONING:".Length).Trim();
                }
            }
            catch { }

            // Fallback parsing if JSON-like structure failed
            if (confidence == 0 && raw.Contains("Önem: 5")) confidence = 5;

            return (confidence, status, summary, symbols, reasoning);
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

            // 🚫 REDDET (Türkiye ekonomisini ALAKASIZ veya spesifik yerel gürültü)
            string[] nonTrKeywords = {
                "wall street", "nasdaq", "s&p 500", "dow jones", 
                "avrupa", "ab", "ecb", "deutsche", 
                "londra", "paris", "frankfurt", "zurich", "tokyo", "sydney", "singapur",
                "forex", "dolar euro", "gbp", "jpy", "eur", "chf",
                "çin", "hindistan", "rusya", "suudi", "dubai"
            };

            // v3.6.5: Allow high-impact Global Tech News as they affect BIST technology sector
            string[] globalImpactTech = { "apple", "nvidia", "tesla", "microsoft", "google", "fed", "openai", "chatgpt", "btc", "bitcoin", "kripto" };
            if (globalImpactTech.Any(k => lower.Contains(k)) && (lower.Contains("kritik") || lower.Contains("rekor") || lower.Contains("yeni") || lower.Contains("faiz") || lower.Contains("karar")))
            {
                // Global olsa bile kritik teknoloji/kripto haberi
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


