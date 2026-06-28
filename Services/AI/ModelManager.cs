using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Manages multiple AI model providers with intelligent selection and automatic fallback
    /// </summary>
    public class ModelManager
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // v3.5.2: Global AI Traffic Control
        private static readonly ConcurrentDictionary<string, (DateTime StoredAt, string Response)> _responseCache = new();
        private readonly Dictionary<string, IModelProvider> _providers = new();
        private readonly Dictionary<TaskType, List<string>> _taskModelPreferences = new();
        private readonly Action<string> _logger;
        
        /// <summary>
        /// Last error message encountered during AI operations
        /// </summary>
        public string? LastError { get; private set; }

        public ModelManager(Action<string> logger)
        {
            _logger = logger;
            InitializeTaskPreferences();
        }
        
        /// <summary>
        /// Register a model provider
        /// </summary>
        public void RegisterProvider(string name, IModelProvider provider)
        {
            _providers[name] = provider;
            _logger($"✅ Model provider registered: {provider.ProviderName} - {provider.ModelName} ({provider.Tier})");
        }
        
        /// <summary>
        /// Initialize task-to-model preferences (priority order for fallback)
        /// v5.0.0: GEMMA 4 (Local) only. Gemini and Perplexity removed.
        /// </summary>
        private void InitializeTaskPreferences()
        {
            // All tasks redirected to local LM Studio (gemma4)
            var localOnly = new List<string> { "lm-studio" };
            
            // v5.6.1: Use Gemini specifically for News tasks
            var geminiForNews = new List<string> { XiDeAI_Pro.Config.ConfigManager.Current.GeminiModel ?? "gemini-2.5-flash", "lm-studio" };

            _taskModelPreferences[TaskType.DeepScan] = localOnly;
            _taskModelPreferences[TaskType.NewsAnalysis] = geminiForNews;
            _taskModelPreferences[TaskType.NewsThreadGeneration] = geminiForNews;
            _taskModelPreferences[TaskType.ShortThreadGeneration] = localOnly;
            _taskModelPreferences[TaskType.FormationAnalysis] = localOnly;
            _taskModelPreferences[TaskType.TweetGeneration] = localOnly;
            _taskModelPreferences[TaskType.SmartQuote] = localOnly;
            _taskModelPreferences[TaskType.InfluencerReply] = localOnly;
            _taskModelPreferences[TaskType.SymbolResearch] = localOnly;
            _taskModelPreferences[TaskType.TrendTracking] = localOnly;
            _taskModelPreferences[TaskType.GeneralAnalysis] = localOnly;
            _taskModelPreferences[TaskType.MetaTeacherAnalysis] = localOnly;
            _taskModelPreferences[TaskType.PotentialGuruAnalysis] = localOnly;
            _taskModelPreferences[TaskType.FanZoneReaction] = localOnly;
            _taskModelPreferences[TaskType.ArGeAnalysis] = localOnly;
        }
        
        /// <summary>
        /// Send a text-only request with automatic model selection and fallback
        /// </summary>
        public async Task<string?> SendRequest(TaskType taskType, string prompt, int maxTokens = 4096)
        {
            string promptHash = ComputePromptHash(prompt);
            string cacheKey = $"text|{taskType}|{maxTokens}|{promptHash}";
            TimeSpan cacheTtl = GetCacheTtl(taskType, prompt);
            if (TryGetCached(cacheKey, cacheTtl, out var cached))
            {
                _logger($"♻️ AI cache hit: task={taskType}, hash={promptHash}, chars={cached.Length}");
                return cached;
            }

            await _semaphore.WaitAsync(); // v3.5.2: Ensure sequential execution across all providers
            try
            {
                LastError = null;
                if (TryGetCached(cacheKey, cacheTtl, out cached))
                {
                    _logger($"♻️ AI cache hit after wait: task={taskType}, hash={promptHash}, chars={cached.Length}");
                    return cached;
                }

                _logger($"🧾 AI request: task={taskType}, hash={promptHash}, chars={prompt.Length}, maxTokens={maxTokens}");

                // v3.6.6: Increased delay for better rate limit protection
                // Priority logic: Add extra delay for background tasks during business hours
                await ApplyPriorityDelay(taskType);
                await Task.Delay(1000).ConfigureAwait(false);

                if (!_taskModelPreferences.ContainsKey(taskType))
                {
                    _logger($"⚠️ Task type {taskType} not configured, using GeneralAnalysis");
                    taskType = TaskType.GeneralAnalysis;
                }
                
                var preferredModels = _taskModelPreferences[taskType];
                
                // Try each model in preference order (fallback mechanism)
                foreach (var modelName in preferredModels)
                {
                    if (!_providers.ContainsKey(modelName))
                    {
                        _logger($"⏭️ Model {modelName} not registered, skipping");
                        continue;
                    }
                    
                    var provider = _providers[modelName];
                    
                    if (!provider.IsAvailable())
                    {
                        _logger($"⏭️ Model {modelName} not available (no API key), trying next");
                        continue;
                    }
                    
                    try
                    {
                        _logger($"🤖 Using {provider.ProviderName} - {provider.ModelName} for {taskType}");
                        
                        var result = await provider.SendRequest(prompt, maxTokens);
                        
                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            _logger($"✅ {provider.ModelName} returned successful response ({result.Length} chars)");
                            CacheResponse(cacheKey, result);
                            return result;
                        }
                        else
                        {
                            LastError = provider.LastError ?? "Provider returned empty response";
                            _logger($"⚠️ {provider.ModelName} failed: {LastError}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = $"Exception: {ex.Message}";
                        _logger($"❌ {provider.ModelName} fatal failure: {LastError}");
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            if (string.IsNullOrEmpty(LastError)) LastError = $"Yerel modele ulaşılamıyor (LM Studio) - Görev: {taskType}";
            _logger($"❌ {LastError}");
            return null;
        }
        
        /// <summary>
        /// Send a request with image (for vision-capable models)
        /// </summary>
        public async Task<string?> SendRequestWithImage(TaskType taskType, string prompt, string imagePath, int maxTokens = 4096)
        {
            await _semaphore.WaitAsync();
            try
            {
                LastError = null;
                string promptHash = ComputePromptHash($"{prompt}|{imagePath}|{GetImageStamp(imagePath)}");
                _logger($"🧾 AI vision request: task={taskType}, hash={promptHash}, chars={prompt.Length}, image={System.IO.Path.GetFileName(imagePath)}, maxTokens={maxTokens}");

                // Small delay between requests to keep API keys healthy
                await Task.Delay(300).ConfigureAwait(false);

                var preferredModels = _taskModelPreferences.GetValueOrDefault(taskType, _taskModelPreferences[TaskType.GeneralAnalysis]);
                
                foreach (var modelName in preferredModels)
                {
                    if (!_providers.ContainsKey(modelName))
                        continue;
                    
                    var provider = _providers[modelName];
                    
                    if (!provider.IsAvailable())
                        continue;
                    
                    try
                    {
                        _logger($"🤖 Using {provider.ProviderName} - {provider.ModelName} for {taskType} (with image)");
                        
                        var result = await provider.SendRequestWithImage(prompt, imagePath, maxTokens);
                        
                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            _logger($"✅ {provider.ModelName} vision analysis successful");
                            return result;
                        }
                        else
                        {
                            LastError = provider.LastError ?? "Provider returned empty vision response";
                            _logger($"⚠️ {provider.ModelName} vision failed: {LastError}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = $"Vision Exception: {ex.Message}";
                        _logger($"❌ {provider.ModelName} vision fatal failure: {LastError}");
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            if (string.IsNullOrEmpty(LastError)) LastError = $"All vision models failed for task {taskType}";
            _logger($"❌ {LastError}");
            return null;
        }
        
        /// <summary>
        /// Get cost estimates for all registered providers
        /// </summary>
        public Dictionary<string, decimal> GetCostEstimates()
        {
            var costs = new Dictionary<string, decimal>();
            
            foreach (var kvp in _providers)
            {
                costs[kvp.Key] = kvp.Value.GetCostPer1KTokens();
            }
            
            return costs;
        }

        private async Task ApplyPriorityDelay(TaskType taskType)
        {
            var now = DateTime.Now;
            bool isBusinessHours = now.DayOfWeek != DayOfWeek.Saturday 
                                && now.DayOfWeek != DayOfWeek.Sunday
                                && now.Hour >= 9 && now.Hour < 20;

            if (isBusinessHours)
            {
                // Background tasks wait extra during business hours to favor Signals/Manual Analysis
                if (taskType == TaskType.MetaTeacherAnalysis || 
                    taskType == TaskType.ArGeAnalysis || 
                    taskType == TaskType.PotentialGuruAnalysis ||
                    taskType == TaskType.NewsAnalysis ||
                    taskType == TaskType.NewsThreadGeneration)
                {
                    // v4.10.3: Extra delay for news and background tasks to favor UI responsiveness
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }
        }
        
        /// <summary>
        /// Get all registered model providers
        /// </summary>
        public List<IModelProvider> GetAvailableProviders() => _providers.Values.ToList();
        
        /// <summary>
        /// Override model preference for a specific task type
        /// </summary>
        public void SetTaskPreference(TaskType taskType, List<string> modelNames)
        {
            _taskModelPreferences[taskType] = modelNames;
            _logger($"✅ Task preference updated for {taskType}: {string.Join(", ", modelNames)}");
        }

        private static string ComputePromptHash(string prompt)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(prompt ?? string.Empty));
            return Convert.ToHexString(bytes).Substring(0, 12).ToLowerInvariant();
        }

        private static bool TryGetCached(string cacheKey, TimeSpan ttl, out string cached)
        {
            cached = string.Empty;
            if (ttl <= TimeSpan.Zero) return false;
            if (!_responseCache.TryGetValue(cacheKey, out var entry)) return false;
            if (DateTime.UtcNow - entry.StoredAt > ttl)
            {
                _responseCache.TryRemove(cacheKey, out _);
                return false;
            }
            cached = entry.Response;
            return true;
        }

        private static void CacheResponse(string cacheKey, string response)
        {
            _responseCache[cacheKey] = (DateTime.UtcNow, response);
            if (_responseCache.Count > 500)
            {
                foreach (var oldKey in _responseCache.OrderBy(x => x.Value.StoredAt).Take(100).Select(x => x.Key))
                    _responseCache.TryRemove(oldKey, out _);
            }
        }

        private static TimeSpan GetCacheTtl(TaskType taskType, string prompt)
        {
            string p = prompt ?? string.Empty;
            if (taskType == TaskType.NewsAnalysis || taskType == TaskType.NewsThreadGeneration ||
                p.Contains("Baş Editörü", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("CATEGORY", StringComparison.OrdinalIgnoreCase))
                return TimeSpan.FromHours(24);

            if (taskType == TaskType.DeepScan || taskType == TaskType.TrendTracking || taskType == TaskType.SymbolResearch)
                return TimeSpan.FromMinutes(15);

            return TimeSpan.FromMinutes(5);
        }

        private static string GetImageStamp(string imagePath)
        {
            try
            {
                var info = new System.IO.FileInfo(imagePath);
                return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
