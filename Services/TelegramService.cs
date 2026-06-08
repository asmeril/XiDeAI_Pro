using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public class TelegramService
    {
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private string _baseUrl = "";
        
        // Rate Limiting
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private bool _isProcessingQueue = false;

        public TelegramService()
        {
            UpdateConfig();
            StartQueueProcessor();
        }

        public void UpdateConfig()
        {
            var token = ConfigManager.Current.TelegramBotToken;
            if (!string.IsNullOrEmpty(token))
            {
                _baseUrl = $"https://api.telegram.org/bot{token}/";
            }
        }

        private void StartQueueProcessor()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;

            Task.Run(async () =>
            {
                while (_isProcessingQueue)
                {
                    try
                    {
                        await _signal.WaitAsync(); // Wait for a signal (message enqueued)
                        
                        if (_messageQueue.TryDequeue(out var message))
                        {
                            await SendMessageInternalAsync(message);
                            // Rate Limit: Wait 1.5 seconds between messages to be safe
                            await Task.Delay(1500); 
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Telegram($"Queue Processor Error: {ex.Message}");
                    }
                }
            });
        }

        public async Task<(bool Success, string Message)> TestConnection()
        {
            UpdateConfig();
            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(ConfigManager.Current.TelegramChatId))
                return (false, "Bot Token veya Chat ID eksik.");

            try
            {
                var response = await _client.GetAsync(_baseUrl + "getMe");
                if (response.IsSuccessStatusCode)
                {
                    // For test, send immediately bypass queue to give instant feedback
                    var chatCheck = await SendMessageInternalWithResultAsync("🔔 X'iDeAI Test Mesajı: Bağlantı Başarılı!");
                    if (chatCheck.Success) return (true, "✅ Bot aktif ve test mesajı başarıyla Telegram'a gönderildi!");
                    
                    return (true, $"⚠️ Bot aktif (API Key doğru) AMA mesaj gönderilemedi.\n\nHata: {chatCheck.Error}\n\nOlası Sebepler:\n1. Chat ID hatalı olabilir.\n2. Eğer bu kendi ID'niz ise, Telegram'dan bota girip önce '/start' yazmalısınız.\n3. Eğer grup ise, botu gruba ekleyip yönetici yapmalısınız.");
                }
                return (false, $"❌ Bot Token hatalı. (HTTP {(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Bağlantı hatası: {ex.Message}");
            }
        }

        // Public method now enqueues instead of sending directly
        public Task<bool> SendMessageAsync(string message)
        {
            if (string.IsNullOrEmpty(_baseUrl)) return Task.FromResult(false);
            
            _messageQueue.Enqueue(message);
            _signal.Release(); // Signal the processor
            return Task.FromResult(true); // Always return true as it's queued
        }

        private async Task<(bool Success, string Error)> SendMessageInternalWithResultAsync(string message)
        {
            var chatId = ConfigManager.Current.TelegramChatId;
            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(chatId)) 
            {
                Logger.Telegram("⚠️ Mesaj gönderilemedi: BaseURL veya ChatID eksik.");
                return (false, "Chat ID boş veya kaydedilmemiş");
            }

            try
            {
                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "Markdown"
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(_baseUrl + "sendMessage", content);
                bool success = response.IsSuccessStatusCode;
                
                if (!success)
                {
                    string errorDetail = await response.Content.ReadAsStringAsync();
                    Logger.Telegram($"❌ Telegram API Hatası (sendMessage): {response.StatusCode} - {errorDetail}");

                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && errorDetail.Contains("parse entities", StringComparison.OrdinalIgnoreCase))
                    {
                        var plainPayload = new
                        {
                            chat_id = chatId,
                            text = message
                        };
                        string plainJson = JsonSerializer.Serialize(plainPayload);
                        var plainContent = new StringContent(plainJson, System.Text.Encoding.UTF8, "application/json");
                        var plainResponse = await _client.PostAsync(_baseUrl + "sendMessage", plainContent);
                        if (plainResponse.IsSuccessStatusCode)
                        {
                            Logger.Telegram("✅ Telegram mesajı Markdown olmadan gönderildi.");
                            return (true, "");
                        }
                        string plainError = await plainResponse.Content.ReadAsStringAsync();
                        Logger.Telegram($"❌ Telegram plain-text retry hatası: {plainResponse.StatusCode} - {plainError}");
                    }
                    
                    if ((int)response.StatusCode == 429)
                    {
                         Logger.Telegram("⏳ Rate Limit hit. Waiting extra 5 seconds...");
                         await Task.Delay(5000); 
                    }
                    
                    return (false, errorDetail);
                }
                
                return (true, "");
            }
            catch (Exception ex)
            {
                Logger.Telegram($"❌ Telegram SendMessage Hatası: {ex.Message}");
                return (false, ex.Message);
            }
        }
        
        private async Task<bool> SendMessageInternalAsync(string message)
        {
            var result = await SendMessageInternalWithResultAsync(message);
            return result.Success;
        }

        public class TelegramUpdateInfo
        {
            public string Text { get; set; } = "";
            public long Date { get; set; }
            public long UpdateId { get; set; }
        }

        public async Task<List<TelegramUpdateInfo>> GetUpdatesAsync(long offset)
        {
            if (string.IsNullOrEmpty(_baseUrl)) return new List<TelegramUpdateInfo>();

            try
            {
                // Fetch all updates from the given offset
                // v3.0.9: Using short timeout (1s) to work better with 3s Timer loop
                var response = await _client.GetAsync(_baseUrl + $"getUpdates?offset={offset}&timeout=1");
                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    Logger.Telegram($"⚠️ getUpdates Hatası: {response.StatusCode} - {err}");
                    return new List<TelegramUpdateInfo>();
                }

                string json = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(json)) return new List<TelegramUpdateInfo>();

                using var doc = JsonDocument.Parse(json);
                var updates = new List<TelegramUpdateInfo>();

                if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
                {
                    foreach (var updateItem in result.EnumerateArray())
                    {
                        long updateId = updateItem.GetProperty("update_id").GetInt64();
                        
                        // Check for regular message
                        if (updateItem.TryGetProperty("message", out var msg))
                        {
                            string text = "";
                            if (msg.TryGetProperty("text", out var t)) text = t.GetString() ?? "";
                            
                            long date = 0;
                            if (msg.TryGetProperty("date", out var d)) date = d.GetInt64();

                            updates.Add(new TelegramUpdateInfo { Text = text, Date = date, UpdateId = updateId });
                        }
                        // Check for channel post
                        else if (updateItem.TryGetProperty("channel_post", out var channelMsg))
                        {
                            string text = "";
                            if (channelMsg.TryGetProperty("text", out var t)) text = t.GetString() ?? "";
                            
                            long date = 0;
                            if (channelMsg.TryGetProperty("date", out var d)) date = d.GetInt64();

                            updates.Add(new TelegramUpdateInfo { Text = text, Date = date, UpdateId = updateId });
                        }
                    }
                }
                return updates;
            }
            catch (Exception ex)
            {
                Logger.Telegram($"❌ GetUpdates Hatası: {ex.Message}");
                return new List<TelegramUpdateInfo>();
            }
        }

        public async Task<string?> GetLastMessageAsync()
        {
             var updates = await GetUpdatesAsync(0);
             return updates.LastOrDefault()?.Text;
        }
    }
}
