namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// Cost breakdown for an AI API call.
/// </summary>
public class CostBreakdown
{
    public decimal InputCost { get; set; }
    public decimal OutputCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Service for calculating API costs across different AI providers.
/// </summary>
public interface ICostCalculatorService
{
    /// <summary>
    /// Calculate the total cost for an API call.
    /// </summary>
    decimal CalculateCost(string provider, string model, int inputTokens, int outputTokens);

    /// <summary>
    /// Get a detailed cost breakdown for an API call.
    /// </summary>
    CostBreakdown GetCostBreakdown(string provider, string model, int inputTokens, int outputTokens);
}

/// <summary>
/// Implementation of cost calculation for all supported AI providers.
/// Pricing per 1K tokens based on provider documentation.
/// </summary>
public class CostCalculatorService : ICostCalculatorService
{
    /// <summary>
    /// Pricing dictionary: (provider, model) -> (inputPricePer1K, outputPricePer1K)
    /// </summary>
    private static readonly Dictionary<string, (decimal Input, decimal Output)> Pricing = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI pricing per 1K tokens
        ["openai:gpt-4o"] = (0.0025m, 0.01m),
        ["openai:gpt-4o-mini"] = (0.00015m, 0.0006m),
        ["openai:gpt-4-turbo"] = (0.01m, 0.03m),

        // Anthropic pricing per 1K tokens
        ["anthropic:claude-3-opus"] = (0.015m, 0.075m),
        ["anthropic:claude-3-sonnet"] = (0.003m, 0.015m),
        ["anthropic:claude-3-haiku"] = (0.00025m, 0.00125m),
        ["anthropic:claude-sonnet-4"] = (0.003m, 0.015m),

        // DeepSeek pricing per 1K tokens
        ["deepseek:deepseek-chat"] = (0.00014m, 0.00028m),
        ["deepseek:deepseek-coder"] = (0.00014m, 0.00028m),

        // Google pricing per 1K tokens
        ["google:gemini-1.5-pro"] = (0.00125m, 0.005m),
        ["google:gemini-1.5-flash"] = (0.000075m, 0.0003m),
    };

    /// <summary>
    /// Default pricing for unknown models.
    /// </summary>
    private static readonly (decimal Input, decimal Output) DefaultPricing = (0.001m, 0.002m);

    public decimal CalculateCost(string provider, string model, int inputTokens, int outputTokens)
    {
        var breakdown = GetCostBreakdown(provider, model, inputTokens, outputTokens);
        return breakdown.TotalCost;
    }

    public CostBreakdown GetCostBreakdown(string provider, string model, int inputTokens, int outputTokens)
    {
        var pricing = GetPricing(provider, model);

        var inputCost = (inputTokens / 1000m) * pricing.Input;
        var outputCost = (outputTokens / 1000m) * pricing.Output;

        return new CostBreakdown
        {
            InputCost = inputCost,
            OutputCost = outputCost,
            TotalCost = inputCost + outputCost,
            Currency = "USD"
        };
    }

    private (decimal Input, decimal Output) GetPricing(string provider, string model)
    {
        // Normalize provider and model names
        var normalizedProvider = provider.ToLowerInvariant().Trim();
        var normalizedModel = model.ToLowerInvariant().Trim();

        // Try exact match first
        var key = $"{normalizedProvider}:{normalizedModel}";
        if (Pricing.TryGetValue(key, out var pricing))
        {
            return pricing;
        }

        // Try partial model match (e.g., "gpt-4o-2024-08-06" should match "gpt-4o")
        foreach (var kvp in Pricing)
        {
            var parts = kvp.Key.Split(':');
            if (parts.Length == 2 &&
                parts[0].Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase) &&
                normalizedModel.Contains(parts[1], StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Handle Ollama (local) - free
        if (normalizedProvider == "ollama")
        {
            return (0m, 0m);
        }

        // Return default pricing for unknown models
        return DefaultPricing;
    }
}
