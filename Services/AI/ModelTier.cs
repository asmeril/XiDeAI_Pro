namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// AI Model performance tier classification
    /// </summary>
    public enum ModelTier
    {
        /// <summary>
        /// Ultra-fast models for simple tasks (e.g., Gemini Flash)
        /// Characteristics: FREE or very cheap, very fast, good for yes/no decisions
        /// </summary>
        UltraFast,
        
        /// <summary>
        /// Balanced models for general use (e.g., Gemini Pro, Claude Sonnet, Perplexity Sonar)
        /// Characteristics: Medium cost, good quality, versatile
        /// </summary>
        Balanced,
        
        /// <summary>
        /// Premium models for complex tasks (e.g., Gemini Pro 2.0, GPT-4)
        /// Characteristics: Expensive, highest quality, best for critical analysis
        /// </summary>
        Premium
    }
}

