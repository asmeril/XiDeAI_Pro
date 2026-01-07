using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Manages multiple AI model providers with intelligent selection and automatic fallback
    /// </summary>
    public class ModelManager
    {
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
        /// </summary>
        private void InitializeTaskPreferences()
        {
            // Deep Scan - Speed is critical, accuracy is binary (yes/no)
            _taskModelPreferences[TaskType.DeepScan] = new List<string> 
            { 
                "gemini-flash",          // 1st: FREE, very fast
                "gemini-flash-1.5",      // 2nd: Fallback
                "gemini-pro"             // 3rd: Last resort
            };
            
            // News Analysis - Real-time info is CRITICAL
            _taskModelPreferences[TaskType.NewsAnalysis] = new List<string> 
            { 
                "perplexity-sonar",      // 1st: Real-time + sources
                "perplexity-sonar-pro",  // 2nd: More detailed
                "gemini-flash",          // 3rd: Fast fallback (not real-time)
                "gemini-pro"             // 4th: Last resort
            };
            
            // Formation Analysis - Vision quality is critical
            _taskModelPreferences[TaskType.FormationAnalysis] = new List<string> 
            { 
                "gemini-pro-2.0",        // 1st: Best vision
                "gemini-pro-1.5",        // 2nd: Good vision
                "gemini-flash"           // 3rd: Basic vision
            };
            
            // Tweet Generation - Creativity and tone
            _taskModelPreferences[TaskType.TweetGeneration] = new List<string> 
            { 
                "gemini-pro-1.5",        // 1st: Good creativity
                "gemini-flash",          // 2nd: Fast fallback
                "perplexity-sonar"       // 3rd: Can add real-time context
            };
            
            // Smart Quote - Simple text processing
            _taskModelPreferences[TaskType.SmartQuote] = new List<string> 
            { 
                "gemini-flash",          // 1st: Fast and sufficient
                "gemini-pro"             // 2nd: Fallback
            };
            
            // Influencer Reply - Context and tone are important
            _taskModelPreferences[TaskType.InfluencerReply] = new List<string> 
            { 
                "gemini-pro-1.5",        // 1st: Good context understanding
                "gemini-flash",          // 2nd: Fast fallback
                "perplexity-sonar"       // 3rd: Can research context
            };
            
            // Symbol Research - Real-time info is important
            _taskModelPreferences[TaskType.SymbolResearch] = new List<string> 
            { 
                "perplexity-sonar-pro",  // 1st: Most detailed research
                "perplexity-sonar",      // 2nd: Good research
                "gemini-pro-1.5"         // 3rd: Fallback
            };
            
            // Trend Tracking - Real-time info is CRITICAL
            _taskModelPreferences[TaskType.TrendTracking] = new List<string> 
            { 
                "perplexity-sonar",      // 1st: Real-time trends
                "perplexity-sonar-pro",  // 2nd: More detailed
                "gemini-flash"           // 3rd: Fallback
            };
            
            // General Analysis - Balanced quality/cost
            _taskModelPreferences[TaskType.GeneralAnalysis] = new List<string> 
            { 
                "gemini-pro-1.5",        // 1st: Good quality
                "gemini-flash",          // 2nd: Fast and cheap
                "perplexity-sonar"       // 3rd: Can add context
            };
        }
        
        /// <summary>
        /// Send a text-only request with automatic model selection and fallback
        /// </summary>
        public async Task<string?> SendRequest(TaskType taskType, string prompt, int maxTokens = 1000)
        {
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
            
            _logger($"❌ All models failed for task {taskType}");
            return null;
        }
        
        /// <summary>
        /// Send a request with image (for vision-capable models)
        /// </summary>
        public async Task<string?> SendRequestWithImage(TaskType taskType, string prompt, string imagePath, int maxTokens = 1000)
        {
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
        
        /// <summary>
        /// Get list of available providers
        /// </summary>
        public List<string> GetAvailableProviders()
        {
            return _providers
                .Where(p => p.Value.IsAvailable())
                .Select(p => $"{p.Value.ProviderName} - {p.Value.ModelName}")
                .ToList();
        }
        
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

