// INTERACTION_ENGINE_VERSION: 1.0
// PURPOSE: Handles targeted interactions with influencers (Likes, RTs, Replies).
// This ensures professional engagement without manual overhead.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class InteractionEngine
    {
        private readonly SocialIntelService _socialIntel;
        private readonly TwitterService _twitter;
        private readonly GeminiService _gemini;
        private readonly InfluencerControlService _influencers;
        private readonly SpamProtection _spam;
        private readonly TelegramService _telegram;
        private readonly StatsEngine _stats;
        private readonly PromptManager _prompts = new PromptManager();

        public event Action<string, string>? OnLog;
        public event Action<string>? OnStatusUpdate;

        public InteractionEngine(
            SocialIntelService socialIntel,
            TwitterService twitter,
            GeminiService gemini,
            InfluencerControlService influencers,
            SpamProtection spam,
            TelegramService telegram,
            StatsEngine stats)
        {
            _socialIntel = socialIntel;
            _twitter = twitter;
            _gemini = gemini;
            _influencers = influencers;
            _spam = spam;
            _telegram = telegram;
            _stats = stats;
        }

        /// <summary>
        /// Mass Like & RT for a list of target influencers.
        /// Only targets original tweets from the author.
        /// </summary>
        public async Task RunTargetedCheck(string category)
        {
            try
            {
                OnStatusUpdate?.Invoke($"{category} fenomenleri kontrol ediliyor...");
                
                // 1. Get targets from database
                var targets = _influencers.GetInfluencers(category);
                if (targets == null || targets.Count == 0)
                {
                    OnLog?.Invoke($"⚠️ {category} için hedef fenomen bulunamadı.", "Interaction");
                    return;
                }

                // 2. Select top N targets for this cycle
                string handles = string.Join(",", targets
                    .Select(t => t.Handle)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim()));
                if (string.IsNullOrWhiteSpace(handles))
                {
                    OnLog?.Invoke($"⚠️ {category} için geçerli handle bulunamadı.", "Interaction");
                    return;
                }
                OnLog?.Invoke($"🔍 {targets.Count} fenomen kontrol ediliyor...", "Interaction");

                // 3. Execute bulk interaction via social_intel.py (now fixed with strict handle check)
                var result = await _socialIntel.InteractWithTargets(handles);

                if (result.status == "success")
                {
                    _stats.RecordActivity("Interaction", $"Ran interaction check: {category}", true, result.message);
                    OnLog?.Invoke($"✅ Etkileşim Tamamlandı: {result.message}", "Interaction");
                }
                else
                {
                    OnLog?.Invoke($"❌ Etkileşim Hatası: {result.message}", "Interaction");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ InteractionEngine Error: {ex.Message}", "System");
            }
        }

        /// <summary>
        /// Proactive Reply Bot logic (Phase 3: Smart Interaction)
        /// Finds viral tweets on trending topics and generates AI replies
        /// </summary>
        public async Task CheckViralInteractions()
        {
            var cfg = Config.ConfigManager.Current;
            if (!cfg.BotInteractionEnabled)
            {
                OnLog?.Invoke("⏸️ Bot etkileşimi devre dışı", "Interaction");
                return;
            }

            try
            {
                OnStatusUpdate?.Invoke("🔍 Trend arıyor...");
                
                // 1. Find relevant topics from Config
                var trends = await _socialIntel.GetTrendsAsync();
                var relevantKeywords = cfg.BotTopicKeywords.Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => !string.IsNullOrEmpty(k)).ToArray();
                
                if (relevantKeywords.Length == 0)
                {
                    OnLog?.Invoke("⚠️ Bot konuları tanımlanmamış.", "Interaction");
                    return;
                }
                
                string? topic = trends.FirstOrDefault(t => relevantKeywords.Any(k => t.ToLowerInvariant().Contains(k)));
                
                if (string.IsNullOrEmpty(topic))
                {
                    OnLog?.Invoke("⚠️ İlgili trend bulunamadı.", "Interaction");
                    return;
                }
                
                OnLog?.Invoke($"📌 Trend bulundu: {topic}", "Interaction");
                
                // 2. Find viral tweet with filters
                var posts = await _socialIntel.FindInfluencerAnalyses(topic + $" min_faves:{cfg.BotMinFavorites}", SocialIntelService.DetectMarket(topic));
                
                // 3. Apply filters from Config
                var now = DateTime.Now;
                var filteredPosts = posts
                    .Where(p => !_socialIntel.Memory.HasInteracted(p.Url))
                    .Where(p => p.PostDate > DateTime.MinValue && (now - p.PostDate).TotalHours < cfg.BotMaxTweetAgeHours)
                    .Where(p => p.FollowerCount >= cfg.BotMinFollowers)
                    .OrderByDescending(p => p.Engagement)
                    .ToList();
                
                if (filteredPosts.Count == 0)
                {
                    OnLog?.Invoke($"⚠️ Filtrelere uygun tweet bulunamadı (≥{cfg.BotMinFollowers} takipçi, <{cfg.BotMaxTweetAgeHours}h).", "Interaction");
                    return;
                }
                
                var post = filteredPosts[0];
                OnStatusUpdate?.Invoke($"💡 AI yanıt üretiyor (Two-Step)...");
                
                // v4.2.0: Two-Step Logic (Category Detection + Categorized Reply)
                var (category, reply) = await _gemini.GenerateTwoStepReply(post.Content, post.Handle);
                    
                if (!string.IsNullOrEmpty(reply))
                {
                    // Send to Telegram for approval
                    string approvalMsg = $"🤖 BOT YANIT ÖNERİSİ [{category}]\n\n" +
                                       $"👤 @{post.Handle} ({post.FollowerCount:N0} takipçi)\n" +
                                       $"💬 Orijinal: {post.Content.Substring(0, Math.Min(200, post.Content.Length))}...\n\n" +
                                       $"🤖 Önerilen Yanıt:\n{reply}\n\n" +
                                       $"🔗 {post.Url}\n\n" +
                                       $"Onaylamak için 'ONAYLA' yazın.";
                    
                    await _telegram.SendMessageAsync(approvalMsg);
                    _socialIntel.Memory.RecordInteraction(post.Url);
                    _stats.RecordActivity("Interaction", $"Viral reply proposed [{category}]: {post.Handle}", true, topic);
                    OnLog?.Invoke($"✅ Yanıt önerisi Telegram'a gönderildi: @{post.Handle} [{category}]", "Interaction");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ CheckViralInteractions Error: {ex.Message}", "Interaction");
            }
        }
    }
}
