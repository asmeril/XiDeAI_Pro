using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class TwitterService
    {
        public string LastError { get; private set; } = "";
        private readonly SocialIntelService _socialIntel;
        private Microsoft.Web.WebView2.WinForms.WebView2? _webView;

        public TwitterService()
        {
            _socialIntel = new SocialIntelService();
        }

        public void RegisterWebView(Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {
            _webView = webView;
        }

        /// <summary>
        /// Sends a tweet using Selenium (Cookies) primarily, to bypass API limits.
        /// Falls back to API if strictly necessary or configured.
        /// </summary>
        public async Task<bool> SendTweetAsync(string text)
        {
            // 1. Try Selenium (External Browser - Headless)
            try 
            {
                var result = await _socialIntel.PostTweet(text);
                if (result != null && result.status == "success")
                {
                    // Increment Total Counts (Selenium)
                    var cfg = ConfigManager.Current;
                    cfg.CheckReset();
                    cfg.DailyTotalTweetCount++;
                    cfg.MonthlyTotalTweetCount++;
                    ConfigManager.Save();
                    return true;
                }
                
                LastError = "Selenium Error: " + result?.ErrorMessage;
            }
            catch (Exception ex)
            {
                LastError = "Selenium Exception: " + ex.Message;
            }
            
            // 2. Fallback to API
            return SendTweet(text);
        }

        public bool SendTweet(string text)
        {
            try
            {
                var settings = ConfigManager.Current;
                if (string.IsNullOrEmpty(settings.TwitterApiKey) || string.IsNullOrEmpty(settings.TwitterAccessToken))
                {
                    LastError = "API Keys Missing";
                    return false;
                }

                var oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();
                var oauth_url = "https://api.twitter.com/2/tweets";

                var dict = new SortedDictionary<string, string>();
                dict.Add("oauth_consumer_key", settings.TwitterApiKey);
                dict.Add("oauth_nonce", oauth_nonce);
                dict.Add("oauth_signature_method", "HMAC-SHA1");
                dict.Add("oauth_timestamp", oauth_timestamp);
                dict.Add("oauth_token", settings.TwitterAccessToken);
                dict.Add("oauth_version", "1.0");

                string baseString = "POST&" + Uri.EscapeDataString(oauth_url) + "&" +
                                    Uri.EscapeDataString(string.Join("&", dict.Select(kvp => kvp.Key + "=" + Uri.EscapeDataString(kvp.Value))));

                string compositeKey = Uri.EscapeDataString(settings.TwitterApiSecret) + "&" + Uri.EscapeDataString(settings.TwitterTokenSecret);

                string oauth_signature;
                using (HMACSHA1 hasher = new HMACSHA1(Encoding.ASCII.GetBytes(compositeKey)))
                {
                    oauth_signature = Convert.ToBase64String(hasher.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
                }

                string authHeader = string.Format(
                    "OAuth oauth_consumer_key=\"{0}\", oauth_nonce=\"{1}\", oauth_signature=\"{2}\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"{3}\", oauth_token=\"{4}\", oauth_version=\"1.0\"",
                    Uri.EscapeDataString(settings.TwitterApiKey),
                    Uri.EscapeDataString(oauth_nonce),
                    Uri.EscapeDataString(oauth_signature),
                    Uri.EscapeDataString(oauth_timestamp),
                    Uri.EscapeDataString(settings.TwitterAccessToken)
                );

#pragma warning disable SYSLIB0014
                var request = (HttpWebRequest)WebRequest.Create(oauth_url);
#pragma warning restore SYSLIB0014
                request.Method = "POST";
                request.Headers.Add("Authorization", authHeader);
                request.ContentType = "application/json";

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    // Use proper JSON serialization to handle special characters
                    var payload = new { text = text };
                    string json = System.Text.Json.JsonSerializer.Serialize(payload);
                    streamWriter.Write(json);
                }

                using (var response = request.GetResponse())
                {
                    // Update Quota Counters
                    ConfigManager.Current.CheckReset();
                    ConfigManager.Current.DailyTweetCount++;
                    ConfigManager.Current.DailyTotalTweetCount++; // Total includes API too
                    ConfigManager.Current.MonthlyTweetCount++;
                    ConfigManager.Current.MonthlyTotalTweetCount++; // Total includes API too
                    ConfigManager.Save();
                    
                    return true;
                }
            }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse httpResponse)
                {
                    if ((int)httpResponse.StatusCode == 429)
                    {
                        LastError = "🛑 X KOTASI DOLDU (429): Günlük veya saatlik gönderim limitine ulaşıldı. Lütfen daha sonra deneyin.";
                        return false;
                    }
                    
                    using var reader = new StreamReader(httpResponse.GetResponseStream());
                    var error = reader.ReadToEnd();
                    LastError = $"API Error ({(int)httpResponse.StatusCode}): {error}";
                    return false;
                }
                LastError = wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Test connection to X/Twitter API by verifying credentials
        /// </summary>
        public (bool Success, string Message) TestConnection()
        {
            var settings = ConfigManager.Current;
            
            // Check if keys are filled
            if (string.IsNullOrEmpty(settings.TwitterApiKey))
                return (false, "❌ API Key (Consumer Key) boş!");
            
            if (string.IsNullOrEmpty(settings.TwitterApiSecret))
                return (false, "❌ API Secret (Consumer Secret) boş!");
            
            if (string.IsNullOrEmpty(settings.TwitterAccessToken))
                return (false, "❌ Access Token boş!");
            
            if (string.IsNullOrEmpty(settings.TwitterTokenSecret))
                return (false, "❌ Access Token Secret boş!");

            try
            {
                // Try to verify credentials using Twitter API v2 (Free Tier Compatible)
                var oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();
                
                // V2 Endpoint: users/me
                var oauth_url = "https://api.twitter.com/2/users/me";

                var dict = new SortedDictionary<string, string>
                {
                    { "oauth_consumer_key", settings.TwitterApiKey },
                    { "oauth_nonce", oauth_nonce },
                    { "oauth_signature_method", "HMAC-SHA1" },
                    { "oauth_timestamp", oauth_timestamp },
                    { "oauth_token", settings.TwitterAccessToken },
                    { "oauth_version", "1.0" }
                };

                string baseString = "GET&" + Uri.EscapeDataString(oauth_url) + "&" +
                                    Uri.EscapeDataString(string.Join("&", dict.Select(kvp => kvp.Key + "=" + Uri.EscapeDataString(kvp.Value))));

                string compositeKey = Uri.EscapeDataString(settings.TwitterApiSecret) + "&" + Uri.EscapeDataString(settings.TwitterTokenSecret);

                string oauth_signature;
                using (HMACSHA1 hasher = new HMACSHA1(Encoding.ASCII.GetBytes(compositeKey)))
                {
                    oauth_signature = Convert.ToBase64String(hasher.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
                }

                string headerString = "OAuth " +
                    $"oauth_consumer_key=\"{Uri.EscapeDataString(settings.TwitterApiKey)}\", " +
                    $"oauth_nonce=\"{Uri.EscapeDataString(oauth_nonce)}\", " +
                    $"oauth_signature=\"{Uri.EscapeDataString(oauth_signature)}\", " +
                    "oauth_signature_method=\"HMAC-SHA1\", " +
                    $"oauth_timestamp=\"{oauth_timestamp}\", " +
                    $"oauth_token=\"{Uri.EscapeDataString(settings.TwitterAccessToken)}\", " +
                    "oauth_version=\"1.0\"";

#pragma warning disable SYSLIB0014
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(oauth_url);
#pragma warning restore SYSLIB0014
                request.Headers.Add("Authorization", headerString);
                request.Method = "GET";
                request.ContentType = "application/json";

                using var response = (HttpWebResponse)request.GetResponse();
                using var reader = new StreamReader(response.GetResponseStream());
                var result = reader.ReadToEnd();
                
                // Parse username from V2 response: {"data":{"id":"...","name":"...","username":"..."}}
                if (result.Contains("username"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(result, "\"username\":\"([^\"]+)\"");
                    var username = match.Success ? match.Groups[1].Value : "Bilinmiyor";
                    return (true, $"✅ X/Twitter bağlantısı başarılı!\n👤 Hesap: @{username}");
                }
                
                return (true, "✅ X/Twitter bağlantısı başarılı!");
            }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse httpResponse)
                {
                    using var reader = new StreamReader(httpResponse.GetResponseStream());
                    var error = reader.ReadToEnd();
                    
                    if ((int)httpResponse.StatusCode == 401)
                        return (false, "❌ Yetkilendirme hatası (401)!\nAPI key'leri kontrol edin.");
                    
                    return (false, $"❌ API Hatası ({(int)httpResponse.StatusCode}): {error.Substring(0, Math.Min(error.Length, 150))}");
                }
                return (false, $"❌ Bağlantı hatası: {wex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Test hatası: {ex.Message}");
            }
        }
    }
}


