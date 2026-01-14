using Microsoft.SemanticKernel;
using MindSetCoach.Api.Services;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Api.Configuration;

/// <summary>
/// Extension methods for configuring Semantic Kernel and AI services.
/// Supports: OpenAI, Azure OpenAI, DeepSeek, and Ollama.
/// </summary>
public static class SemanticKernelConfiguration
{
    /// <summary>
    /// Add Semantic Kernel and AI services to the service collection.
    /// </summary>
    public static IServiceCollection AddSemanticKernelServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get configuration
        var aiProvider = configuration["AI:Provider"] ?? "OpenAI";
        var modelId = configuration["AI:ModelId"] ?? "gpt-4o-mini";
        
        // Get API key based on provider
        var apiKey = GetApiKey(aiProvider, configuration);

        // Ollama doesn't require an API key
        var isOllama = aiProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);
        var isLocal = isOllama;
        
        if (!isOllama && string.IsNullOrEmpty(apiKey))
        {
            // Register a placeholder kernel for development without API key
            services.AddSingleton<Kernel>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Kernel>>();
                logger.LogWarning("No AI API key configured for {Provider}. AI features will return mock responses.", aiProvider);
                return Kernel.CreateBuilder().Build();
            });

            // Register mock AI service
            services.AddScoped<IMentalCoachAIService, MockMentalCoachAIService>();
        }
        else
        {
            // Build the real kernel based on provider
            services.AddSingleton<Kernel>(sp =>
            {
                var builder = Kernel.CreateBuilder();
                var logger = sp.GetRequiredService<ILogger<Kernel>>();

                switch (aiProvider.ToLower())
                {
                    case "openai":
                        logger.LogInformation("Configuring OpenAI provider with model: {ModelId}", modelId);
                        builder.AddOpenAIChatCompletion(modelId, apiKey!);
                        break;

                    case "azure":
                        var azureEndpoint = configuration["AI:AzureEndpoint"]
                            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                        var deploymentName = configuration["AI:DeploymentName"] ?? modelId;

                        if (!string.IsNullOrEmpty(azureEndpoint))
                        {
                            logger.LogInformation("Configuring Azure OpenAI provider with deployment: {DeploymentName}", deploymentName);
                            builder.AddAzureOpenAIChatCompletion(deploymentName, azureEndpoint, apiKey!);
                        }
                        break;

                    case "deepseek":
                        // DeepSeek uses OpenAI-compatible API
                        var deepseekEndpoint = configuration["AI:DeepSeekEndpoint"]
                            ?? Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT")
                            ?? "https://api.deepseek.com/v1";

                        var deepseekModel = NormalizeDeepSeekModel(modelId);

                        logger.LogInformation(
                            "Configuring DeepSeek provider at {Endpoint} with model: {ModelId}",
                            deepseekEndpoint, deepseekModel);

                        #pragma warning disable SKEXP0010 // OpenAI endpoint overload is experimental
                        builder.AddOpenAIChatCompletion(
                            modelId: deepseekModel,
                            apiKey: apiKey!,
                            endpoint: new Uri(deepseekEndpoint));
                        #pragma warning restore SKEXP0010
                        break;

                    case "ollama":
                        // Ollama exposes an OpenAI-compatible API
                        var ollamaEndpoint = configuration["AI:OllamaEndpoint"]
                            ?? Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
                            ?? "http://localhost:11434";

                        var ollamaApiEndpoint = new Uri($"{ollamaEndpoint.TrimEnd('/')}/v1");

                        logger.LogInformation(
                            "Configuring Ollama provider at {Endpoint} with model: {ModelId}",
                            ollamaEndpoint, modelId);

                        #pragma warning disable SKEXP0010 // OpenAI endpoint overload is experimental
                        builder.AddOpenAIChatCompletion(
                            modelId: modelId,
                            apiKey: "ollama",
                            endpoint: ollamaApiEndpoint);
                        #pragma warning restore SKEXP0010
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unknown AI provider: {aiProvider}. Supported: OpenAI, Azure, DeepSeek, Ollama");
                }

                return builder.Build();
            });

            // Register real AI service
            services.AddScoped<IMentalCoachAIService, MentalCoachAIService>();
        }

        // Register experiment logger as singleton (to persist across requests)
        services.AddSingleton<ContextExperimentLogger>();
        
        // Register AI provider info for experiments
        services.AddSingleton(new AIProviderInfo
        {
            Provider = aiProvider,
            ModelId = modelId,
            IsLocal = isLocal
        });

        return services;
    }
    
    /// <summary>
    /// Get the appropriate API key based on provider.
    /// </summary>
    private static string? GetApiKey(string provider, IConfiguration configuration)
    {
        return provider.ToLower() switch
        {
            "openai" => configuration["AI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),

            "azure" => configuration["AI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),

            "deepseek" => configuration["AI:DeepSeekApiKey"]
                ?? configuration["AI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),

            "ollama" => "ollama", // Dummy key, not used

            _ => configuration["AI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("AI_API_KEY")
        };
    }
    
    /// <summary>
    /// Normalize DeepSeek model names.
    /// </summary>
    private static string NormalizeDeepSeekModel(string modelId)
    {
        return modelId.ToLower() switch
        {
            "deepseek-chat" or "chat" => "deepseek-chat",
            "deepseek-reasoner" or "reasoner" or "r1" => "deepseek-reasoner",
            "deepseek-coder" or "coder" => "deepseek-coder",
            _ when modelId.StartsWith("gpt-") => "deepseek-chat", // Default if using OpenAI model name
            _ => modelId
        };
    }
}

/// <summary>
/// Information about the configured AI provider for experiment logging and cost tracking.
/// </summary>
public class AIProviderInfo
{
    public string Provider { get; set; } = "Unknown";
    public string ModelId { get; set; } = "Unknown";
    public bool IsLocal { get; set; }
    
    /// <summary>
    /// Estimated cost per 1K input tokens. $0 for local models.
    /// </summary>
    public decimal CostPer1KInputTokens => IsLocal ? 0m : GetInputCost();
    
    /// <summary>
    /// Estimated cost per 1K output tokens. $0 for local models.
    /// </summary>
    public decimal CostPer1KOutputTokens => IsLocal ? 0m : GetOutputCost();
    
    private decimal GetInputCost()
    {
        var provider = Provider.ToLower();
        var model = ModelId.ToLower();
        
        return (provider, model) switch
        {
            // OpenAI pricing (per 1K input tokens)
            ("openai", var m) when m.Contains("gpt-4o-mini") => 0.00015m,
            ("openai", var m) when m.Contains("gpt-4o") => 0.0025m,
            ("openai", var m) when m.Contains("gpt-4-turbo") => 0.01m,
            ("openai", var m) when m.Contains("o1-preview") => 0.015m,
            ("openai", var m) when m.Contains("o1-mini") => 0.003m,
            ("openai", var m) when m.Contains("o3-mini") => 0.0011m,

            // DeepSeek pricing (per 1K input tokens) - very cheap!
            ("deepseek", var m) when m.Contains("reasoner") => 0.00055m,
            ("deepseek", _) => 0.00014m,

            // Azure - similar to OpenAI
            ("azure", _) => 0.001m,

            _ => 0.001m // Default estimate
        };
    }
    
    private decimal GetOutputCost()
    {
        var provider = Provider.ToLower();
        var model = ModelId.ToLower();
        
        return (provider, model) switch
        {
            // OpenAI output pricing
            ("openai", var m) when m.Contains("gpt-4o-mini") => 0.0006m,
            ("openai", var m) when m.Contains("gpt-4o") => 0.01m,
            ("openai", var m) when m.Contains("gpt-4-turbo") => 0.03m,
            ("openai", var m) when m.Contains("o1-preview") => 0.06m,
            ("openai", var m) when m.Contains("o1-mini") => 0.012m,
            ("openai", var m) when m.Contains("o3-mini") => 0.0044m,

            // DeepSeek output pricing
            ("deepseek", var m) when m.Contains("reasoner") => 0.00219m,
            ("deepseek", _) => 0.00028m,

            // Azure
            ("azure", _) => 0.002m,

            _ => 0.002m
        };
    }
    
    /// <summary>
    /// Get a human-readable cost comparison string.
    /// </summary>
    public string GetCostComparison()
    {
        if (IsLocal)
            return "Running locally - $0.00 per request (electricity only)";
        
        var inputCost = CostPer1KInputTokens * 1000; // Per 1M
        var outputCost = CostPer1KOutputTokens * 1000;
        
        return $"${inputCost:F2}/1M input, ${outputCost:F2}/1M output tokens";
    }
    
    /// <summary>
    /// Get the provider tier description.
    /// </summary>
    public string GetProviderTier()
    {
        return Provider.ToLower() switch
        {
            "openai" => "Cloud - OpenAI (GPT-4o, o1, o3 models)",
            "azure" => "Cloud - Azure OpenAI (Enterprise)",
            "deepseek" => "Cloud - DeepSeek (Budget, strong reasoning)",
            "ollama" => "Local - Ollama (Free, private)",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Mock AI service for development without API key.
/// Returns placeholder responses to allow testing the API structure.
/// </summary>
public class MockMentalCoachAIService : IMentalCoachAIService
{
    private readonly IJournalService _journalService;
    private readonly ILogger<MockMentalCoachAIService> _logger;
    private readonly ContextExperimentLogger _experimentLogger;

    public MockMentalCoachAIService(
        IJournalService journalService,
        ILogger<MockMentalCoachAIService> logger,
        ContextExperimentLogger experimentLogger)
    {
        _journalService = journalService;
        _logger = logger;
        _experimentLogger = experimentLogger;
    }

    public async Task<WeeklySummaryResponse> GenerateWeeklySummaryAsync(
        int athleteId,
        string persona,
        ContextOptions? options = null)
    {
        _logger.LogWarning("Using MOCK AI service - configure API key for real responses");

        var entries = await _journalService.GetAthleteEntriesAsync(athleteId);

        // Log for experiment tracking even in mock mode
        _experimentLogger.LogContextSent(new ContextLog
        {
            AthleteId = athleteId,
            Persona = persona,
            EntryCount = entries.Count,
            TotalTokensEstimate = 0,
            ContextOptions = options,
            Timestamp = DateTime.UtcNow
        });

        var mockSummary = persona == "goggins"
            ? $"[MOCK GOGGINS] You logged {entries.Count} entries. Stop making excuses. The work isn't done. Stay hard."
            : $"[MOCK LASSO] Hey there! You've been showing up - {entries.Count} entries logged. That takes courage. Believe in yourself!";

        return new WeeklySummaryResponse
        {
            Summary = mockSummary,
            Persona = persona,
            EntriesAnalyzed = entries.Count,
            TokensUsed = 0,
            ContextOptionsUsed = options
        };
    }

    public Task<PatternAnalysisResponse> AnalyzePatternsAsync(int athleteId, int daysToAnalyze = 30)
    {
        return Task.FromResult(new PatternAnalysisResponse
        {
            OverallTrend = "mock_data",
            RecurringBarriers = new List<string> { "self-doubt", "comparison" },
            Recommendations = new List<string> { "Configure API key for real analysis" },
            DaysAnalyzed = daysToAnalyze
        });
    }

    public Task<FlagRecommendation> ShouldFlagEntryAsync(int entryId)
    {
        return Task.FromResult(new FlagRecommendation
        {
            ShouldFlag = false,
            Reason = "Mock service - configure API key for real analysis",
            Severity = "none",
            ConcernIndicators = new List<string>()
        });
    }

    public Task<PositionTestResult> RunPositionTestAsync(
        int athleteId,
        string needleFact,
        string persona = "lasso")
    {
        return Task.FromResult(new PositionTestResult
        {
            NeedleFact = needleFact,
            Results = new List<PositionTestOutcome>
            {
                new() { Position = "start", FactRetrieved = true, GeneratedSummary = "[MOCK]" },
                new() { Position = "middle", FactRetrieved = false, GeneratedSummary = "[MOCK]" },
                new() { Position = "end", FactRetrieved = true, GeneratedSummary = "[MOCK]" }
            },
            Conclusion = "MOCK: Configure API key for real experiment"
        });
    }

    public Task<CompressionTestResult> RunCompressionTestAsync(int athleteId, string persona = "lasso")
    {
        return Task.FromResult(new CompressionTestResult
        {
            FullContext = new CompressionTestOutcome { Label = "Full", Summary = "[MOCK]" },
            CompressedContext = new CompressionTestOutcome { Label = "Compressed", Summary = "[MOCK]" },
            LimitedContext = new CompressionTestOutcome { Label = "Limited", Summary = "[MOCK]" },
            Conclusion = "MOCK: Configure API key for real experiment"
        });
    }
}
