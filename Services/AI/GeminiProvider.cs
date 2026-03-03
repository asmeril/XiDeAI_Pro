using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Gemini AI provider implementation
    /// Supports multiple Gemini models (Flash, Pro, Pro 2.0)
    /// </summary>
    public class GeminiProvider : IModelProvider
    {
        private static readonly HttpClient _client = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly Action<string> _logger;
        
        public string ProviderName => "Gemini";
        public string ModelName { get; }
        public ModelTier Tier { get; }
        
        public GeminiProvider(string apiKey, string modelName, Action<string> logger)
        {
            _apiKey = apiKey;
            _modelName = modelName;
            ModelName = modelName;
            _logger = logger;
            
            // Auto-detect tier based on model name
            Tier = modelName.Contains("flash") ? ModelTier.UltraFast :
                   modelName.Contains("2.0") || modelName.Contains("2-0") ? ModelTier.Premium :
                   ModelTier.Balanced;
        }
        
        public async Task<string?> SendRequest(string prompt, int maxTokens = 1000)
        {
            try
            {
                // v4.6.10: CRITICAL FIX - Gemini 2.5 'thinking' models consume ~800 tokens internally as "thoughtsTokenCount".
                // Bumping minimum maxTokens to 4000 to prevent mid-word MAX_TOKENS truncation.
                if (maxTokens < 4000) maxTokens = 4000;

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    safetySettings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = maxTokens,
                        temperature = 0.7
                    }
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger($"❌ Gemini API Error: {response.StatusCode}");
                    return null;
                }
                
                // Parse JSON response
                var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;
                
                string? result = null;
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentElement))
                    {
                        if (contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var firstPart = parts[0];
                            if (firstPart.TryGetProperty("text", out var text))
                            {
                                result = text.GetString();
                                
                                // v4.6.9: CRITICAL FIX - Verify it's not a mid-generation safety abort
                                if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                                {
                                    string reason = finishReason.GetString() ?? "";
                                    if (reason == "SAFETY" || reason == "MAX_TOKENS" || reason == "RECITATION" || reason == "PROHIBITED_CONTENT")
                                    {
                                        _logger($"⚠️ Gemini generation chopped off mid-word due to API FinishReason: {reason}. Discarding partial content.");
                                        result = null; 
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Retry logic for empty responses
                if (string.IsNullOrWhiteSpace(result) || result == "null")
                {
                    _logger("⚠️ Gemini boş yanıt döndü, 2 saniye sonra tekrar deneniyor...");
                    await Task.Delay(2000);
                    
                    // Retry once
                    response = await _client.PostAsync(url, content);
                    responseContent = await response.Content.ReadAsStringAsync();
                    jsonDoc = JsonDocument.Parse(responseContent);
                    root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("candidates", out candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var contentElement2))
                        {
                            if (contentElement2.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                var firstPart = parts[0];
                                if (firstPart.TryGetProperty("text", out var text))
                                {
                                    result = text.GetString();
                                    
                                    if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                                    {
                                        string reason = finishReason.GetString() ?? "";
                                        if (reason == "SAFETY" || reason == "MAX_TOKENS" || reason == "RECITATION" || reason == "PROHIBITED_CONTENT")
                                        {
                                            _logger($"⚠️ [RETRY] Gemini generation chopped off due to: {reason}");
                                            result = null; 
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        // Check for Safety Block or other feedback
                        string failReason = "Gemini returned empty response (after retry).";
                        try {
                            if (root.TryGetProperty("promptFeedback", out var feedback)) {
                                if (feedback.TryGetProperty("blockReason", out var reason)) {
                                    failReason = $"Gemini BLOCKED: {reason.GetString()}";
                                } else if (feedback.TryGetProperty("safetyRatings", out var ratings)) {
                                    failReason = $"Gemini SAFETY FILTER triggered";
                                }
                            }
                        } catch {}
                        
                        _logger($"❌ {failReason}");
                    }
                    else
                    {
                        _logger("✅ Gemini 2. denemede başarılı yanıt aldı");
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger($"❌ Gemini Exception: {ex.Message}");
                return null;
            }
        }
        
        public async Task<string?> SendRequestWithImage(string prompt, string imagePath, int maxTokens = 1000)
        {
            try
            {
                // v4.6.10: CRITICAL FIX - Prevent MAX_TOKENS early cutoff
                if (maxTokens < 4000) maxTokens = 4000;

                if (!System.IO.File.Exists(imagePath))
                {
                    _logger($"❌ Image file not found: {imagePath}");
                    return null;
                }
                
                byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                string imageBase64 = Convert.ToBase64String(imageBytes);
                
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "image/png",
                                        data = imageBase64
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = maxTokens,
                        temperature = 0.7
                    }
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger($"❌ Gemini Vision API Error: {response.StatusCode}");
                    return null;
                }
                
                var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;
                
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentElement))
                    {
                        if (contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var firstPart = parts[0];
                            if (firstPart.TryGetProperty("text", out var text))
                            {
                                return text.GetString();
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger($"❌ Gemini Vision Exception: {ex.Message}");
                return null;
            }
        }
        
        public bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }
        
        public decimal GetCostPer1KTokens()
        {
            // Gemini pricing (as of Jan 2025)
            if (_modelName.Contains("flash"))
                return 0.00m; // Flash is FREE
            else if (_modelName.Contains("2.0") || _modelName.Contains("2-0"))
                return 0.0025m; // Pro 2.0: $2.50 per 1M tokens
            else
                return 0.00125m; // Pro 1.5: $1.25 per 1M tokens
        }
    }
}


