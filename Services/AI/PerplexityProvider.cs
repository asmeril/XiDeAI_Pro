using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Perplexity AI provider implementation
    /// Specialized for real-time information and news analysis with source citations
    /// </summary>
    public class PerplexityProvider : IModelProvider
    {
        private static readonly HttpClient _client = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly Action<string> _logger;
        
        public string ProviderName => "Perplexity";
        public string ModelName { get; }
        public ModelTier Tier => ModelTier.Balanced;
        public string? LastError { get; private set; }
        /// <summary>
        /// Create Perplexity provider
        /// </summary>
        /// <param name="apiKey">Perplexity API key</param>
        /// <param name="modelName">Model name: "sonar" (recommended), "sonar-pro", or "sonar-reasoning"</param>
        /// <param name="logger">Logger action</param>
        public PerplexityProvider(string apiKey, string modelName, Action<string> logger)
        {
            _apiKey = apiKey;
            _modelName = modelName;
            ModelName = $"perplexity-{modelName}";
            _logger = logger;
            
            // v3.7.0: Removed global _client.DefaultRequestHeaders.Add("Authorization", ...)
            // We now add the header specifically to each HttpRequestMessage in SendRequest.
            // This prevents double 'Authorization' headers which causes 401 errors.
            
            // Masked log for verification
            string maskedKey = _apiKey.Length > 8 ? $"{_apiKey.Substring(0, 5)}...{_apiKey.Substring(_apiKey.Length - 4)}" : "****";
            _logger($"✅ Perplexity provider initialized with model: {_modelName} (Key: {maskedKey})");
        }
        
        public async Task<string?> SendRequest(string prompt, int maxTokens = 1000)
        {
            try
            {
                var url = "https://api.perplexity.ai/chat/completions";
                
                var requestBody = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Sen finansal piyasalar ve haberler konusunda uzman bir analistsin. Türkçe yanıt ver. Her zaman güncel bilgileri internetten araştır ve kaynakları göster."
                        },
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    max_tokens = maxTokens,
                    temperature = 0.7,
                    // Enable internet search and citations
                    search_domain_filter = new[] { "tr", "com" },
                    return_citations = true,
                    return_images = false
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Remove old auth header and add new one for this request
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = content;
                
                var response = await _client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger($"❌ Perplexity API Error: {response.StatusCode}");
                    _logger($"📄 Response Detail: {responseContent}");
                    return null;
                }
                
                var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;
                
                // Parse response
                string? result = null;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var messageContent))
                        {
                            result = messageContent.GetString();
                            
                            // Add citations if available
                            if (root.TryGetProperty("citations", out var citations) && citations.GetArrayLength() > 0)
                            {
                                var citationsList = new List<string>();
                                foreach (var citation in citations.EnumerateArray())
                                {
                                    var citationUrl = citation.GetString();
                                    if (!string.IsNullOrEmpty(citationUrl))
                                    {
                                        citationsList.Add(citationUrl);
                                    }
                                }
                                
                                if (citationsList.Count > 0)
                                {
                                    result += "\n\n📚 Kaynaklar:\n";
                                    for (int i = 0; i < Math.Min(citationsList.Count, 5); i++) // Max 5 sources
                                    {
                                        result += $"{i + 1}. {citationsList[i]}\n";
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger("⚠️ Perplexity boş yanıt döndü");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger($"❌ Perplexity Exception: {ex.Message}");
                return null;
            }
        }
        
        public Task<string?> SendRequestWithImage(string prompt, string imagePath, int maxTokens = 1000)
        {
            // Perplexity currently doesn't support image analysis
            // Fallback to text-only request
            _logger("⚠️ Perplexity doesn't support image analysis, using text-only mode");
            return SendRequest(prompt, maxTokens);
        }
        
        public bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }
        
        public decimal GetCostPer1KTokens()
        {
            // Perplexity pricing (as of Jan 2025)
            return _modelName switch
            {
                "sonar-pro" => 0.003m,      // $3 per 1M tokens
                "sonar" => 0.001m,          // $1 per 1M tokens
                "sonar-reasoning" => 0.005m, // $5 per 1M tokens
                _ => 0.001m
            };
        }
    }
}

