using System.Threading.Tasks;

namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Interface for AI model providers (Gemini, Claude, Perplexity, etc.)
    /// Enables multi-model support with automatic fallback
    /// </summary>
    public interface IModelProvider
    {
        /// <summary>
        /// Provider name (e.g., "Gemini", "Claude", "Perplexity")
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Specific model name (e.g., "gemini-2.0-flash", "gemini-2.5-flash")
        /// </summary>
        string ModelName { get; }
        
        /// <summary>
        /// Performance tier classification
        /// </summary>
        ModelTier Tier { get; }
        
        /// <summary>
        /// Send a text-only request to the model
        /// </summary>
        /// <param name="prompt">The prompt text</param>
        /// <param name="maxTokens">Maximum tokens in response</param>
        /// <returns>Model response or null if failed</returns>
        Task<string?> SendRequest(string prompt, int maxTokens = 1000);
        
        /// <summary>
        /// Send a request with an image (for vision-capable models)
        /// </summary>
        /// <param name="prompt">The prompt text</param>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="maxTokens">Maximum tokens in response</param>
        /// <returns>Model response or null if failed</returns>
        Task<string?> SendRequestWithImage(string prompt, string imagePath, int maxTokens = 1000);
        
        /// <summary>
        /// Check if provider is available (API key exists, not rate-limited, etc.)
        /// </summary>
        /// <returns>True if provider can be used</returns>
        bool IsAvailable();
        
        /// <summary>
        /// Get cost per 1000 tokens for this model
        /// Used for cost tracking and optimization
        /// </summary>
        /// <returns>Cost in USD per 1K tokens</returns>
        decimal GetCostPer1KTokens();
    }
}

