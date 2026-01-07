using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;
using XiDeAI_Pro.Services;
using XiDeAI_Pro.Services.Core;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// OPERATION_MANAGER_VERSION: 1.0
    /// PURPOSE: Centralized controller for all system modules and background operations.
    /// This acts as the 'Brain' of XiDeAI Pro, separating logic from UI (MainForm).
    /// </summary>
    public class OperationManager
    {
        // Core Services
        public LogFileWatcher Watcher { get; private set; } = null!;
        public TwitterService Twitter { get; private set; } = null!;
        public GeminiService Gemini { get; private set; } = null!;
        public SignalParser Parser { get; private set; }
        public SpamProtection Spam { get; private set; } = null!;
        public PerformanceTracker Performance { get; private set; } = null!;
        public SignalEngine SignalEng { get; private set; } = null!;
        public NewsEngine NewsEng { get; private set; } = null!;
        public SocialIntelService SocialIntel { get; private set; }
        public InteractionEngine Interaction { get; private set; } = null!;
        public OperationEngine OpEngine { get; private set; } = null!;
        public SchedulerService Scheduler { get; private set; } = null!;
        public StatsEngine Stats { get; private set; } = null!;
        public MemoryEngine Memory { get; private set; } = null!;
        public ScreenshotService Screenshot { get; private set; } = null!;
        public ThreadService ThreadSvc { get; private set; } = null!;
        public TelegramService Telegram { get; private set; } = null!;
        public PromptManager Prompts { get; private set; }
        public InfluencerControlService InfluencerControl { get; private set; }
        public ManualAnalysisService ManualAnalysis { get; private set; } = null!;
        public NewsTrackerService NewsTracker { get; private set; } = null!;
        public DependencyManager DependencyManager { get; private set; } = null!;
        public TrendService TrendService { get; private set; } = null!;
        public GuruPersistenceService GuruPersist { get; private set; } = null!;
        public PriceFetchService PriceFetch { get; private set; } = null!;
        
        // Multi-Model AI Manager (v3.1+)
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; private set; }

        // Module Registry
        private readonly List<IModule> _modules = new List<IModule>();

        // Logging
        public event Action<string, string>? OnLog;
        // public event Action<string>? OnStatusUpdate; // Removed unused event
        public event Action<string>? OnLogAI;

        public OperationManager()
        {
            Prompts = new PromptManager();
            Parser = new SignalParser();
            SocialIntel = new SocialIntelService();
            InfluencerControl = new InfluencerControlService();
        }

        public async Task InitializeAllAsync(string appDataDir, Action<string, string> logDelegate)
        {
            try
            {
                OnLog += logDelegate;
                Log("🧠 OperationManager: Başlatılıyor...", "System");

                // 1. Storage & Stats
                InfluencerControl.LoadDatabase();
                Stats = new StatsEngine(Path.Combine(appDataDir, "stats.json"));
                Memory = new MemoryEngine(Path.Combine(appDataDir, "memory.json"));
                Spam = new SpamProtection(Path.Combine(appDataDir, "tweet_log.json"));
                Performance = new PerformanceTracker(Path.Combine(appDataDir, "performance_data.json"));

                // 2. Base Services
                Twitter = new TwitterService();
                Gemini = new GeminiService(Memory, Stats);
                Telegram = new TelegramService();
                Screenshot = new ScreenshotService(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "screenshot.py"),
                    Path.Combine(appDataDir, "screenshots"),
                    (msg) => Log(msg, "Screenshot")
                );

                // 3. Logic Engines
                ThreadSvc = new ThreadService(Twitter, Gemini, SocialIntel, InfluencerControl);
                
                SignalEng = new SignalEngine(
                    Parser, Gemini, Twitter, SocialIntel, Screenshot, 
                    ThreadSvc, Prompts, Performance, Spam, Telegram, Stats, ModelManager
                );

                Interaction = new InteractionEngine(
                    SocialIntel, Twitter, Gemini, InfluencerControl, Spam, Telegram, Stats
                );

                OpEngine = new OperationEngine(
                    Gemini, SocialIntel, Twitter, Spam, Prompts, Stats
                );

                var newsPersistence = new NewsPersistenceService();
                NewsEng = new NewsEngine(
                    Gemini, Twitter, SocialIntel, Telegram, Spam, Prompts, Stats, newsPersistence
                );

                ManualAnalysis = new ManualAnalysisService(Gemini, Screenshot, SocialIntel, InfluencerControl, Memory, (msg) => LogAI(msg));
                NewsTracker = new NewsTrackerService(Gemini, SocialIntel);
                Scheduler = new SchedulerService();
                DependencyManager = new DependencyManager((msg) => Log(msg, "System"));
                TrendService = new TrendService();
                GuruPersist = new GuruPersistenceService();
                PriceFetch = new PriceFetchService();
                
                // Multi-Model AI Manager (v3.1+)
                var cfg = ConfigManager.Current;
                if (cfg.EnableMultiModel)
                {
                    ModelManager = new XiDeAI_Pro.Services.AI.ModelManager((msg) => Log(msg, "AI"));
                    
                    // Register Gemini providers (multiple models)
                    var geminiFlash = new XiDeAI_Pro.Services.AI.GeminiProvider(
                        cfg.GeminiApiKey, 
                        "gemini-2.0-flash-exp", 
                        (msg) => Log(msg, "AI")
                    );
                    ModelManager.RegisterProvider("gemini-flash", geminiFlash);
                    
                    var geminiPro15 = new XiDeAI_Pro.Services.AI.GeminiProvider(
                        cfg.GeminiApiKey, 
                        "gemini-1.5-pro", 
                        (msg) => Log(msg, "AI")
                    );
                    ModelManager.RegisterProvider("gemini-pro-1.5", geminiPro15);
                    
                    var geminiPro20 = new XiDeAI_Pro.Services.AI.GeminiProvider(
                        cfg.GeminiApiKey, 
                        "gemini-2.0-pro-exp", 
                        (msg) => Log(msg, "AI")
                    );
                    ModelManager.RegisterProvider("gemini-pro-2.0", geminiPro20);
                    
                    // Register Perplexity (if API key exists)
                    if (!string.IsNullOrEmpty(cfg.PerplexityApiKey))
                    {
                        var perplexitySonar = new XiDeAI_Pro.Services.AI.PerplexityProvider(
                            cfg.PerplexityApiKey, 
                            cfg.PerplexityModel, 
                            (msg) => Log(msg, "AI")
                        );
                        ModelManager.RegisterProvider($"perplexity-{cfg.PerplexityModel}", perplexitySonar);
                        
                        // Also register sonar-pro if model is sonar-pro
                        if (cfg.PerplexityModel == "sonar-pro")
                        {
                            ModelManager.RegisterProvider("perplexity-sonar-pro", perplexitySonar);
                        }
                        else
                        {
                            // Register regular sonar
                            ModelManager.RegisterProvider("perplexity-sonar", perplexitySonar);
                        }
                    }
                    
                    // Log available providers
                    var providers = ModelManager.GetAvailableProviders();
                    Log($"✅ Multi-Model AI: {providers.Count} providers registered", "System");
                }
                SocialIntel.SetInfluencerControl(InfluencerControl);
                NewsEng.OnLog += (m, s) => Log(m, s);
                NewsTracker.LogAction = (m, s) => Log(m, s);
                NewsTracker.OnNewsDetected += async (item) => await NewsEng.ProcessNews(item); // Fix: Wire news to engine
                OpEngine.OnLog += (m, s) => Log(m, s);
                Scheduler.OnLog += (m, s) => Log(m, s);
                Scheduler.OnGuruCheckTime += () => ProcessGuruAutomationAsync((msg) => Log(msg, "Social"));
                
                // Initialize background tasks
                await SignalEng.InitializeAsync();
                
                Log("✅ Tüm servisler OperationManager tarafından ayağa kaldırıldı.", "System");
            }
            catch (Exception ex)
            {
                Log($"❌ Başlatma Hatası: {ex.Message}", "System");
                throw;
            }
        }

        public void RegisterModule(IModule module)
        {
            if (!_modules.Contains(module))
                _modules.Add(module);
        }

        public void StartOperations()
        {
            Scheduler.Start();
            // NewsTracker.Start(); // Removed auto-start, user will start manually from News Panel
            Log("🚀 Zamanlayıcı ve Operasyonlar başlatıldı.", "System");
        }

        public void StopOperations()
        {
            Scheduler.Dispose();
            Log("🛑 Operasyonlar durduruldu.", "System");
        }

        private void LogAI(string msg)
        {
            OnLogAI?.Invoke(msg);
        }

        private void Log(string msg, string src)
        {
            OnLog?.Invoke(msg, src);
        }

        public async Task ProcessGuruAutomationAsync(Action<string> logCallback)
        {
            if (!Config.ConfigManager.Current.IsGuruAutomationEnabled) return;

            logCallback($"🤖 Otomatik Üstat Taraması Başladı: @EFELERiiNEFESi3");
            try
            {
                var posts = await SocialIntel.FindInfluencerAnalyses("@EFELERiiNEFESi3", "BIST", new List<string> { "@EFELERiiNEFESi3" });
                int processedCount = 0;

                foreach (var post in posts)
                {
                    if (string.IsNullOrEmpty(post.ImageUrl)) continue;
                    if (GuruPersist.IsProcessed(post.Url)) continue;

                    logCallback($"🎯 Yeni tablo tespit edildi: {post.Url}");
                    
                    // Parse
                    var (items, tableName) = await Gemini.ParseGuruTableFromImage(post.ImageUrl.Split(',')[0]);
                    if (items.Count > 0)
                    {
                        foreach (var (symbol, period) in items)
                        {
                            var thread = await Gemini.GenerateGuruHonoringThread(symbol, period, post.Handle, post.Url, tableName);
                            if (!string.IsNullOrEmpty(thread))
                            {
                                logCallback($"🚀 #{symbol} için thread hazırlandı. (Otomatik)");
                                
                                var tweets = thread.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(t => t.Trim())
                                                   .ToList();
                                                   
                                await SocialIntel.PostThreadAsync(tweets, null);
                            }
                        }
                        GuruPersist.MarkAsProcessed(post.Url);
                        processedCount++;
                    }
                }

                if (processedCount > 0)
                    logCallback($"✅ Otomatik tarama tamamlandı. {processedCount} yeni paylaşım işlendi.");
                else
                    logCallback($"😴 Yeni paylaşım bulunamadı.");
            }
            catch (Exception ex)
            {
                logCallback($"❌ ProcessGuruAutomationAsync Hatası: {ex.Message}");
            }
        }
    }
}


