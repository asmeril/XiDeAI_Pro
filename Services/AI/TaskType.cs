namespace XiDeAI_Pro.Services.AI
{
    /// <summary>
    /// Task types for AI model selection
    /// Each task type has preferred models based on requirements
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Quick signal quality check (yes/no decision)
        /// Preferred: Gemini Flash (fast, free)
        /// </summary>
        DeepScan,
        
        /// <summary>
        /// News analysis and impact assessment
        /// Preferred: Perplexity Sonar (real-time info, sources)
        /// </summary>
        NewsAnalysis,
        
        /// <summary>
        /// Chart pattern and formation analysis with image
        /// Preferred: Gemini Pro 2.0 (best vision capabilities)
        /// </summary>
        FormationAnalysis,
        
        /// <summary>
        /// Creative tweet generation
        /// Preferred: Claude Sonnet (best creativity and tone)
        /// </summary>
        TweetGeneration,
        
        /// <summary>
        /// Quick quote extraction and formatting
        /// Preferred: Gemini Flash (fast, simple task)
        /// </summary>
        SmartQuote,
        
        /// <summary>
        /// Contextual reply to influencer posts
        /// Preferred: Claude Sonnet (best context understanding)
        /// </summary>
        InfluencerReply,
        
        /// <summary>
        /// Research symbol with current information
        /// Preferred: Perplexity Sonar Pro (real-time research)
        /// </summary>
        SymbolResearch,
        
        /// <summary>
        /// Track trending topics in real-time
        /// Preferred: Perplexity Sonar (real-time trends)
        /// </summary>
        TrendTracking,
        
        /// <summary>
        /// General analysis without specific requirements
        /// Preferred: Gemini Pro (balanced quality/cost)
        /// </summary>
        GeneralAnalysis
    }
}

