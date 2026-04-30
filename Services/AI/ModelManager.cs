using System;
using System.Collections.Generic;
using System.Linq;
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

            _taskModelPreferences[TaskType.DeepScan] = localOnly;
            _taskModelPreferences[TaskType.NewsAnalysis] = localOnly;
            _taskModelPreferences[TaskType.NewsThreadGeneration] = localOnly;
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
            await _semaphore.WaitAsync(); // v3.5.2: Ensure sequential execution across all providers
            try
            {
                LastError = null;
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
    }
}

