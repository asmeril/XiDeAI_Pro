using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Smart Model Benchmark Service - Tests all AI models and recommends the best one
    /// </summary>
    public class ModelBenchmarkService
    {
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private const string TEST_PROMPT = "Reply with only 'OK' - this is a connection test.";
        
        public class BenchmarkResult
        {
            public string ModelName { get; set; } = "";
            public bool Success { get; set; }
            public int ResponseTimeMs { get; set; }
            public string ErrorMessage { get; set; } = "";
            public decimal CostPer1KTokens { get; set; }
            public string Tier { get; set; } = "Unknown"; // Free, Paid, Premium
        }
        
        /// <summary>
        /// Fallback models if API fetch fails
        /// </summary>
        public static readonly string[] FallbackModels = new[]
        {
            "gemini-2.0-flash",
            "gemini-2.5-flash",
            "gemini-2.5-pro"
        };
        
        /// <summary>
        /// Model info from API
        /// </summary>
        public class ModelInfo
        {
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int InputTokenLimit { get; set; }
            public int OutputTokenLimit { get; set; }
        }
        
        /// <summary>
        /// Fetch available models from Gemini API dynamically
        /// </summary>
        public static async Task<List<ModelInfo>> FetchAvailableModelsAsync(string apiKey)
        {
            var models = new List<ModelInfo>();
            
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await _client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    return models;
                
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        // Only include models that support generateContent
                        if (model.TryGetProperty("supportedGenerationMethods", out var methods))
                        {
                            bool supportsGenerate = false;
                            foreach (var method in methods.EnumerateArray())
                            {
                                if (method.GetString() == "generateContent")
                                {
                                    supportsGenerate = true;
                                    break;
                                }
                            }
                            
                            if (supportsGenerate)
                            {
                                var name = model.GetProperty("name").GetString() ?? "";
                                // Remove "models/" prefix
                                if (name.StartsWith("models/"))
                                    name = name.Substring(7);
                                
                                // Filter out embedding, TTS, image-only, and video models
                                if (name.Contains("embedding") || name.Contains("tts") || 
                                    name.Contains("imagen") || name.Contains("veo") ||
                                    name.Contains("aqa") || name.Contains("gemma") ||
                                    name.Contains("image-generation"))
                                    continue;
                                
                                models.Add(new ModelInfo
                                {
                                    Name = name,
                                    DisplayName = model.GetProperty("displayName").GetString() ?? name,
                                    InputTokenLimit = model.TryGetProperty("inputTokenLimit", out var inp) ? inp.GetInt32() : 0,
                                    OutputTokenLimit = model.TryGetProperty("outputTokenLimit", out var outp) ? outp.GetInt32() : 0
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            
            return models;
        }
        
        /// <summary>
        /// Get model names as simple string array (for backward compatibility)
        /// </summary>
        public static async Task<string[]> GetAvailableModelNamesAsync(string apiKey)
        {
            var models = await FetchAvailableModelsAsync(apiKey);
            if (models.Count == 0)
                return FallbackModels;
            
            return models.ConvertAll(m => m.Name).ToArray();
        }
        
        /// <summary>
        /// Run benchmark on all available models.
        /// If modelsToTest is provided (from live API), uses those; otherwise falls back to FallbackModels.
        /// </summary>
        public async Task<List<BenchmarkResult>> RunBenchmarkAsync(string apiKey, string[]? modelsToTest = null)
        {
            var results = new List<BenchmarkResult>();
            var targets = (modelsToTest != null && modelsToTest.Length > 0) ? modelsToTest : FallbackModels;
            
            foreach (var model in targets)
            {
                var result = await TestModelAsync(apiKey, model);
                results.Add(result);
                
                // Small delay between tests to avoid rate limiting
                await Task.Delay(500);
            }
            
            return results;
        }
        
        /// <summary>
        /// Test a single model
        /// </summary>
        public async Task<BenchmarkResult> TestModelAsync(string apiKey, string modelName)
        {
            var result = new BenchmarkResult
            {
                ModelName = modelName,
                Tier = GetModelTier(modelName),
                CostPer1KTokens = GetModelCost(modelName)
            };
            
            var sw = Stopwatch.StartNew();
            
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = TEST_PROMPT } } }
                    },
                    generationConfig = new { maxOutputTokens = 10, temperature = 0.1 }
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _client.PostAsync(url, content);
                sw.Stop();
                
                result.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    result.Success = false;
                    result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {GetErrorMessage(errorContent)}";
                    return result;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                
                // Check for content
                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    result.Success = true;
                }
                else
                {
                    // Check for safety block
                    if (jsonDoc.RootElement.TryGetProperty("promptFeedback", out var feedback))
                    {
                        if (feedback.TryGetProperty("blockReason", out var reason))
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Blocked: {reason.GetString()}";
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = "Empty response";
                    }
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                result.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
                result.Success = false;
                result.ErrorMessage = "Timeout (30s)";
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get recommended model based on task type
        /// </summary>
        public static string GetRecommendedModel(List<BenchmarkResult> results, string priority = "balanced")
        {
            // Filter successful models
            var successful = results.FindAll(r => r.Success);
            if (successful.Count == 0) return FallbackModels[0]; // Fallback
            
            switch (priority.ToLower())
            {
                case "speed":
                    // Return fastest model
                    successful.Sort((a, b) => a.ResponseTimeMs.CompareTo(b.ResponseTimeMs));
                    return successful[0].ModelName;
                    
                case "cost":
                    // Return cheapest model
                    successful.Sort((a, b) => a.CostPer1KTokens.CompareTo(b.CostPer1KTokens));
                    return successful[0].ModelName;
                    
                case "balanced":
                default:
                    // Score = Speed * 0.5 + (1/Cost) * 0.5
                    // Prefer fast and cheap
                    foreach (var r in successful)
                    {
                        // Simple scoring: lower response time + lower cost = better
                        // Normalize: flash models get bonus
                        if (r.ModelName.Contains("flash") && r.ResponseTimeMs < 2000)
                            return r.ModelName;
                    }
                    return successful[0].ModelName;
            }
        }
        
        private static string GetModelTier(string modelName)
        {
            if (modelName.Contains("flash")) return "⚡ Hızlı";
            if (modelName.Contains("pro")) return "💎 Premium";
            if (modelName.Contains("exp")) return "🧪 Deneysel";
            return "📦 Standart";
        }
        
        private static decimal GetModelCost(string modelName)
        {
            // Costs per 1K tokens (approximate, as of Jan 2025)
            if (modelName.Contains("flash")) return 0.00m; // Free tier
            if (modelName.Contains("pro")) return 0.00125m;
            return 0.00m;
        }
        
        private static string GetErrorMessage(string jsonError)
        {
            try
            {
                var doc = JsonDocument.Parse(jsonError);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var msg))
                        return msg.GetString() ?? "Unknown error";
                }
            }
            catch { }
            return "API Error";
        }
    }
}
