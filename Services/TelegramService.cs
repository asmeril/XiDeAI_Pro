using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;
using System.Collections.Generic;

namespace XiDeAI_Pro.Services
{
    public class TelegramService
    {
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private string _baseUrl = "";

        public TelegramService()
        {
            UpdateConfig();
        }

        public void UpdateConfig()
        {
            var token = ConfigManager.Current.TelegramBotToken;
            if (!string.IsNullOrEmpty(token))
            {
                _baseUrl = $"https://api.telegram.org/bot{token}/";
            }
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
                    bool msgSent = await SendMessageAsync("🔔 X'iDeAI Test Mesajı: Bağlantı Başarılı!");
                    return msgSent ? (true, "✅ Bot aktif ve mesaj gönderildi!") : (true, "⚠️ Bot aktif ama mesaj gönderilemedi (Chat ID hatalı olabilir).");
                }
                return (false, $"❌ Bot Token hatalı. (HTTP {(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Bağlantı hatası: {ex.Message}");
            }
        }

        public async Task<bool> SendMessageAsync(string message)
        {
            var chatId = ConfigManager.Current.TelegramChatId;
            if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(chatId)) return false;

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
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telegram Send Error: {ex.Message}");
                return false;
            }
        }

        public class TelegramUpdateInfo
        {
            public string Text { get; set; } = "";
            public long Date { get; set; }
            public long UpdateId { get; set; }
        }

        public async Task<TelegramUpdateInfo?> GetLastUpdateAsync()
        {
            if (string.IsNullOrEmpty(_baseUrl)) return null;

            try
            {
                // Get updates with offset -1 to get the last message
                var response = await _client.GetAsync(_baseUrl + "getUpdates?offset=-1");
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("result", out var result) && result.GetArrayLength() > 0)
                {
                    var lastUpdate = result[result.GetArrayLength() - 1];
                    long updateId = lastUpdate.GetProperty("update_id").GetInt64();
                    
                    if (lastUpdate.TryGetProperty("message", out var msg))
                    {
                        string text = "";
                        if (msg.TryGetProperty("text", out var t)) text = t.GetString() ?? "";
                        
                        long date = 0;
                        if (msg.TryGetProperty("date", out var d)) date = d.GetInt64();

                        return new TelegramUpdateInfo { Text = text, Date = date, UpdateId = updateId };
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetLastMessageAsync()
        {
             var update = await GetLastUpdateAsync();
             return update?.Text;
        }
    }
}


