using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using XiDeAI_Pro.Config;
using System.Windows.Forms;
using System.Timers;
using XiDeAI_Pro.Services.AI; 

namespace XiDeAI_Pro.Services
{


    public enum OperationStatus
    {
        Draft,
        Ready,
        Live_Execution,
        Monitoring,
        Completed,
        Archived
    }

    public class OperationTweet
    {
        // Fields expected by OperatorForm and Logic
        public string Text { get; set; } = ""; 
        public string MediaPath { get; set; } = "";
        public bool IsPosted { get; set; } = false;
        public DateTime PostedAt { get; set; }
        public string PostedUrl { get; set; } = "";
        
        // Alias properties for compatibility if needed
        public string Content 
        { 
            get => Text; 
            set => Text = value; 
        }
        
        // Status string for legacy check (if any)
        public string Status 
        {
            get => IsPosted ? "Posted" : "Pending";
            set => IsPosted = (value == "Posted");
        }
    }

    public class Operation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string Objective { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public OperationStatus Status { get; set; } = OperationStatus.Draft;
        public List<OperationTweet> TwitterChain { get; set; } = new List<OperationTweet>();
        public string TelegramContent { get; set; } = "";
        public string TelegramPoll { get; set; } = "";
        public List<string> MonitorKeywords { get; set; } = new List<string>();
        public string CortexSynthesis { get; set; } = "";
        
        // Required by OperatorForm
        public string CortexReportId { get; set; } = "";
        public string TargetAudience { get; set; } = ""; 
        public string StrategyNotes { get; set; } = "";
        
        // v3.7.3: Social Media Optimization (HIVE Operator only)
        public string SourceAuthor { get; set; } = ""; // Mention (e.g., "@username")
        public string SourceUrl { get; set; } = ""; // Link to source (tweet/article)
        public string GlobalHashtags { get; set; } = ""; // Comma-separated hashtags
    }

    public class OperationManager
    {
        // --- PATHS ---
        private readonly string _baseDir;
        private readonly string _appDataDir;
        private readonly string _scriptsDir;
        private readonly string _logsDir;
        private readonly string _opsFilePath;

        // --- SERVICES ---
        public SocialIntelService SocialIntel { get; private set; }
        public MemoryEngine Memory { get; private set; }
        public TelegramService Telegram { get; private set; }
        public NewsEngine NewsEng { get; private set; }
        public GeminiService Gemini { get; private set; }
        public SpamProtection Spam { get; private set; }
        public TwitterService Twitter { get; private set; }
        public ManualAnalysisService ManualAnalysis { get; private set; }
        public ThreadService ThreadSvc { get; private set; }
        public PerformanceTracker Performance { get; private set; }
        public InteractionEngine Interaction { get; private set; }
        public FanZoneService FanZone { get; private set; }
        public ScreenshotService Screenshot { get; private set; }
        public PriceFetchService PriceFetch { get; private set; }

        public PromptManager Prompts { get; private set; }
        public InfluencerControlService InfluencerControl { get; private set; }
        public StatsEngine Stats { get; private set; }
        public NewsPersistenceService NewsPersistence { get; private set; }
        public SignalPersistenceService SignalPersistence { get; private set; }
        public SignalParser SignalParser { get; private set; }
        public GuruPersistenceService GuruPersistence { get; private set; }
        public DependencyManager DependencyManager { get; private set; }
        public NewsTrackerService NewsTracker { get; private set; }
        public SignalEngine SignalEng { get; private set; }
        public TrendService TrendService { get; private set; }
        public ModelManager? ModelManager { get; private set; }
        public AthleteDiscoveryService AthleteDiscovery { get; private set; } // v4.1.0
        
        // --- AUTO BENCHMARK ---
        private System.Timers.Timer? _dailyBenchmarkTimer;
        private DateTime _lastBenchmarkRun = DateTime.MinValue;

        // --- EVENTS ---
        // MainForm expects Action<string> due to matching LogAI(msg)
        public event Action<string>? OnLogAI;
        

        // --- STATE ---
        private List<Operation> _operations = new List<Operation>();

        public OperationManager()
        {
            // 1. Setup Paths
            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _scriptsDir = Path.Combine(_baseDir, "Scripts");
            _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
            _logsDir = Path.Combine(_appDataDir, "Logs");
            Directory.CreateDirectory(_appDataDir);
            Directory.CreateDirectory(_logsDir);
            
            _opsFilePath = Path.Combine(_appDataDir, "active_operations.json");

            // 2. Base Utility Services (No dependencies)
            Stats = new StatsEngine(_logsDir);
            Spam = new SpamProtection(Path.Combine(_logsDir, "spam_log.json"));
            SocialIntel = new SocialIntelService();
            Twitter = new TwitterService(SocialIntel);
            Telegram = new TelegramService();
            Performance = new PerformanceTracker(Path.Combine(_appDataDir, "performance_data.json"));
            PriceFetch = new PriceFetchService();
            Prompts = new PromptManager();

            Memory = new MemoryEngine(Path.Combine(_appDataDir, "memory_store.json")); // Pre-requisite for Gemini
            InfluencerControl = new InfluencerControlService();
            NewsPersistence = new NewsPersistenceService();
            SignalPersistence = new SignalPersistenceService();
            SignalParser = new SignalParser();
            GuruPersistence = new GuruPersistenceService();
            DependencyManager = new DependencyManager((msg) => Logger.Sys($"[DepMgr] {msg}"));
            TrendService = new TrendService();
            ModelManager = new ModelManager((msg) => Logger.AI($"[ModelMgr] {msg}")); 
            Screenshot = new ScreenshotService(
                Path.Combine(_scriptsDir, "screenshot.py"), 
                Path.Combine(_baseDir, "screenshots"), 
                (msg) => Logger.Sys($"[Screenshot] {msg}"));

            // 3. Core Intelligence Services (Dependency: Memory, Stats, ModelManager)
            Gemini = new GeminiService(Memory, Stats);
            Gemini.ModelManager = ModelManager;

            SocialIntel.SetMemoryEngine(Memory);
            SocialIntel.SetInfluencerControl(InfluencerControl);
            SocialIntel.SetStatsEngine(Stats);
            
            // v4.4.0: X daemon in background (DISABLED AUTO-START for safety)
            // _ = Task.Run(async () => {
            //     try { await SocialIntel.StartDaemonAsync(); }
            //     catch (Exception ex) { Logger.Sys($"[Daemon] Startup error: {ex.Message}"); }
            // });
            
            // 4. Analysis & Execution Services (Dependency: Intelligence)
            // [HIVE REMOVED] Sentinel service moved to HiveProjesi
            ThreadSvc = new ThreadService(Twitter, Gemini, SocialIntel, InfluencerControl, Stats);
            
            NewsEng = new NewsEngine(Gemini, Twitter, SocialIntel, Telegram, Spam, Prompts, Stats, NewsPersistence);
            NewsEng.ModelManager = ModelManager;
            
            // [HIVE REMOVED] Apex service moved to HiveProjesi
            FanZone = new FanZoneService(SocialIntel, Gemini);
            AthleteDiscovery = new AthleteDiscoveryService(SocialIntel); // v4.1.0
            NewsTracker = new NewsTrackerService(Gemini, SocialIntel);

            // 5. High Level Product Services
            ManualAnalysis = new ManualAnalysisService(
                Gemini, 
                Screenshot, 
                SocialIntel, 
                InfluencerControl, 
                Memory, 
                NewsEng,
                (msg) => Console.WriteLine($"[ManualAnalysis] {msg}")
            );

            Interaction = new InteractionEngine(
                SocialIntel, 
                Twitter, 
                Gemini, 
                InfluencerControl, 
                Spam, 
                Telegram, 
                Stats
            );

            SignalEng = new SignalEngine(
                SignalParser,
                Gemini,
                Twitter,
                SocialIntel,
                InfluencerControl,
                Screenshot,
                ThreadSvc,
                Prompts,
                Performance,
                Spam,
                Telegram,
                Stats,
                SignalPersistence,
                Memory,  // v3.8: Weekly thread control
                ModelManager
            );

            // [HIVE REMOVED] OmniScout and Oracle services moved to HiveProjesi

            SyncGeminiProviders(); // v3.7.2: Ensure providers are registered on startup
            SyncLMStudioProviders(); // v4.6.0: Local models / LM Studio
            LoadOperations();
        }

        // Updated Signature to match MainForm call (string, Action<string,string>)
        public async Task InitializeAllAsync(string appDataDir, Action<string, string> logger)
        {
            // Log startup
            logger("🔑 OperationManager Initialization...", "System");
            
            await DependencyManager.CheckAndUpdateAllAsync();
            await SignalEng.InitializeAsync();
             
            logger("✅ OperationManager Ready.", "System");
        }
        
        // Overload if needed
        public async Task InitializeAllAsync()
        {
            await InitializeAllAsync(_appDataDir, (msg, src) => Console.WriteLine($"[{src}] {msg}"));
        }

        public void StartOperations()
        {
            // DISABLED AUTO-START for safety
            // NewsTracker.Start();
            // FanZone.Start(); 
            // v4.1.0: Start Athlete Discovery in background
            // Task.Run(async () => {
            //     await Task.Delay(10000); // 10 sn bekle
            //     if (ConfigManager.Current.FenerbahceAthletes.Count == 0)
            //     {
            //         await AthleteDiscovery.DiscoverAthletes();
            //     }
            // });
        }

        // SyncGeminiProviders - v3.7.2: Register providers in ModelManager
        public void SyncGeminiProviders(ModelManager manager)
        {
            this.ModelManager = manager;
            if (SignalEng != null) SignalEng.ModelManager = manager;
            if (NewsEng != null) NewsEng.ModelManager = manager;
            if (Gemini != null) Gemini.ModelManager = manager;
            SyncGeminiProviders(); // Perform initial registration
        }

        public void SyncGeminiProviders(List<string> models)
        {
            if (ModelManager == null) return;
            var cfg = ConfigManager.Current;
            string geminiKey = cfg.GeminiApiKey;
            
            if (string.IsNullOrEmpty(geminiKey)) return;

            foreach (var m in models)
            {
                var provider = new GeminiProvider(geminiKey, m, (msg) => Console.WriteLine($"[AI-Gemini] {msg}"));
                ModelManager.RegisterProvider(m, provider);
            }
            
            // Also sync Perplexity if key exists
            SyncPerplexityProviders();
        }
        public void SyncGeminiProviders()
        {
            // v5.0.0: Gemini and Perplexity removed. Only Local LM Studio.
            SyncLMStudioProviders();
        }

        public void SyncLMStudioProviders()
        {
            if (ModelManager == null) return;
            var cfg = ConfigManager.Current;
            
            if (cfg.LMStudioEnabled && !string.IsNullOrEmpty(cfg.LMStudioUri))
            {
                var provider = new LMStudioProvider(
                    cfg.LMStudioUri, 
                    cfg.LMStudioApiKey, 
                    cfg.LMStudioModel, 
                    (msg) => Logger.AI($"[AI-LMStudio] {msg}")
                );
                
                ModelManager.RegisterProvider(cfg.LMStudioModel, provider);
                // Also register as "lm-studio" generic handle
                ModelManager.RegisterProvider("lm-studio", provider);
            }
        }

        private void SyncPerplexityProviders()
        {
            if (ModelManager == null) return;
            var cfg = ConfigManager.Current;
            if (!string.IsNullOrEmpty(cfg.PerplexityApiKey))
            {
                var pModels = new[] { "sonar", "sonar-pro" };
                foreach (var pm in pModels)
                {
                    var pProvider = new PerplexityProvider(cfg.PerplexityApiKey, pm, (msg) => Console.WriteLine($"[AI-Perplexity] {msg}"));
                    ModelManager.RegisterProvider($"perplexity-{pm}", pProvider);
                }
            }
        }

        // OnLogAI wrapper if needed, but Event is public
        public void LogAIWrapper(string msg) { OnLogAI?.Invoke(msg); }

        // --- OPERATION MANAGEMENT ---

        public void SaveOperations()
        {
            try
            {
                var json = JsonSerializer.Serialize(_operations, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_opsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving operations: {ex.Message}");
            }
        }

        private void LoadOperations()
        {
            try
            {
                if (File.Exists(_opsFilePath))
                {
                    var json = File.ReadAllText(_opsFilePath);
                    _operations = JsonSerializer.Deserialize<List<Operation>>(json) ?? new List<Operation>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading operations: {ex.Message}");
                _operations = new List<Operation>();
            }
        }

        public Operation CreateOperationFromCortex(string title, string cortexReport)
        {
            var op = new Operation
            {
                Title = title,
                Objective = "Wisdom/Cortex iç görüsüne dayalı operasyon",
                CortexSynthesis = cortexReport,
                CortexReportId = Guid.NewGuid().ToString().Substring(0,8), // Default
                Status = OperationStatus.Draft,
                TargetAudience = "Genel Finans / X Kitlesi",
                StrategyNotes = "Wisdom Insight'tan otomatik olarak üretilmiştir."
            };
            op.TelegramContent = cortexReport; 
            
            // --- PARSE TWEETS FROM REPORT ---
            // Pattern: TWEET X (TYPE): Content
            try 
            {
                var lines = cortexReport.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                string currentTweet = "";
                
                foreach (var line in lines)
                {
                    // Detect "TWEET X" or "TWEET 1" start
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^TWEET\s?\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(currentTweet))
                        {
                            op.TwitterChain.Add(new OperationTweet { Text = currentTweet.Trim(), IsPosted = false });
                        }
                        // Start new tweet, remove the "TWEET 1 (HOOK):" prefix part
                        var parts = line.Split(':', 2);
                        currentTweet = parts.Length > 1 ? parts[1].Trim() : "";
                    }
                    else if (line.Trim().StartsWith("---") || line.Trim().StartsWith("Action:"))
                    {
                        // End of tweet section, probably metadata
                        continue;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(currentTweet))
                            currentTweet += "\n" + line;
                    }
                }
                // Add last one
                if (!string.IsNullOrWhiteSpace(currentTweet))
                {
                    op.TwitterChain.Add(new OperationTweet { Text = currentTweet.Trim(), IsPosted = false });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing tweets: {ex.Message}");
                 // Fallback: Add one tweet with everything if parsing fails
                 if (op.TwitterChain.Count == 0 && !string.IsNullOrEmpty(cortexReport))
                 {
                     op.TwitterChain.Add(new OperationTweet { Text = cortexReport, IsPosted = false });
                 }
            }
            
            _operations.Add(op);
            SaveOperations();
            return op;
        }

        public Operation? GetOperation(string id)
        {
            return _operations.FirstOrDefault(o => o.Id == id);
        }

        public void UpdateOperation(Operation op)
        {
            var existing = GetOperation(op.Id);
            if (existing != null)
            {
                _operations.Remove(existing);
                _operations.Add(op);
                SaveOperations();
            }
        }
        
        public List<Operation> GetActiveOperations()
        {
            return _operations.Where(o => o.Status != OperationStatus.Completed && o.Status != OperationStatus.Archived).ToList();
        }

        #region Auto Model Benchmark (v3.8.5)
        
        /// <summary>
        /// Initialize daily benchmark timer (runs at 03:00)
        /// </summary>
        public void InitializeDailyBenchmarkTimer()
        {
            // Calculate time until 03:00
            var now = DateTime.Now;
            var next3AM = now.Date.AddHours(3);
            if (now.Hour >= 3) next3AM = next3AM.AddDays(1);
            var delay = (next3AM - now).TotalMilliseconds;
            
            _dailyBenchmarkTimer = new System.Timers.Timer(delay);
            _dailyBenchmarkTimer.Elapsed += async (s, e) => {
                _dailyBenchmarkTimer.Interval = 24 * 60 * 60 * 1000; // Reset to 24h for subsequent runs
                await RunAutoModelBenchmarkAsync();
            };
            _dailyBenchmarkTimer.AutoReset = true;
            _dailyBenchmarkTimer.Start();
            
            Logger.Sys($"[ModelBenchmark] ⏰ Günlük benchmark zamanlayıcısı kuruldu. Sonraki çalışma: {next3AM:dd.MM HH:mm}");
        }
        
        /// <summary>
        /// Run automatic model benchmark - fetches latest models and tests them
        /// </summary>
        public async Task RunAutoModelBenchmarkAsync()
        {
            try
            {
                var apiKey = ConfigManager.Current.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    Logger.AI("[AutoBenchmark] ⚠️ Gemini API Key yok, benchmark atlanıyor.");
                    return;
                }
                
                Logger.AI("[AutoBenchmark] 🚀 Otomatik model benchmark başlatılıyor...");
                
                // 1. Fetch fresh model list from API
                var availableModels = await ModelBenchmarkService.FetchAvailableModelsAsync(apiKey);
                if (availableModels.Count == 0)
                {
                    Logger.AI("[AutoBenchmark] ⚠️ API'den model listesi alınamadı, varsayılanlar kullanılacak.");
                }
                else
                {
                    Logger.AI($"[AutoBenchmark] 📋 {availableModels.Count} model bulundu.");
                }
                
                // 2. Run benchmark — use live API model list if available, otherwise fallback
                var benchmark = new ModelBenchmarkService();
                var modelNames = availableModels.Count > 0
                    ? availableModels.ConvertAll(m => m.Name).ToArray()
                    : null;
                var results = await benchmark.RunBenchmarkAsync(apiKey, modelNames);
                
                // 3. Log results
                int successCount = results.Count(r => r.Success);
                Logger.AI($"[AutoBenchmark] ✅ Benchmark tamamlandı: {successCount}/{results.Count} model başarılı.");
                
                foreach (var r in results)
                {
                    string status = r.Success ? $"✅ {r.ResponseTimeMs}ms" : $"❌ {r.ErrorMessage}";
                    Logger.AI($"[AutoBenchmark]    • {r.ModelName}: {status}");
                }
                
                // 4. Get recommended model and update config
                var recommended = ModelBenchmarkService.GetRecommendedModel(results, "balanced");
                Logger.AI($"[AutoBenchmark] 💡 Önerilen model: {recommended}");
                
                // Update config if different
                if (ConfigManager.Current.GeminiModel != recommended)
                {
                    Logger.AI($"[AutoBenchmark] 🔄 Aktif model güncelleniyor: {ConfigManager.Current.GeminiModel} → {recommended}");
                    ConfigManager.Current.GeminiModel = recommended;
                    ConfigManager.Save();
                }
                
                // v5.1.6: ModelManager task preferences artık güncellenmeyecek.
                // Haber modülü hariç tüm modüller yerel modeli (LM Studio) kullanır.
                // Benchmark sadece ConfigManager.GeminiModel'i günceller (News REST API için).
                Logger.AI($"[AutoBenchmark] ℹ️ ModelManager tercihleri korunuyor (Yerel Model öncelikli).");
                
                _lastBenchmarkRun = DateTime.Now;
                Logger.AI($"[AutoBenchmark] ✅ Benchmark tamamlandı. Sonraki çalışma: yarın 03:00");
            }
            catch (Exception ex)
            {
                Logger.AI($"[AutoBenchmark] ❌ Hata: {ex.Message}");
            }
        }
        
        #endregion
    }
}
