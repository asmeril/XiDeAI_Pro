using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// LM Studio / LM Link Provider (OpenAI Compatible)
    /// Supports local and private-network LLMs via LM Studio.
    /// </summary>
    public class LMStudioProvider : IModelProvider
    {
        // Text-only requests: 300s for reasoning models on short prompts
        private static readonly HttpClient _client = new HttpClient() { Timeout = TimeSpan.FromSeconds(900) };
        // v4.5: Increased timeout for vision to 15 minutes (900s) as local vision + reasoning takes longer
        private static readonly HttpClient _visionClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(900) };
        private readonly string _uri;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly Action<string> _logger;

        public string ProviderName => "LMStudio";
        public string ModelName { get; }
        public ModelTier Tier => ModelTier.UltraFast; // Local is always fast
        public string? LastError { get; private set; }

        public LMStudioProvider(string uri, string apiKey, string modelName, Action<string> logger)
        {
            _uri = uri.TrimEnd('/');
            _apiKey = apiKey;
            _modelName = modelName;
            ModelName = modelName;
            _logger = logger;
            
            // Set default headers for OpenAI compatibility
            if (_client.DefaultRequestHeaders.Authorization == null && !string.IsNullOrEmpty(_apiKey))
            {
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
            if (_visionClient.DefaultRequestHeaders.Authorization == null && !string.IsNullOrEmpty(_apiKey))
            {
                _visionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<string?> SendRequest(string prompt, int maxTokens = 4096)
        {
            try
            {
                LastError = null;
                var url = $"{_uri}/chat/completions";
                
                // v5.1.7: Qwen3.6 /no_think'e rağmen reasoning_content'e düşünme yazıyor.
                // 4096 token tümüyle reasoning'e gidiyor → content boş kalıyor.
                // 16K token ile reasoning sızıntısına rağmen content için yeterli alan kalır.
                int effectiveMaxTokens = Math.Max(maxTokens, 16384);
                
                // v5.0.0: Prepend /no_think to suppress chain-of-thought on Qwen3/DeepSeek-R1.
                // LM Studio ignores the `thinking.budget_tokens` parameter, so this is the only
                // reliable way to prevent the model from consuming all tokens on reasoning.
                var requestBody = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new { role = "user", content = "/no_think\n" + prompt }
                    },
                    max_tokens = effectiveMaxTokens,
                    temperature = 0.2
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"LMStudio Error: {response.StatusCode} - {responseContent}";
                    _logger($"❌ {LastError}");
                    return null;
                }

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    // v5.1.3: finish_reason=length → token limiti aşıldı, uyar
                    if (choice.TryGetProperty("finish_reason", out var fr) && fr.GetString() == "length")
                    {
                        _logger("⚠️ [LMStudio] finish_reason=length — model token limitini aşarak kesildi. İçerik eksik olabilir.");
                        LastError = "LMStudio: finish_reason=length (token limit exceeded)";
                    }
                    return ExtractContentFromChoice(choice);
                }

                LastError = "LMStudio returned an empty response (no choices)";
                _logger($"⚠️ {LastError}. Raw response: {(responseContent.Length > 200 ? responseContent.Substring(0, 200) : responseContent)}");
                return null;
            }
            catch (Exception ex)
            {
                LastError = $"LMStudio Exception: {ex.Message}";
                _logger($"❌ {LastError}");
                return null;
            }
        }

        /// <summary>
        /// Resizes an image to fit within maxDimension x maxDimension and encodes as JPEG.
        /// This prevents LM Studio vision errors caused by oversized screenshots (e.g. 4K @ 4x DPI).
        /// </summary>
        private static (byte[] Data, string MimeType) PrepareImageForVision(string imagePath, int maxDimension = 1024)
        {
            using var original = Image.FromFile(imagePath);
            int origW = original.Width;
            int origH = original.Height;

            // Calculate target size preserving aspect ratio
            double scale = Math.Min((double)maxDimension / origW, (double)maxDimension / origH);
            int newW = scale < 1.0 ? (int)(origW * scale) : origW;
            int newH = scale < 1.0 ? (int)(origH * scale) : origH;

            using var resized = new Bitmap(newW, newH);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0, 0, newW, newH);
            }

            // Encode as JPEG with 85% quality
            var jpegParams = new EncoderParameters(1);
            jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.MimeType == "image/jpeg")
                ?? throw new InvalidOperationException("JPEG codec not found");

            using var ms = new MemoryStream();
            resized.Save(ms, jpegCodec, jpegParams);
            return (ms.ToArray(), "image/jpeg");
        }

        public async Task<string?> SendRequestWithImage(string prompt, string imagePath, int maxTokens = 4096)
        {
            try
            {
                LastError = null;
                if (!File.Exists(imagePath))
                {
                    LastError = $"Görsel bulunamadı: {imagePath}";
                    _logger($"❌ {LastError}");
                    return null;
                }

                // v5.0.2: Resize oversized screenshots (e.g. 4K @ 4x DPI = 10240x5760px) to max 1024px
                // and convert to JPEG to prevent "Invalid image at index 0" error in LM Studio/Gemma 4.
                var (imageBytes, mimeType) = PrepareImageForVision(imagePath, maxDimension: 1024);
                var base64Image = Convert.ToBase64String(imageBytes);
                _logger($"📷 Görsel hazırlandı: {imageBytes.Length / 1024}KB JPEG → LM Studio");

                var url = $"{_uri}/chat/completions";

                // v5.0.0: Prepend /no_think to suppress chain-of-thought on Qwen3/DeepSeek-R1.
                // LM Studio ignores budget_tokens; /no_think is the only reliable suppression.
                // Also raise max_tokens to 8192 minimum so output tokens aren't starved.
                int effectiveMaxTokens = Math.Max(maxTokens, 8192);
                var requestBody = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = "/no_think\n" + prompt },
                                new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                            }
                        }
                    },
                    max_tokens = effectiveMaxTokens,
                    temperature = 0.2
                };
                _logger($"🧠 [LMStudio Vision] max_tokens={effectiveMaxTokens}, /no_think aktif");

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _visionClient.PostAsync(url, content); // v4.10.5: 300s vision timeout
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"LMStudio Vision Error: {response.StatusCode} - {responseContent}";
                    _logger($"❌ {LastError}");
                    return null;
                }

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("finish_reason", out var fr) && fr.GetString() == "length")
                    {
                        _logger("⚠️ [LMStudio Vision] finish_reason=length — token limiti aşıldı.");
                        LastError = "LMStudio Vision: finish_reason=length (token limit exceeded)";
                    }
                    return ExtractContentFromChoice(choice);
                }

                LastError = "LMStudio Vision returned empty response";
                _logger($"⚠️ {LastError}. Raw response: {(responseContent.Length > 200 ? responseContent.Substring(0, 200) : responseContent)}");
                return null;
            }
            catch (Exception ex)
            {
                LastError = $"LMStudio Vision Exception: {ex.Message}";
                _logger($"❌ {LastError}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to extract content string from various possible OpenAI response structures. 
        /// Supports: choices[0].message.content (string), choices[0].message.content (array/parts), and choices[0].text
        /// </summary>
        private string? ExtractContentFromChoice(JsonElement choice)
        {
            // 1. Standard OpenAI Chat format: choices[0].message.content
            if (choice.TryGetProperty("message", out var messageElement))
            {
                if (messageElement.TryGetProperty("content", out var contentElement))
                {
                    // Case A: content is a string
                    if (contentElement.ValueKind == JsonValueKind.String)
                    {
                        var text = contentElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                    
                    // Case B: content is an array of parts (common in some vision/multimodal outputs)
                    if (contentElement.ValueKind == JsonValueKind.Array)
                    {
                        var sb = new StringBuilder();
                        foreach (var part in contentElement.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textProp))
                            {
                                sb.Append(textProp.GetString());
                            }
                        }
                        var arrayText = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(arrayText))
                            return arrayText;
                    }
                }

                // v5.1.7: reasoning_content AKILLI KURTARMA.
                // Qwen3.6 /no_think'e rağmen reasoning_content'e hem düşünce hem çıktı yazıyor.
                // content boşsa, reasoning_content'ten <think>...</think> bloklarını soyup
                // geriye kalan yararlı metni çıkarıyoruz. Eğer sadece düşünce varsa null döner.
                if (messageElement.TryGetProperty("reasoning_content", out var reasoningElement)
                    && reasoningElement.ValueKind == JsonValueKind.String)
                {
                    var reasoning = reasoningElement.GetString();
                    if (!string.IsNullOrWhiteSpace(reasoning))
                    {
                        // <think>...</think> bloklarını soy
                        string salvaged = System.Text.RegularExpressions.Regex.Replace(
                            reasoning, @"<think>[\s\S]*?</think>", "", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                        
                        // Eğer think bloğu kapatılmadan kaldıysa (token bitti), <think> sonrasını da sil
                        int openThinkIdx = salvaged.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                        if (openThinkIdx >= 0)
                            salvaged = salvaged.Substring(0, openThinkIdx).Trim();
                        
                        if (!string.IsNullOrWhiteSpace(salvaged) && salvaged.Length > 50)
                        {
                            _logger($"🔧 [LMStudio] content boş → reasoning_content'ten {salvaged.Length} karakter kurtarıldı.");
                            return salvaged;
                        }
                        
                        _logger("⚠️ [LMStudio] content boş, reasoning_content sadece düşünce içeriyor. Null döndürülüyor.");
                        return null;
                    }
                }
            }

            // 2. Fallback: Legacy/Completion format: choices[0].text
            if (choice.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString();
            }

            return null;
        }

        public bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_uri);
        }

        public decimal GetCostPer1KTokens()
        {
            return 0.00m; // Local compute is free!
        }
    }
}
