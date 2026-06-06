// SIGNAL_ENGINE_VERSION: 1.0
// PURPOSE: Central orchestrator for signal lifecycle (Parsing -> AI Filtering -> Rich Generation -> Execution).
// This decouples the business logic from MainForm.cs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

using XiDeAI_Pro.Services.Core;

namespace XiDeAI_Pro.Services
{
    public class SignalEngine : IModule
    {
        // IModule Implementation
        public string ModuleName => "SIGNAL_ENGINE";
        public bool IsActive { get; set; } = true;

        private readonly SignalParser _parser;
        private readonly GeminiService _gemini;
        private readonly TwitterService _twitter;
        private readonly SocialIntelService _socialIntel;
        private readonly InfluencerControlService _influencerControl;
        private readonly ScreenshotService _screenshot;
        private readonly ThreadService _threadService;
        private readonly PromptManager _promptManager;
        private readonly PromptManager _prompts; // Alias for Deep Scan
        private readonly PerformanceTracker _performance;
        private readonly SpamProtection _spam;
        private readonly TelegramService _telegram;
        private readonly StatsEngine _stats;
        private readonly SignalPersistenceService _persistence; // Persistent memory (v3.1.2)
        private readonly MemoryEngine? _memory; // v3.8: Weekly thread control
        private readonly PostingService _posting;
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; set; } // Public for OperationManager linkage
        private readonly XiDeAI_Pro.Services.AI.ModelManager? _modelManager; // Multi-Model AI (v3.1+)

        // Batch State
        private readonly object _batchLock = new object();
        private List<SignalData> _pendingBatchSignals = new List<SignalData>();
        private System.Threading.CancellationTokenSource? _batchCts;
        private DateTime _lastBatchUpdate = DateTime.MinValue;
        private DateTime _batchStartUtc = DateTime.MinValue;

        // Batch Windows
        private static readonly TimeSpan BatchQuietWindow = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan BatchMaxWindow = TimeSpan.FromSeconds(240);
        private const int BatchMaxCount = 30;

        // Memory for Global Deduplication (independent of SpamProtection settings)
        private readonly Dictionary<string, DateTime> _globalSignalMemory = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _signalMemory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase); // For Common Signals logic
        private readonly object _memoryLock = new object();
        private static readonly TimeSpan GlobalCooldown = TimeSpan.FromHours(4);

        // Events for UI Updates
        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;
        public event Action<SignalData>? OnSignalProcessed;

        public SignalEngine(
            SignalParser parser, 
            GeminiService gemini, 
            TwitterService twitter, 
            SocialIntelService socialIntel,
            InfluencerControlService influencerControl,
            ScreenshotService screenshot,
            ThreadService threadService,
            PromptManager promptManager,
            PerformanceTracker performance,
            SpamProtection spam,
            TelegramService telegram,
            StatsEngine stats,
            SignalPersistenceService persistence,
            MemoryEngine? memory = null,
            XiDeAI_Pro.Services.AI.ModelManager? modelManager = null,
            PostingService? posting = null)
        {
            _parser = parser;
            _gemini = gemini;
            _twitter = twitter;
            _socialIntel = socialIntel;
            _influencerControl = influencerControl;
            _screenshot = screenshot;
            _threadService = threadService;
            _promptManager = promptManager;
            _prompts = promptManager; // Alias
            _performance = performance;
            _spam = spam;
            _telegram = telegram;
            _stats = stats;
            _persistence = persistence;
            _memory = memory;
            _modelManager = modelManager;
            _posting = posting ?? new PostingService(socialIntel, stats);
        }

        public async Task InitializeAsync()
        {
            OnLog?.Invoke("🚀 SignalEngine (Core Module) Initialized.", "System");
            await Task.CompletedTask;
        }

        public string GetStatus()
        {
            return $"Active: {IsActive}, Pending Batch: {_pendingBatchSignals.Count}";
        }
        
        // IModule generic processing
        public async Task ProcessSignalAsync(SignalData signal)
        {
             // Direct processing logic
             await ProcessStructuredSignal(signal);
        }

        // --- Future Hooks for AI Formation Analysis ---
        private Task<bool> AnalyzeFormationAsync(string symbol, string period)
        {
            // Placeholder for future multi-angle formation scan
            return Task.FromResult(true);
        }

        /// <summary>
        /// Entry point for a new signal log line
        /// </summary>
        public async Task ProcessRawSignal(string rawLine, string source)
        {
            try
            {
                // 1. Time Filter (Sync with Config - v3 Improved)
                var scanHours = ConfigManager.Current.ScanHours;
                if (scanHours != null && scanHours.Count > 0)
                {
                    bool isAllowed = false;
                    DateTime now = DateTime.Now;
                    foreach (var h in scanHours)
                    {
                        if (TimeSpan.TryParse(h, out TimeSpan ts))
                        {
                            // Eski projedeki gibi gün bazlı karşılaştırma (Midnight-Safe)
                            var target = DateTime.Today.Add(ts);
                            
                            // +/- 60 dakika tolerans (Eski proje standardı)
                            if (Math.Abs((now - target).TotalMinutes) <= 60)
                            {
                                isAllowed = true;
                                break;
                            }
                        }
                    }

                    if (!isAllowed) 
                    {
                        string msg = $"🛡️ Zaman Filtresi: Sinyal saati ({now:HH:mm}) taranacak saatlerle ({string.Join(", ", scanHours)}) eşleşmiyor (+/- 60 dk).";
                        OnLog?.Invoke(msg, "Engine");
                        Logger.Sys(msg); // Also to file
                        return;
                    }
                }

                // 2. Parse
                var signalsList = _parser.Parse(rawLine, source);
                if (signalsList == null || !signalsList.Any()) return;

                List<SignalData> validSignals = new List<SignalData>();

                foreach (var sig in signalsList)
                {
                    // 3. Common Scan Logic
                    if (ConfigManager.Current.OnlyCommonSignals)
                    {
                        var targetStrategies = ConfigManager.Current.CommonStrategies;

                        if (targetStrategies.Any())
                        {
                            lock (_memoryLock)
                            {
                                if (!_signalMemory.ContainsKey(sig.Symbol))
                                    _signalMemory[sig.Symbol] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                
                                _signalMemory[sig.Symbol].Add(sig.Strategy.ToUpper());

                                bool allFound = true;
                                foreach (var required in targetStrategies)
                                {
                                    if (!_signalMemory[sig.Symbol].Any(found => found.Contains(required)))
                                    {
                                        allFound = false;
                                        break;
                                    }
                                }

                                if (!allFound)
                                {
                                    OnLog?.Invoke($"⏳ Bekleniyor (Ortak Tarama): {sig.Symbol} (Eksik onaylar var)", "Engine");
                                    continue;
                                }
                                else
                                {
                                    OnLog?.Invoke($"✨ ORTAK TARAMA EŞLEŞTİ: {sig.Symbol}", "Engine");
                                }
                            }
                        }
                    }

                    validSignals.Add(sig);
                }

                // 4. Batch vs Single Decision
                // Kullanıcı thread (zincir) istiyor. Batch eşiğini yükseltiyoruz.
                // Eskiden 2 idi, şimdi 4 yapıyoruz. Yani 2 veya 3 sinyal gelirse batch OLMASIN, thread olsun.
                bool batchEnabled = !ConfigManager.Current.DisableSpamProtection; // Global koruma kapalıysa batch yapma (tek tek at)
                if (batchEnabled && validSignals.Count >= 4)
                {
                    EnqueueForBatch(validSignals);
                }
                else
                {
                    foreach (var sig in validSignals)
                    {
                        // Küçük, yönetilebilir sayıda sinyal direk thread'e
                        await ProcessStructuredSignal(sig);
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ SignalEngine Error (ProcessRaw): {ex.Message}", "System");
            }
        }

        public void EnqueueForBatch(List<SignalData> signals)
        {
            lock (_batchLock)
            {
                _lastBatchUpdate = DateTime.UtcNow;
                if (_pendingBatchSignals.Count == 0) _batchStartUtc = _lastBatchUpdate;

                foreach (var s in signals)
                {
                    bool exists = _pendingBatchSignals.Any(x =>
                        x.Symbol.Equals(s.Symbol, StringComparison.OrdinalIgnoreCase) &&
                        x.Strategy.Equals(s.Strategy, StringComparison.OrdinalIgnoreCase));
                    
                    if (!exists) _pendingBatchSignals.Add(s);
                }

                if (_pendingBatchSignals.Count >= BatchMaxCount)
                {
                    _ = FinalizeBatch();
                }
                else
                {
                    _batchCts?.Cancel();
                    _batchCts = new System.Threading.CancellationTokenSource();
                    var token = _batchCts.Token;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(BatchQuietWindow, token);
                            if (!token.IsCancellationRequested) await FinalizeBatch();
                        }
                        catch (OperationCanceledException) { }
                    }, token);
                }
            }
        }

        private async Task FinalizeBatch()
        {
            List<SignalData> batch;
            lock (_batchLock)
            {
                if (_pendingBatchSignals.Count == 0) return;
                batch = new List<SignalData>(_pendingBatchSignals);
                _pendingBatchSignals.Clear();
                _batchCts?.Cancel();
                _batchCts = null;
            }

            OnLog?.Invoke($"📦 Toplu Paylaşım Hazırlanıyor ({batch.Count} sinyal)...", "Engine");
            
            try 
            {
                // Group by Symbol to avoid same symbol appearing multiple times
                var grouped = batch
                    .GroupBy(s => s.Symbol.ToUpperInvariant())
                    .Select(g => new {
                        Symbol = g.Key,
                        Strategies = string.Join(", ", g.Select(x => x.Strategy).Distinct()),
                        LastPrice = g.Last().Price
                    })
                    .ToList();

                // Generate Batch Text
                string batchText = "📊 TOPLU SİNYAL RAPORU\n\n";
                foreach(var s in grouped.Take(12)) // Show up to 12 unique symbols
                {
                    batchText += $"• ${s.Symbol}: {s.Strategies} ({s.LastPrice:N2})\n";
                }

                if (grouped.Count > 12)
                {
                    batchText += $"...ve {grouped.Count - 12} diğer sembol.\n";
                }

                batchText += "\n🚀 Detaylar platformumuzda!";
                
                // Use centralized hashtag manager (ThreadService)
                var batchHashtags = _threadService.GetType().GetMethod("ExtractUniqueHashtags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_threadService, new object[] { ConfigManager.Current.DailyTrends, "BATCH", batchText }) as List<string>;
                
                if (batchHashtags != null)
                {
                    batchText += "\n" + string.Join(" ", batchHashtags);
                }
                else
                {
                    batchText += "\n#Borsa #AlgoritmikTrade #XideAI";
                }

                // Post
                bool sent = await ExecutePost(batchText, "", new SignalData { Symbol = "BATCH" });
                if (sent)
                {
                    OnLog?.Invoke($"✅ Toplu Sinyal Paylaşıldı ({batch.Count} adet)", "Twitter");
                    foreach(var s in batch) _spam.RecordTweet(s.Symbol, "BATCH");
                }
            }
            catch(Exception ex)
            {
                OnLog?.Invoke($"❌ Batch Finalize Hatası: {ex.Message}", "Engine");
            }
        }

        public async Task ProcessStructuredSignal(SignalData sig)
        {
            try
            {
                // v4.3.0: Hybrid Signal Intelligence Start
                _stats.RecordActivity("SignalEngine", $"Processing signal: {sig.Symbol}", true, sig.Strategy);
                OnLog?.Invoke($"📡 Hybrid Processing: {sig.Symbol} | Strategy: {sig.Strategy} | Tier: {sig.Tier} | Durum: {sig.Durum}{(sig.IsRoket ? " 🚀" : "")}", "HybridEngine");
                
                // 1. GLOBAL DEDUPLICATION
                if (_persistence.IsProcessed(sig))
                {
                    OnLog?.Invoke($"⏭️ Mükerrer Sinyal: {sig.Symbol} ({sig.Strategy})", "Engine");
                    return;
                }
                
                // 2. AI Pre-Filter
                bool isWorthAnalyzing = await CheckSignalQualityWithAI(sig);
                if (!isWorthAnalyzing) return;

                // 3. Spam/Cooldown Check
                if (ConfigManager.Current.SpamProtectSignals)
                {
                    if (!_spam.CanTweet(sig.Symbol, sig.Strategy, out string reason))
                    {
                        OnLog?.Invoke($"🛡️ Spam Filtresi: {reason}", "Engine");
                        return;
                    }
                }

                // 3.5 WEEKLY LIMIT
                if (_memory != null && _memory.GetWeeklyThreadCount(sig.Symbol) >= 1)
                {
                    OnLog?.Invoke($"🛡️ Haftalık Limit Dolu: {sig.Symbol}", "Engine");
                    return;
                }

                _performance.RecordSignal(sig);
                OnSignalProcessed?.Invoke(sig); 
                OnStatusUpdate?.Invoke($"{sig.Symbol} (Tier: {sig.Tier}) analiz ediliyor...");
                
                // 4. Analysis & Generation
                string? screenshotPath = await _screenshot.CaptureChart(sig.Symbol, sig.Period); // Parallel candidate

                // Context Preparation
                string priceContext = $"Fiyat: {sig.Price:0.00} {sig.Basis}, Strateji: {sig.Strategy}, Periyot: {sig.Period}, Durum: {sig.Durum}{(sig.IsRoket ? " (Roket 🚀)" : "")}";

                // Strateji bazlı tarama kriteri bağlamı
                string scanCriteria = sig.Strategy switch
                {
                    "ALPHA" => "TARAMA KRİTERLERİ: 60dk periyot | EMA200 üzerinde (trend: yukarı) | ADX>20 (güçlü trend) | 18-bar dar bant (squeeze/sıkışma) | Hacim 1.5x+ ortalamanın üzeri | Seçilme sebebi: momentum + volatilite kırılım beklentisi",
                    "PREMOVE" => "TARAMA KRİTERLERİ: Günlük periyot | Destek bölgesinde fiyat tespiti (PreMove) | Hacim artışı ile birlikte dip test | Seçilme sebebi: öncü hareket (önceden birikim) sinyali",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(scanCriteria))
                    priceContext += $"\n\n{scanCriteria}";
                if (sig.IsRoket)
                    priceContext += "\n\n⚡ ROKET SİNYALİ: Hacim 3x+ ve mum %1+ uzunluğunda — yüksek güven, ani hareket beklentisi.";
                
                // Visual Analysis (if shot exists)
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                     string visualAnalysis = await AnalyzeFormationAsync(sig.Symbol, sig.Period, priceContext, screenshotPath);
                     if (!string.IsNullOrEmpty(visualAnalysis) && !visualAnalysis.Contains("hata"))
                     {
                         priceContext += $"\n\nGRAFİK ANALİZİ:\n{visualAnalysis}";
                     }
                }

                // Influencer Intelligence: VIP listesi → genel X araması → sadece doğrulanmış mention
                string influencerCitations = "";
                var allowedMentionHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string symUpper = sig.Symbol.ToUpperInvariant();
                    string currentUser = ConfigManager.Current.XLoginUser?.Replace("@", "").Trim() ?? "";

                    Func<List<InfluencerPost>, List<InfluencerPost>> cleanFilter = (raw) => (raw ?? new List<InfluencerPost>())
                        .Where(p => !ContentQualityGuard.ContainsPrivateLinks(p.Content))
                        .Where(p => {
                            string h = p.Handle?.Replace("@", "").Trim() ?? "";
                            if (!string.IsNullOrEmpty(currentUser) && h.Equals(currentUser, StringComparison.OrdinalIgnoreCase)) return false;
                            string content = p.Content?.ToUpperInvariant() ?? "";
                            return content.Contains($"#{symUpper}") ||
                                   content.Contains($"${symUpper}") ||
                                   System.Text.RegularExpressions.Regex.IsMatch(content, $@"\b{symUpper}\b");
                        })
                        .ToList();

                    // Adım 1: VIP (fenomen modülü) listesiyle ara
                    var vipHandles = _influencerControl?.GetTopInfluencers(sig.Symbol, 20);
                    var vipPosts = cleanFilter(await _socialIntel.FindInfluencerAnalyses(sig.Symbol, sig.Market, vipHandles));
                    OnLog?.Invoke($"🔎 VIP arama: {vipPosts.Count} fenomen yorumu ({sig.Symbol})", "SocialIntel");

                    List<InfluencerPost> finalPosts = vipPosts;

                    // Adım 2: VIP'te bulunamazsa genel X araması
                    if (finalPosts.Count == 0)
                    {
                        OnLog?.Invoke($"🔍 VIP'te yok, X'te genel arama yapılıyor...", "SocialIntel");
                        finalPosts = cleanFilter(await _socialIntel.FindInfluencerAnalyses(sig.Symbol, sig.Market, null));
                        OnLog?.Invoke($"🔎 Genel arama: {finalPosts.Count} yorum", "SocialIntel");
                    }

                    if (finalPosts.Count > 0)
                    {
                        var topPosts = finalPosts
                            .OrderByDescending(p => p.Content?.Length ?? 0)
                            .Take(3)
                            .ToList();
                        var lines = new List<string>();
                        for (int i = 0; i < topPosts.Count; i++)
                        {
                            string prefix = i == 0 ? "⭐ [EN ALAKALI MENTION]" : "•";
                            string cleanHandle = topPosts[i].Handle?.TrimStart('@') ?? "";
                            if (string.IsNullOrWhiteSpace(cleanHandle)) continue;
                            if (!string.IsNullOrWhiteSpace(cleanHandle)) allowedMentionHandles.Add(cleanHandle);
                            string cleanContent = StripUnapprovedMentions(topPosts[i].Content?.Trim() ?? "", new HashSet<string>(new[] { cleanHandle }, StringComparer.OrdinalIgnoreCase), out _);
                            lines.Add($"{prefix} @{cleanHandle}: {cleanContent}");
                        }
                        influencerCitations = string.Join("\n", lines);
                        OnLog?.Invoke($"✅ Mention hazır → @{topPosts[0].Handle?.TrimStart('@')}", "SocialIntel");
                    }
                    else
                    {
                        // Sembole özel doğrulanmış post yoksa modele fallback handle verme.
                        OnLog?.Invoke($"ℹ️ {sig.Symbol} için doğrulanmış fenomen yorumu yok; mention kapalı.", "SocialIntel");
                    }
                }
                catch (Exception ex) { OnLog?.Invoke($"⚠️ Influencer araması başarısız: {ex.Message}", "SocialIntel"); }

                // GENERATE HYBRID CONTENT
                OnLog?.Invoke($"🧠 AI Analiz Üretiliyor (Tier: {sig.Tier})...", "HybridEngine");
                string htfContext = ConfigManager.Current?.DailyTrends ?? "";
                string? aiContent = await _gemini.GenerateStrategySpecificAnalysis(sig, priceContext, influencerCitations, htfContext);

                if (string.IsNullOrEmpty(aiContent))
                {
                    OnLog?.Invoke("❌ AI İçerik Üretemedi.", "Engine");
                    return;
                }

                aiContent = StripUnapprovedMentions(aiContent, allowedMentionHandles, out var removedMentions);
                if (removedMentions.Count > 0)
                {
                    OnLog?.Invoke($"🧹 Alakasız mention temizlendi: {string.Join(", ", removedMentions.Select(h => "@" + h))}", "SocialIntel");
                }

                // 5. Execution (New Raw Handler)
                OnLog?.Invoke($"🧵 Paylaşılıyor: {sig.Symbol}", "Twitter");
                
                var (sent, errorMsg) = await _threadService.PostAIGeneratedThread(sig, aiContent, screenshotPath ?? "");

                if (sent)
                {
                    _persistence.MarkAsProcessed(sig);
                    _spam.RecordTweet(sig.Symbol, sig.Strategy);
                    OnLog?.Invoke($"✅ Başarılı: {sig.Symbol} (Tier: {sig.Tier})", "Twitter");
                }
                else
                {
                    OnLog?.Invoke($"❌ Paylaşım Hatası: {errorMsg}", "Twitter");
                    // v4.6.15: Even on failure, record the attempt to trigger symbol cooldown and prevent loops
                    _spam.RecordTweet(sig.Symbol, sig.Strategy + "_FAILED");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ SignalEngine Error: {ex.Message}", "System");
            }
        }

        private static string StripUnapprovedMentions(string content, HashSet<string> allowedHandles, out List<string> removedHandles)
        {
            var localRemoved = new List<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                removedHandles = localRemoved;
                return content ?? string.Empty;
            }

            string cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"(?<![\w])@([A-Za-z0-9_]{1,15})\b", match =>
            {
                string handle = match.Groups[1].Value;
                if (allowedHandles.Contains(handle)) return match.Value;
                if (!localRemoved.Contains(handle, StringComparer.OrdinalIgnoreCase)) localRemoved.Add(handle);
                return string.Empty;
            });

            removedHandles = localRemoved;
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[ \t]{2,}", " ");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+([,.;:!?])", "$1");
            return cleaned.Trim();
        }

        private async Task<bool> CheckSignalQualityWithAI(SignalData sig)
        {
            var cfg = ConfigManager.Current;

            // Alpha ve PreMove robotları DB'ye sadece kendi eşiklerini geçen sinyalleri yazar.
            // (Alpha≥90p, PreMove≥75p) — burada ek skor filtresi gereksiz.
            // PULLBACK_ADAY dahil tüm DB satırları geçer; filtre sadece Enable bayrağı.
            if (sig.Source == "ALPHA" && !cfg.EnableAlpha)
            {
                OnLog?.Invoke($"⏭️ Alpha sinyali devre dışı: {sig.Symbol}", "Engine");
                return false;
            }
            if (sig.Source == "PREMOVE" && !cfg.EnablePreMove)
            {
                OnLog?.Invoke($"⏭️ PreMove sinyali devre dışı: {sig.Symbol}", "Engine");
                return false;
            }
            if (cfg.AlphaOnlyAktif && sig.Source == "ALPHA" && sig.Durum != "AKTIF")
            {
                OnLog?.Invoke($"⏭️ Alpha PULLBACK_ADAY filtresiyle elendi: {sig.Symbol}", "Engine");
                return false;
            }
            if (cfg.PreMoveOnlyAktif && sig.Source == "PREMOVE" && sig.Durum != "AKTIF")
            {
                OnLog?.Invoke($"⏭️ PreMove PULLBACK_ADAY filtresiyle elendi: {sig.Symbol}", "Engine");
                return false;
            }

            OnLog?.Invoke($"✅ Sinyal Onaylandı: {sig.Symbol} ({sig.Strategy} | {sig.Durum}{(sig.IsRoket ? " 🚀" : "")})", "Engine");

            // Step 2: AI Deep Scan (Optional - Config controlled)
            // Phase 4: Deep Scan to save API quota
            if (cfg.EnableDeepScan)
            {
                try
                {
                    OnLog?.Invoke($"🔍 Deep Scan başlatılıyor: {sig.Symbol}", "Engine");
                    string prompt = _prompts.GetDeepScanPrompt(sig);
                    
                    string? response = null;
                    
                    // Use ModelManager if available (v3.1+)
                    if (_modelManager != null && cfg.EnableMultiModel)
                    {
                        response = await _modelManager.SendRequest(
                            XiDeAI_Pro.Services.AI.TaskType.DeepScan,
                            prompt,
                            maxTokens: 100  // Simple yes/no, minimal tokens
                        );
                    }
                    else
                    {
                        // Fallback to GeminiService
                        response = await _gemini.SendRequest(prompt);
                    }
                    
                    if (response != null && response.ToUpperInvariant().Contains("WORTHY"))
                    {
                        OnLog?.Invoke($"✅ Deep Scan: {sig.Symbol} analize değer bulundu.", "Engine");
                        return true;
                    }
                    else
                    {
                        OnLog?.Invoke($"⏭️ Deep Scan: {sig.Symbol} zayıf/gürültülü olarak değerlendirildi, atlandı.", "Engine");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"⚠️ Deep Scan hatası: {ex.Message}, varsayılan olarak devam ediliyor.", "Engine");
                    return true; // Hata durumunda sinyali geçir (fail-safe)
                }
            }
            
            return true; 
        }

        /// <summary>
        /// Phase 2: AI Formation Analysis
        /// Asks Gemini to identify chart patterns from a screenshot
        /// </summary>
        private async Task<string> AnalyzeFormationAsync(string symbol, string period, string priceContext, string screenshotPath)
        {
            try
            {
                if (string.IsNullOrEmpty(screenshotPath)) return "Görsel analiz yapılamadı (Ekran görüntüsü yok).";

                OnLog?.Invoke($"🔍 AI Formasyon Analizi Başlatıldı: {symbol} ({period}dk)", "Engine");
                
                string marketType = (symbol.Contains("BTC") || symbol.Contains("ETH") || symbol.Contains("USDT")) ? "Crypto" : "BIST";
                string? analysis = await _gemini.GenerateMarketAnalysisWithChart(symbol, marketType, priceContext, screenshotPath);

                if (!string.IsNullOrEmpty(analysis))
                {
                    OnLog?.Invoke($"✅ AI Formasyon Tespit Edildi: {symbol}", "Engine");
                    return analysis;
                }
                
                return "Belirgin bir formasyon tespit edilemedi.";
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Formasyon Analiz Hatası: {ex.Message}", "Engine");
                return "Analiz sırasında hata oluştu.";
            }
        }



        private async Task<bool> ExecutePost(string content, string mediaPath, SignalData sig)
        {
            var threadTweets = ThreadPipeline.ParseThreadPayload(content, 280);
            if (threadTweets.Count > 1)
            {
                var res = await _posting.PostThreadAsync(threadTweets, mediaPath, "SignalEngine");
                return res.status == "success";
            }

            // Web automation first
            var result = await _posting.PostTweetAsync(content, mediaPath, "SignalEngine");
            if (result.status == "success")
            {
                _stats.RecordActivity("SignalEngine", $"Posted tweet via Web: {sig.Symbol}", true);
                return true;
            }
            _stats.RecordActivity("SignalEngine", $"Post failed: {sig.Symbol}", false, result.ErrorMessage);
            return false;
        }
    }
}
