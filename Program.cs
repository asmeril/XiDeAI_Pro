using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using XiDeAI_Pro.Services;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Test mode check
            if (args.Length > 0 && args[0] == "--test")
            {
                RunTestMode().Wait();
                return;
            }

            // Normal UI mode
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            // Single-File publish fix: Assembly.Location is empty, use MainModule.FileName
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.GetCommandLineArgs()[0];
            var buildDate = File.GetLastWriteTime(exePath);
            Logger.Sys($"Uygulama başlatıldı (v{version} - Build: {buildDate:dd.MM.yyyy HH:mm})");
            
            try 
            {
                System.Windows.Forms.Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                // Catch any unhandled exception during startup
                System.Windows.Forms.MessageBox.Show(
                    $"UYGULAMA BAŞLATMA HATASI:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "KRİTİK HATA", 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Error
                );
            }
        }

        static async Task RunTestMode()
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Console.WriteLine("===========================================");
            Console.WriteLine("   iDeal Smart Notifier - TEST MODE");
            Console.WriteLine("===========================================\n");

            // Load config
            ConfigManager.Load();
            var cfg = ConfigManager.Current;

            // Test data for THYAO (with BONUS SCORE!)
            var signal = new SignalData
            {
                Symbol = "THYAO",
                Strategy = "King+Bomba",
                Period = "60",
                Price = 324.50m,
                Score = 27,  // BONUS SKOR! (Max 25)
                MaxScore = 25,
                Source = "KING"
            };

            Console.WriteLine($"📊 Test Sinyali: {signal.Symbol}");
            Console.WriteLine($"   Strateji: {signal.Strategy}");
            Console.WriteLine($"   Periyot: {signal.Period}dk");
            Console.WriteLine($"   Fiyat: {signal.Price:N2} TL");
            Console.WriteLine($"   Skor: {signal.Score}/{signal.MaxScore}\n");

            // 1. Spam Protection Check
            Console.WriteLine("🛡️ Spam Koruması Kontrolü...");
            var spamProtection = new SpamProtection(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tweet_log.json"));
            if (spamProtection.CanTweet(signal.Symbol, signal.Strategy, out string reason))
            {
                Console.WriteLine("   ✅ Geçti - Tweet atılabilir\n");
            }
            else
            {
                Console.WriteLine($"   ❌ Engellendi: {reason}\n");
            }

            // 2. TradingView Link
            string chartId = cfg.TradingViewChartId;
            string interval = signal.Period == "G" ? "D" : signal.Period;
            string tvLink = $"https://tr.tradingview.com/chart/{chartId}/?symbol=BIST:{signal.Symbol}&interval={interval}";
            Console.WriteLine($"📈 TradingView Link:");
            Console.WriteLine($"   {tvLink}\n");

            // 3. Trend Service
            Console.WriteLine("🔥 Trend Hashtag'leri:");
            var trendService = new TrendService();
            string symbolTag = trendService.GetSymbolHashtag(signal.Symbol);
            string trends = cfg.DailyTrends;
            if (string.IsNullOrEmpty(trends)) trends = "#BIST100 #Borsa";
            Console.WriteLine($"   {symbolTag} {trends}\n");

            // 4. AI Tweet Generation (if API key exists)
            string? aiText = null;
            if (!string.IsNullOrEmpty(cfg.GeminiApiKey))
            {
                Console.WriteLine("🤖 Gemini AI Tweet Oluşturuyor...");
                string hiddenKey = cfg.GeminiApiKey.Length > 10 ? cfg.GeminiApiKey.Substring(0, 10) + "..." : "******";
                Console.WriteLine($"   API Key: {hiddenKey}");
                try
                {
                    string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                    var stats = new StatsEngine(Path.Combine(appDataDir, "stats.json"));
                    var memory = new MemoryEngine(Path.Combine(appDataDir, "memory.json"));
                    
                    var gemini = new GeminiService(memory, stats);
                    aiText = await gemini.GenerateTweetContent(
                        signal.Symbol, 
                        signal.Price.ToString("N2"), 
                        signal.Score.ToString(), 
                        signal.Strategy, 
                        trends
                    );
                    
                    if (!string.IsNullOrEmpty(aiText))
                    {
                        Console.WriteLine("   ✅ AI Yanıtı Alındı:");
                        Console.WriteLine("   ─────────────────────────");
                        Console.WriteLine($"   {aiText}");
                        Console.WriteLine("   ─────────────────────────\n");
                    }
                    else
                    {
                        Console.WriteLine("   ⚠️ AI boş yanıt döndü.\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ AI Hatası: {ex.Message}\n");
                }
            }
            else
            {
                Console.WriteLine("⚠️ Gemini API Key yok, varsayılan format kullanılacak.\n");
            }

            // 5. THREAD TEST - Tüm parçaları göster
            Console.WriteLine("🧵 THREAD ÖN İZLEME (4 Parça)");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            // Thread Part 1
            string thread1 = $"🧵 THREAD: #{signal.Symbol} Sinyali Detaylı Analiz\n\n" +
                           $"🚨 {signal.Strategy} stratejimiz #{signal.Symbol}'de\n" +
                           $"🔥 {signal.Score}/{signal.MaxScore} gibi ÇOK YÜKSEK bir skor yakaladı!\n\n" +
                           $"💰 Fiyat: {signal.Price:N2} TL\n" +
                           $"⏱️ Periyot: {signal.Period}dk\n\n" +
                           $"👇 Thread'de daha fazlası var!\n" +
                           $"❤️ Beğen + 🔁 RT ile destek ol";
            Console.WriteLine("📱 TWEET 1/4 - HOOK + CTA:");
            Console.WriteLine("───────────────────────────────────────────────────");
            Console.WriteLine(thread1);
            Console.WriteLine($"📏 {thread1.Length} karakter\n");

            // Thread Part 2 - AI Technical Analysis
            Console.WriteLine("📱 TWEET 2/4 - TEKNİK ANALİZ (AI):");
            Console.WriteLine("───────────────────────────────────────────────────");
            string thread2Analysis = "";
            if (!string.IsNullOrEmpty(cfg.GeminiApiKey))
            {
                try
                {
                    var analyzePrompt = $@"Sen bir teknik analistsin. Şu sinyal için kısa teknik analiz yaz:
Hisse: {signal.Symbol}, Fiyat: {signal.Price} TL, Skor: {signal.Score}/{signal.MaxScore}
Kurallar: 4-5 madde halinde, emoji kullan, max 300 karakter";
                    
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var body = new { contents = new[] { new { parts = new[] { new { text = analyzePrompt } } } } };
                    var jsonContent = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(body),
                        System.Text.Encoding.UTF8, "application/json");
                    var resp = await client.PostAsync(
                        $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key={cfg.GeminiApiKey}",
                        jsonContent);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        thread2Analysis = doc.RootElement.GetProperty("candidates")[0]
                                            .GetProperty("content").GetProperty("parts")[0]
                                            .GetProperty("text").GetString() ?? "";
                    }
                }
                catch { }
            }
            if (string.IsNullOrEmpty(thread2Analysis))
                thread2Analysis = $"📈 Güçlü momentum sinyali\n💹 Hacim ortalamanın üstünde\n🎯 Skor çok güçlü";
            
            string thread2 = $"📊 TEKNİK ANALİZ\n\n{thread2Analysis}\n\n📈 Grafik: {tvLink}\n\n⬇️ X'te ne deniyor? Devamı aşağıda...";
            Console.WriteLine(thread2);
            Console.WriteLine($"📏 {thread2.Length} karakter\n");

            // Thread Part 3 - X Comments
            Console.WriteLine("📱 TWEET 3/4 - X YORUMLARI (AI Simülasyon):");
            Console.WriteLine("───────────────────────────────────────────────────");
            string xComments = $"💬 @trader_pro: \"#{signal.Symbol} momentum çok güçlü!\"\n" +
                              $"💬 @analist_x: \"Hacim patlaması var, izlemeye devam\"\n" +
                              $"💬 @borsa_takip: \"Hedef fiyat yaklaşıyor\"";
            string thread3 = $"💬 X'TE NE DENİYOR?\n\n{xComments}\n\n⬇️ Sonuç ve öneriler aşağıda...";
            Console.WriteLine(thread3);
            Console.WriteLine($"📏 {thread3.Length} karakter\n");

            // Thread Part 4 - Conclusion + CTA
            Console.WriteLine("📱 TWEET 4/4 - SONUÇ + CTA:");
            Console.WriteLine("───────────────────────────────────────────────────");
            string thread4 = $"✅ SONUÇ\n\n#{signal.Symbol} için {signal.Strategy} stratejisi\n" +
                           $"🎯 Skor: {signal.Score}/{signal.MaxScore}\n\n" +
                           $"📌 Yeni sinyaller için takipte kal + 🔔\n" +
                           $"❤️ Beğen + 🔁 RT ile destek ol\n\n" +
                           $"{trends}\n#{signal.Symbol}\n\n⚠️ Yatırım tavsiyesi değildir.";
            Console.WriteLine(thread4);
            Console.WriteLine($"📏 {thread4.Length} karakter\n");

            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("✅ Thread test tamamlandı! (Gönderilmedi)\n");

            // 6. Manual Analysis Test
            Console.WriteLine("🤖 MANUEL ANALİZ TESTİ (THYAO - BIST)...");
            Console.WriteLine("───────────────────────────────────────────────────");
            
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
            var st = new StatsEngine(Path.Combine(baseDir, "stats.json"));
            var mem = new MemoryEngine(Path.Combine(baseDir, "memory.json"));
            
            var geminiSvc = new GeminiService(mem, st);
            var screenshotSvc = new ScreenshotService(
                Path.Combine(AppContext.BaseDirectory, "scripts", "screenshot.py"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI", "screenshots")
            );
            var socialIntelSvc = new SocialIntelService();
            var influencerControl = new InfluencerControlService();
            var manualService = new ManualAnalysisService(geminiSvc, screenshotSvc, socialIntelSvc, influencerControl, mem, (msg) => Console.WriteLine("[TEST-LOG] " + msg));
            var manualResult = await manualService.PerformManualAnalysis("THYAO", "BIST", "60");
            string manualAnalysis = manualResult.ReportText;
            Console.WriteLine(manualAnalysis);
            Console.WriteLine("───────────────────────────────────────────────────\n");

            Console.WriteLine("───────────────────────────────────────────────────\n");

            // 7. Auto-News Tracker Test (Mock)
            Console.WriteLine("📰 NEWS TRACKER TESTİ (Fed Faiz Kararı)...");
            Console.WriteLine("───────────────────────────────────────────────────");
            string testNewsTitle = "Fed faizleri sabit bıraktı, piyasa olumlu karşıladı";
            string testNewsSource = "CNBC";
            Console.WriteLine($"Haber: {testNewsTitle} ({testNewsSource})");
            Console.WriteLine("AI Analizi Bekleniyor...");
            string? newsAnalysis = await geminiSvc.AnalyzeNewsImpact(testNewsTitle, testNewsSource);
            Console.WriteLine($"SONUÇ:\n{newsAnalysis}");
            Console.WriteLine("───────────────────────────────────────────────────\n");

            // 8. Final Single Tweet (eski format)
            string finalTweet;
            if (!string.IsNullOrEmpty(aiText))
            {
                finalTweet = aiText.Replace("[LINK]", tvLink);
            }
            else
            {
                finalTweet = $@"🚀 {symbolTag} Sinyali!
📊 {signal.Strategy} | {signal.Period}dk
💰 Fiyat: {signal.Price:N2} TL
🔥 Skor: {signal.Score}/{signal.MaxScore}
📈 {tvLink}

{trends}

⚠️ Yatırım tavsiyesi değildir.";
            }

            Console.WriteLine("===========================================");
            Console.WriteLine("   📱 HAZIR TWEET");
            Console.WriteLine("===========================================");
            Console.WriteLine(finalTweet);
            Console.WriteLine("===========================================");
            Console.WriteLine($"📏 Karakter: {finalTweet.Length}/280");
            Console.WriteLine("\n✅ Test tamamlandı! (Tweet gönderilmedi)");
            Console.WriteLine("\nÇıkmak için bir tuşa basın...");
            Console.ReadKey();
        }
    }
}

