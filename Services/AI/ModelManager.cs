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
        /// IMPORTANT: Use valid Gemini API model identifiers:
        /// - gemini-2.0-flash (Fast, stable)
        /// - gemini-2.5-flash (Fast, stable)
        /// - gemini-2.5-pro (Balanced, high quality)
        /// - gemini-exp-1206 (Experimental Pro)
        /// </summary>
        private void InitializeTaskPreferences()
        {
            // Deep Scan - v4.5.3: Flash-first for rate limit safety
            _taskModelPreferences[TaskType.DeepScan] = new List<string> 
            { 
                "gemini-2.5-flash",      // 1st: Higher rate limit
                "gemini-2.0-flash"       // 2nd: Fallback
            };
            
            // News Analysis - v4.5.3: Flash-first to avoid rate limits
            _taskModelPreferences[TaskType.NewsAnalysis] = new List<string> 
            { 
                "gemini-2.5-flash",      // 1st: Higher rate limit, stable
                "gemini-2.0-flash"       // 2nd: Fallback
            };

            // News Thread Generation - v4.5.3: Flash-first for stability
            _taskModelPreferences[TaskType.NewsThreadGeneration] = new List<string>
            {
                "gemini-2.5-flash",      // 1st: Stable, good quality
                "gemini-2.0-flash"       // 2nd: Fallback
            };
            
            // Formation Analysis - Vision quality is critical
            _taskModelPreferences[TaskType.FormationAnalysis] = new List<string> 
            { 
                "gemini-2.5-pro",        // 1st: Best vision
                "gemini-2.5-flash",      // 2nd: Good vision
                "gemini-2.0-flash"       // 3rd: Basic vision
            };
            
            // Tweet Generation - v4.5.3: Pro-first for quality replies
            _taskModelPreferences[TaskType.TweetGeneration] = new List<string> 
            { 
                "gemini-2.5-pro",        // 1st: Best creativity and tone
                "gemini-2.5-flash"       // 2nd: Fallback
            };
            
            // Smart Quote - Simple text processing
            _taskModelPreferences[TaskType.SmartQuote] = new List<string> 
            { 
                "gemini-2.5-flash",      // 1st: Fast and sufficient
                "gemini-2.5-pro"         // 2nd: Fallback
            };
            
            // Influencer Reply - v4.5.3: Pro-first for quality interactions
            _taskModelPreferences[TaskType.InfluencerReply] = new List<string> 
            { 
                "gemini-2.5-pro",        // 1st: Best context understanding
                "gemini-2.5-flash"       // 2nd: Fallback
            };
            
            // Symbol Research - Real-time info is important
            _taskModelPreferences[TaskType.SymbolResearch] = new List<string> 
            { 
                "perplexity-sonar-pro",  // 1st: Most detailed research
                "perplexity-sonar",      // 2nd: Good research
                "gemini-2.5-flash"       // 3rd: Fallback
            };
            
            // Trend Tracking - Real-time info is CRITICAL
            _taskModelPreferences[TaskType.TrendTracking] = new List<string> 
            { 
                "perplexity-sonar",      // 1st: Real-time trends
                "perplexity-sonar-pro",  // 2nd: More detailed
                "gemini-2.0-flash"       // 3rd: Fallback
            };
            
            // General Analysis - v4.5.3: Pro-first for Manuel Analiz quality
            _taskModelPreferences[TaskType.GeneralAnalysis] = new List<string> 
            { 
                "gemini-2.5-pro",        // 1st: Best quality for analysis
                "gemini-2.5-flash"       // 2nd: Fallback
            };

            // Meta-Teacher Analysis - v4.5.3: Pro-first for Üstat quality
            _taskModelPreferences[TaskType.MetaTeacherAnalysis] = new List<string>
            {
                "gemini-2.5-pro",        // 1st: Best for technical analysis
                "gemini-2.5-flash"       // 2nd: Fallback
            };

            // Potential Guru Analysis - v4.5.3: Pro-first for guru quality
            _taskModelPreferences[TaskType.PotentialGuruAnalysis] = new List<string>
            {
                "gemini-2.5-pro",        // 1st: Best for analysis quality
                "gemini-2.5-flash"       // 2nd: Fallback
            };

            // FanZone Reaction - creative but specific
            _taskModelPreferences[TaskType.FanZoneReaction] = new List<string>
            {
                "gemini-2.5-flash",      // 1st: Fast and good tone
                "gemini-2.0-flash"       // 2nd: Fallback
            };

            // Ar-Ge Analysis - Deep understanding
            _taskModelPreferences[TaskType.ArGeAnalysis] = new List<string>
            {
                "gemini-2.5-pro",        // 1st: Best logic
                "gemini-2.0-flash"       // 2nd: Fallback
            };
        }
        
        /// <summary>
        /// Send a text-only request with automatic model selection and fallback
        /// </summary>
        public async Task<string?> SendRequest(TaskType taskType, string prompt, int maxTokens = 1000)
        {
            await _semaphore.WaitAsync(); // v3.5.2: Ensure sequential execution across all providers
            try
            {
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
                            _logger($"⚠️ {provider.ModelName} returned empty response, trying fallback");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger($"❌ {provider.ModelName} failed: {ex.Message}, trying fallback");
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            _logger($"❌ All models failed for task {taskType}");
            return null;
        }
        
        /// <summary>
        /// Send a request with image (for vision-capable models)
        /// </summary>
        public async Task<string?> SendRequestWithImage(TaskType taskType, string prompt, string imagePath, int maxTokens = 1000)
        {
            await _semaphore.WaitAsync();
            try
            {
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
                    }
                    catch (Exception ex)
                    {
                        _logger($"❌ {provider.ModelName} vision failed: {ex.Message}, trying fallback");
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            _logger($"❌ All vision models failed for task {taskType}");
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
                // Background tasks wait extra during business hours to favor Signals/News
                if (taskType == TaskType.MetaTeacherAnalysis || 
                    taskType == TaskType.ArGeAnalysis || 
                    taskType == TaskType.PotentialGuruAnalysis)
                {
                    // Wait an extra 2 seconds for low priority tasks
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

