using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Canonical posting facade. All modules should publish through this service,
    /// while keeping their own content-generation logic separate.
    /// </summary>
    public class PostingService
    {
        private readonly SocialIntelService _socialIntel;
        private readonly StatsEngine? _stats;

        public PostingService(SocialIntelService socialIntel, StatsEngine? stats = null)
        {
            _socialIntel = socialIntel;
            _stats = stats;
        }

        public async Task<SocialIntelResult> PostTweetAsync(string text, string? mediaPath = null, string module = "PostingService")
        {
            if (string.IsNullOrWhiteSpace(text))
                return Error("Tweet payload is empty.");

            var result = await _socialIntel.PostTweet(text.Trim(), mediaPath);
            if (!IsVerifiedTweet(result))
            {
                string reason = result?.ErrorMessage ?? "Unknown posting error";
                Logger.Twitter($"❌ [{module}] Tweet doğrulanamadı: {reason}");
                return Error($"Tweet doğrulanamadı: {reason}");
            }

            _stats?.RecordTweet(module, 1, "", text.Length > 150 ? text.Substring(0, 150) : text);
            Logger.Twitter($"✅ [{module}] Tweet doğrulandı: {GetTweetUrl(result)}");
            return result!;
        }

        public async Task<SocialIntelResult> PostThreadAsync(IEnumerable<string> parts, string? mediaPath = null, string module = "PostingService")
        {
            var tweets = ThreadPipeline.EnsureWithinLimit(parts ?? Enumerable.Empty<string>(), 280)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (tweets.Count == 0)
                return Error("Thread payload is empty.");

            var result = await _socialIntel.PostThreadAsync(tweets, mediaPath);
            if (!IsVerifiedThread(result, tweets.Count))
            {
                string reason = result?.ErrorMessage ?? "Unknown posting error";
                Logger.Twitter($"❌ [{module}] Thread doğrulanamadı ({result?.posted_count ?? 0}/{tweets.Count}): {reason}");
                return Error($"Thread doğrulanamadı ({result?.posted_count ?? 0}/{tweets.Count}): {reason}");
            }

            _stats?.RecordTweet(module, tweets.Count, "", tweets[0].Length > 150 ? tweets[0].Substring(0, 150) : tweets[0]);
            Logger.Twitter($"✅ [{module}] Thread doğrulandı: {GetTweetUrl(result)} ({result!.posted_count}/{result.total_chunks})");
            return result!;
        }

        public static bool IsVerifiedTweet(SocialIntelResult? result)
        {
            return result != null
                && result.status == "success"
                && GetTweetUrl(result).Contains("/status/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVerifiedThread(SocialIntelResult? result, int expectedCount)
        {
            if (!IsVerifiedTweet(result)) return false;
            if (expectedCount <= 1) return true;
            return result!.posted_count >= expectedCount && result.total_chunks >= expectedCount;
        }

        private static string GetTweetUrl(SocialIntelResult? result)
        {
            if (result == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(result.tweet_url)) return result.tweet_url;
            if (!string.IsNullOrWhiteSpace(result.link)) return result.link;
            return string.Empty;
        }

        private static SocialIntelResult Error(string message)
        {
            return new SocialIntelResult { status = "error", message = message };
        }
    }
}
