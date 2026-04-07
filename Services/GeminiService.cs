using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.IO;

namespace XiDeAI_Pro.Services
{
    public class GeminiService
    {
        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly PromptManager _prompts = new PromptManager();
        private readonly MemoryEngine _memory;
        private readonly StatsEngine _stats;
        public string LastError { get; private set; } = "";
        
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; set; }
        
        private static readonly string TrainingDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XiDeAI", "training_data.jsonl");
        
        private void LogTrainingData(string prompt, string response, string taskType = "general")
        {
            try
            {
                var dir = Path.GetDirectoryName(TrainingDataPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                var entry = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    task = taskType,
                    prompt = prompt,
                    response = response
                };
                
                string jsonLine = JsonSerializer.Serialize(entry);
                File.AppendAllText(TrainingDataPath, jsonLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger.Sys($"⚠️ Training data log failed: {ex.Message}");
            }
        }

        public GeminiService(MemoryEngine memory, StatsEngine stats)
        {
            _memory = memory;
            _stats = stats;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        public async Task<string?> GenerateTweetContent(string symbol, string price, string score, string strategy, string trendList)
        {
            return await GenerateTweetContent(new SignalData { Symbol = symbol, Price = decimal.TryParse(price, out var p) ? p : 0, Score = int.TryParse(score.Split('/')[0], out var s) ? s : 0, Strategy = strategy }, "");
        }

        public async Task<string?> GenerateTweetContent(SignalData sig, string screenshotPath, string influencerCitations = "")
        {
            string context = _memory.GetSymbolContext(sig.Symbol);
            string indicatorContext = "";
            try
            {
                string indicatorGuidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                if (File.Exists(indicatorGuidePath))
                {
                    indicatorContext = File.ReadAllText(indicatorGuidePath);
                    indicatorContext = $"\n=== GÖSTERGE REHBERİ ===\n{indicatorContext}\n=== GÖSTERGE REHBERİ SONU ===\n\n";
                }
            }
            catch { }
            
            string scoreStr = $"{sig.Score}/{sig.MaxScore}";
            string prompt = _prompts.GetSignalAnalysisPrompt(sig.Symbol, sig.Strategy, scoreStr, sig.Price.ToString("N2"), indicatorContext, sig.Period, influencerCitations);

            if (!string.IsNullOrEmpty(context)) prompt += "\n\n" + context;

            string? result = await SendMultimodalRequest(prompt, screenshotPath);

            if (!string.IsNullOrEmpty(result))
            {
                _memory.StoreAnalysis(sig.Symbol, sig.Strategy, result);
            }
            return result;
        }

        public async Task<string?> GenerateMarketAnalysis(string symbol, string marketType)
        {
            string prompt = $@"Sen uzman bir finansal analist ve portföy yöneticisisin. {symbol} ({marketType}) için Kapsamlı Teknik ve Temel Analiz hazırla.";
            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateMarketAnalysisWithPrice(string symbol, string marketType, string priceContext, string influencerCitations = "", string newsContext = "", string marketOverview = "")
        {
            string indicatorContext = "";
            try
            {
                string indicatorGuidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                if (File.Exists(indicatorGuidePath))
                {
                    indicatorContext = File.ReadAllText(indicatorGuidePath);
                    indicatorContext = $"\n=== GÖSTERGE REHBERİ ===\n{indicatorContext}\n=== GÖSTERGE REHBERİ SONU ===\n\n";
                }
            }
            catch { }

            string prompt = _prompts.GetDeepManualAnalysisPrompt(symbol, marketType, priceContext, indicatorContext, influencerCitations, newsContext, marketOverview);
            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateMarketAnalysisWithChart(string symbol, string marketType, string priceContext, string screenshotPath, string influencerCitations = "", string newsContext = "", string marketOverview = "")
        {
            string prompt = _prompts.GetDeepManualAnalysisPrompt(symbol, marketType, priceContext, "", influencerCitations, newsContext, marketOverview);
            return await SendMultimodalRequest(prompt, screenshotPath);
        }

        public async Task<string?> AnalyzeNewsImpact(string title, string source)
        {
            string prompt = _prompts.GetNewsEditorPrompt(title, source);
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var result = await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsAnalysis, prompt);
                if (result == null) LastError = ModelManager.LastError;
                return result;
            }
            return await SendRequest(prompt);
        }

        public async Task<(List<(string Symbol, string Period)> Items, string TableName)> ParseGuruTableFromImage(string imageUrl)
        {
            var results = new List<(string Symbol, string Period)>();
            string tableName = "Teknik Tarama Listesi";
            try
            {
                byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);
                string prompt = @"Bu bir borsa tarama sonuç tablosudur. Lütfen 'Sembol' ve 'Periyot' sütunlarını oku. JSON formatında döndür: { ""TableName"": ""..."", ""Items"": [{""Symbol"": ""..."", ""Period"": ""...""}] }";

                string tempFile = Path.Combine(Path.GetTempPath(), $"guru_{Guid.NewGuid():N}.png");
                await File.WriteAllBytesAsync(tempFile, imageBytes);
                
                string? jsonResponse = null;
                try {
                    if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
                    {
                        jsonResponse = await ModelManager.SendRequestWithImage(XiDeAI_Pro.Services.AI.TaskType.GeneralAnalysis, prompt, tempFile);
                    }
                } finally {
                    try { File.Delete(tempFile); } catch {}
                }

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    int startIdx = jsonResponse.IndexOf('{');
                    int endIdx = jsonResponse.LastIndexOf('}');
                    if (startIdx >= 0 && endIdx > startIdx) jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
                    
                    using var doc = JsonDocument.Parse(jsonResponse);
                    if (doc.RootElement.TryGetProperty("TableName", out var tn)) tableName = tn.GetString() ?? tableName;
                    if (doc.RootElement.TryGetProperty("Items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            string sym = item.GetProperty("Symbol").GetString() ?? "";
                            string per = item.GetProperty("Period").GetString() ?? "";
                            if (!string.IsNullOrEmpty(sym)) results.Add((sym, per));
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.AI($"❌ ParseGuruTableFromImage Hatası: {ex.Message}"); }
            return (results, tableName);
        }

        public async Task<string?> GenerateGuruHonoringThread(string symbol, string period, string guruHandle, string originalTweetUrl, string tableName = "EFE HMA", string guruName = "Efelerin Efesi", string technicalContext = "", string? imagePath = null, string visualContext = "", string priceContext = "")
        {
            string prompt = _prompts.GetGuruHonoringThreadPrompt(symbol, tableName, "N/A", priceContext, technicalContext, guruName, guruHandle, $"{guruName} (@{guruHandle}) - {tableName}", visualContext);
            return await SendMultimodalRequest(prompt, imagePath);
        }

        public async Task<string> AnalyzeChartImage(string symbol, string imagePath)
        {
            if (!File.Exists(imagePath)) return "Grafik bulunamadı.";
            string prompt = $@"GÖREV: #{symbol} grafiğini incele. Trend, destek/direnç, formasyon ve indikatörleri raporla.";
            return await SendMultimodalRequest(prompt, imagePath) ?? "Görsel analiz yapılamadı.";
        }

        public async Task<bool> AnalyzePotentialGuru(string content, string author)
        {
            try
            {
                var prompt = $@"@{author} kullanıcısının şu içeriği hoca kalitesinde mi? İçerik: {content}. Sadece EVET veya HAYIR de.";
                string? response = await SendRequest(prompt, 0.3);
                return (response ?? "").Trim().ToUpper().Contains("EVET");
            }
            catch { return false; }
        }

        public async Task<string?> AnalyzeNewsForThread(string title, string source, string summary, string link = "", string? description = null, bool isFlash = false)
        {
            string category = await DetectNewsCategory(title, source);
            string prompt = _prompts.GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash);
            
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsThreadGeneration, prompt);
            }
            return await SendRequest(prompt, 0.3);
        }

        public async Task<string?> GeneratePerformanceSynthesis(DailyReport report)
        {
            var summaryData = new { report.TotalSignals, HitRate = report.TotalSignals > 0 ? (decimal)report.Winners / report.TotalSignals * 100 : 0, report.AvgReturn };
            string prompt = _prompts.GetPerformanceReportPrompt(JsonSerializer.Serialize(summaryData), report.BestPerformer?.Symbol ?? "N/A", report.WorstPerformer?.Symbol ?? "N/A");
            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateGenericContent(string prompt)
        {
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var res = await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.GeneralAnalysis, prompt);
                if (res == null) this.LastError = ModelManager.LastError;
                return res;
            }
            LastError = "ModelManager not initialized or Local Model disabled.";
            return null;
        }

        public async Task<string?> GenerateGenericContent(string prompt, XiDeAI_Pro.Services.AI.TaskType taskType)
        {
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var res = await ModelManager.SendRequest(taskType, prompt);
                if (res == null) this.LastError = ModelManager.LastError;
                return res;
            }
            LastError = "ModelManager not initialized or Local Model disabled.";
            return null;
        }

        public async Task<string?> SendRequest(string prompt, double temperature = 0.5, double topP = 0.95, int topK = 40, int maxOutputTokens = 2048)
        {
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var result = await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.GeneralAnalysis, prompt, maxTokens: maxOutputTokens);
                if (result == null) LastError = ModelManager.LastError;
                if (!string.IsNullOrWhiteSpace(result)) LogTrainingData(prompt, result, "text_generation");
                return result;
            }
            LastError = "Yerel model v5.0 ile zorunludur.";
            return null;
        }

        public async Task<string?> SendMultimodalRequest(string prompt, string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return await SendRequest(prompt);
            
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var result = await ModelManager.SendRequestWithImage(XiDeAI_Pro.Services.AI.TaskType.GeneralAnalysis, prompt, imagePath);
                if (result == null) LastError = ModelManager.LastError;
                if (!string.IsNullOrWhiteSpace(result)) LogTrainingData(prompt, result, "vision_analysis");
                return result;
            }
            LastError = "Görsel analizi v5.0 ile yerel model gerektirir.";
            return null;
        }

        public async Task<List<string>> GetAvailableModels(string apiKey)
        {
            return new List<string> { "gemini-2.5-flash", "gemini-2.5-pro", "gemini-1.5-pro" };
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                var providers = ModelManager.GetAvailableProviders();
                return providers.Count > 0 
                    ? (true, $"✅ Yerel Model Hazır: {providers[0].ProviderName}") 
                    : (false, "❌ Yerel model sağlayıcısı bulunamadı.");
            }
            return (false, "❌ Yerel model desteği kapalı.");
        }

        public async Task<string?> SynthesizeInfluencerAnalyses(string symbol, string marketType, string priceContext, string visualAnalysis, List<InfluencerPost> influencerPosts)
        {
            influencerPosts = influencerPosts ?? new List<InfluencerPost>();
            var influencerContext = influencerPosts.Count == 0 ? "Yorum yok." : string.Join("\n", influencerPosts.Select(p => $"@{p.Handle}: {p.Content}"));
            string prompt = _prompts.GetSignalSynthesisPrompt(symbol, priceContext, visualAnalysis, influencerContext, "");
            return await SendRequest(prompt);
        }

        public async Task<(string Category, string? Reply)> GenerateTwoStepReply(string tweetContent, string authorHandle)
        {
            string category = await DetectTweetCategory(tweetContent);
            string? reply = await GenerateCategorizedReply(category, tweetContent, authorHandle);
            return (category, reply);
        }

        public async Task<string?> GenerateMotivationTweet() { return await SendRequest(_prompts.GetMotivationPrompt()); }
        public async Task<string?> GenerateCloseSummary(string marketData) { return await SendRequest($"BIST100 kapanışını yorumla: {marketData}"); }
        public async Task<string?> GenerateReply(string tweetContent, string authorHandle) { return await SendRequest(_prompts.GetReplyPrompt(tweetContent, authorHandle)); }
        public async Task<string?> GenerateFanZoneReaction(string prompt) { return await SendRequest(prompt, 0.8, 0.95, 40, 150); }

        public async Task<string> DetectTweetCategory(string tweetContent)
        {
            var result = await SendRequest(_prompts.GetCategoryDetectionPrompt(tweetContent), 0.1);
            string category = result?.Trim().ToUpper() ?? "GUNLUK_MIZAH";
            return (new[] { "FINANS", "KULTUR_EGLENCE", "MILLI_TOPLUM", "BILGE_KULTUR", "INSAN_RUH", "GUNLUK_MIZAH" }).Contains(category) ? category : "GUNLUK_MIZAH";
        }

        public async Task<string?> GenerateCategorizedReply(string category, string tweetContent, string authorHandle)
        {
            var config = _prompts.GetCategoryConfig(category);
            return await SendRequest(_prompts.GetCategorizedReplyPrompt(category, tweetContent, authorHandle), config.Temp, config.TopP, config.TopK, config.MaxTokens);
        }

        public async Task<string> DetectNewsCategory(string title, string source)
        {
            var response = await SendRequest(_prompts.GetNewsCategoryDetectionPrompt(title, source), 0.1);
            string cleaned = response?.Trim().ToUpper().Split(' ')[0] ?? "EKONOMI";
            return (new[] { "EKONOMI", "SIYASET", "TEKNOLOJI", "GLOBAL", "KRIPTO", "SPOR", "YASAM" }).Contains(cleaned) ? cleaned : "EKONOMI";
        }

        public async Task<string?> AnalyzeNewsImpactTwoStep(string title, string source)
        {
            string category = await DetectNewsCategory(title, source);
            var config = _prompts.GetNewsCategoryConfig(category);
            return await SendRequest(_prompts.GetNewsEditorPromptV2(title, source, category), config.Temp, config.TopP, config.TopK, config.MaxTokens);
        }

        public async Task<string?> GenerateNewsCategoryAnalysis(string category, string title, string source, string link, string? description = null, bool isFlash = false)
        {
            var config = _prompts.GetNewsCategoryConfig(category);
            return await SendRequest(_prompts.GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash), config.Temp, config.TopP, config.TopK, config.MaxTokens);
        }

        public async Task<string?> GenerateStrategySpecificAnalysis(SignalData sig, string priceContext, string influencerCitations)
        {
            string prompt = _prompts.GetStrategySpecificPrompt(sig, priceContext, influencerCitations);
            int maxTokens = sig.Tier switch { ContentTier.Premium => 1500, ContentTier.Standard => 1000, ContentTier.Summary => 600, _ => 300 };
            return await SendRequest(prompt, maxOutputTokens: maxTokens);
        }

        public async Task<string?> GenerateMarketCloseTableTweet(string indicesData, string topGainers, string topLosers, string topVolume)
        {
            return await SendRequest(_prompts.GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume));
        }

        private string CleanTweetContent(string raw, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"http[s]?://\S+", "").Trim();
            return raw.Length > maxLength ? raw.Substring(0, maxLength) + "..." : raw;
        }

        public async Task<string?> GenerateShortThreadWithHistory(string symbol, string marketType, string priceContext, string visualAnalysis, string influencerContext, string periyot, string? screenshotPath = null, string lastWeekAnalysis = "")
        {
            string prompt = _prompts.GetShortThreadPromptWithHistory(symbol, marketType, priceContext, visualAnalysis, influencerContext, periyot, lastWeekAnalysis);
            return await SendMultimodalRequest(prompt, screenshotPath);
        }

        public string GetLastWeekSuccessfulAnalysis(string symbol)
        {
            try {
                if (_memory == null) return "";
                string context = _memory.GetSymbolContext(symbol);
                if (string.IsNullOrEmpty(context)) return "";
                if (context.Contains("hedef") || context.Contains("başarı") || context.Contains("tuttu")) return context.Length > 200 ? context.Substring(0, 200) + "..." : context;
                return "";
            } catch { return ""; }
        }
    }
}
