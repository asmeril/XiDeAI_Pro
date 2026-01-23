using System;
using System.Collections.Generic;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    public static class ContentQualityGuard
    {
        public static bool IsSpamOrLowQuality(string content, out string reason)
        {
            reason = string.Empty;

            // 1. Boş veya çok kısa içerik kontrolü
            if (string.IsNullOrWhiteSpace(content) || content.Length < 50)
            {
                reason = "İçerik çok kısa (<50 karakter) veya boş.";
                return true;
            }

            // 2. Hashtag oranı kontrolü
            int hashtagCount = System.Text.RegularExpressions.Regex.Matches(content, @"#\w+").Count;
            var words = content.Split(new[] { ' ', '\n', '.', ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            int wordCount = words.Length;
            
            if (wordCount > 0 && (double)hashtagCount / wordCount > 0.5)
            {
                reason = "İçerik çoğunlukla hashtaglerden oluşuyor (>%50).";
                return true;
            }

            /* 
            // 3. LOOP/TEKRAR TESPİTİ - Aynı kelime veya ifade sürekli tekrarlanıyorsa SPAM
            var wordFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                if (word.Length < 3) continue; // Çok kısa kelimeleri atla
                if (wordFrequency.ContainsKey(word))
                    wordFrequency[word]++;
                else
                    wordFrequency[word] = 1;
            }

            // Bir kelime 5+ kez tekrarlanıyorsa SPAM
            var topRepeated = wordFrequency.OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (topRepeated.Value >= 5 && wordCount > 10)
            {
                reason = $"TEKRAR TESPİTİ: '{topRepeated.Key}' kelimesi {topRepeated.Value} kez tekrarlanıyor.";
                return true;
            }

            // Benzersiz kelime oranı %30'un altındaysa SPAM (çok fazla tekrar var)
            if (wordCount > 20)
            {
                double uniqueRatio = (double)wordFrequency.Count / wordCount;
                if (uniqueRatio < 0.30)
                {
                    reason = $"TEKRAR TESPİTİ: Benzersiz kelime oranı çok düşük (%{uniqueRatio:P0}).";
                    return true;
                }
            }

            // 4. Ardışık tekrar tespiti (aynı cümle/parça art arda tekrarlanıyor mu?)
            // 20 karakterlik parçaları kontrol et
            var chunks = new List<string>();
            for (int i = 0; i < content.Length - 20; i += 10)
            {
                chunks.Add(content.Substring(i, 20));
            }
            var chunkFreq = chunks.GroupBy(c => c).Where(g => g.Count() >= 3).FirstOrDefault();
            if (chunkFreq != null)
            {
                reason = $"ARDISIK TEKRAR: '{chunkFreq.Key.Substring(0, Math.Min(15, chunkFreq.Key.Length))}...' parçası {chunkFreq.Count()} kez tekrarlanıyor.";
                return true;
            }
            */

            return false;
        }

        public static string CleanPrivateLinks(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Regex to match t.me and telegram.me links (with optional https://)
            // Matches: t.me/User, telegram.me/JoinChat etc.
            string pattern = @"(https?:\/\/)?(www\.)?(t\.me|telegram\.me)\/[a-zA-Z0-9_]+";
            
            return System.Text.RegularExpressions.Regex.Replace(text, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        public static bool ContainsPrivateLinks(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Aggressive patterns for Telegram & Discord links detection
            var patterns = new[]
            {
                @"(t\.me|telegram\.me)\/[a-zA-Z0-9_\-]+",                      // t.me/username
                @"(?:https?:\/\/)?(t\.me|telegram\.me|telegram\.org)\/\S+",   // Full URLs
                @"telegram\s+(?:link|channel|group|bot|chat)[\s:]*\S+",        // "telegram link/channel" text
                @"\bt\.me\S*\b",                                                // Shortened t.me
                @"(?:discord\.gg|discord\.com)\/\S+",                          // Discord links
                @"ucretsiz\s+telegram",                                         // Turkish: "free telegram"
                @"telegram\s*kanal",                                            // Turkish: "telegram channel"
                @"(?:ses\s+kayd|audio).*telegram",                             // Audio/voice content on Telegram
            };
            
            foreach (var pattern in patterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}

