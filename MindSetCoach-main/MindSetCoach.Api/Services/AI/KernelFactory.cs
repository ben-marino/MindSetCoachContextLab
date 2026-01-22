using Azure.Identity;
using Microsoft.SemanticKernel;

namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// Factory for creating Semantic Kernel instances for different AI providers.
/// Used by batch experiments to run the same test across multiple providers.
/// </summary>
public interface IKernelFactory
{
    /// <summary>
    /// Create a Kernel instance for the specified provider and model.
    /// </summary>
    /// <param name="provider">Provider name: openai, anthropic, deepseek, google, ollama, azure, azureopenai</param>
    /// <param name="model">Model ID (e.g., gpt-4o-mini, claude-3-haiku-20240307)</param>
    /// <returns>Configured Kernel instance</returns>
    Kernel CreateKernel(string provider, string model);

    /// <summary>
    /// Check if a provider is configured with valid API credentials.
    /// </summary>
    bool IsProviderConfigured(string provider);
}

public class KernelFactory : IKernelFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KernelFactory> _logger;

    public KernelFactory(IConfiguration configuration, ILogger<KernelFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Kernel CreateKernel(string provider, string model)
    {
        var builder = Kernel.CreateBuilder();
        var normalizedProvider = provider.ToLower();

        _logger.LogInformation("Creating kernel for provider: {Provider}, model: {Model}", provider, model);

        switch (normalizedProvider)
        {
            case "openai":
                var openaiKey = GetApiKey("openai");
                if (string.IsNullOrEmpty(openaiKey))
                    throw new InvalidOperationException("OpenAI API key not configured");
                builder.AddOpenAIChatCompletion(model, openaiKey);
                break;

            case "anthropic":
                var anthropicKey = GetApiKey("anthropic");
                if (string.IsNullOrEmpty(anthropicKey))
                    throw new InvalidOperationException("Anthropic API key not configured");

                // Anthropic uses OpenAI-compatible endpoint
                var anthropicEndpoint = _configuration["AI:AnthropicEndpoint"]
                    ?? Environment.GetEnvironmentVariable("ANTHROPIC_ENDPOINT")
                    ?? "https://api.anthropic.com/v1";

                #pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: anthropicKey,
                    endpoint: new Uri(anthropicEndpoint));
                #pragma warning restore SKEXP0010
                break;

            case "google":
                var googleKey = GetApiKey("google");
                if (string.IsNullOrEmpty(googleKey))
                    throw new InvalidOperationException("Google API key not configured");

                // Google Gemini via OpenAI-compatible endpoint
                var googleEndpoint = _configuration["AI:GoogleEndpoint"]
                    ?? Environment.GetEnvironmentVariable("GOOGLE_ENDPOINT")
                    ?? "https://generativelanguage.googleapis.com/v1beta/openai";

                #pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: googleKey,
                    endpoint: new Uri(googleEndpoint));
                #pragma warning restore SKEXP0010
                break;

            case "deepseek":
                var deepseekKey = GetApiKey("deepseek");
                if (string.IsNullOrEmpty(deepseekKey))
                    throw new InvalidOperationException("DeepSeek API key not configured");

                var deepseekEndpoint = _configuration["AI:DeepSeekEndpoint"]
                    ?? Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT")
                    ?? "https://api.deepseek.com/v1";

                var normalizedModel = NormalizeDeepSeekModel(model);

                #pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: normalizedModel,
                    apiKey: deepseekKey,
                    endpoint: new Uri(deepseekEndpoint));
                #pragma warning restore SKEXP0010
                break;

            case "ollama":
                var ollamaEndpoint = _configuration["AI:OllamaEndpoint"]
                    ?? Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
                    ?? "http://localhost:11434";

                var ollamaApiEndpoint = new Uri($"{ollamaEndpoint.TrimEnd('/')}/v1");

                #pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: "ollama",
                    endpoint: ollamaApiEndpoint);
                #pragma warning restore SKEXP0010
                break;

            case "azure":
                var azureKey = GetApiKey("azure");
                var azureEndpoint = _configuration["AI:AzureEndpoint"]
                    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

                if (string.IsNullOrEmpty(azureKey) || string.IsNullOrEmpty(azureEndpoint))
                    throw new InvalidOperationException("Azure OpenAI credentials not configured");

                builder.AddAzureOpenAIChatCompletion(model, azureEndpoint, azureKey);
                break;

            case "azureopenai":
                var azureOpenAIEndpoint = _configuration["SemanticKernel:AzureOpenAI:Endpoint"]
                    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                var azureOpenAIDeployment = _configuration["SemanticKernel:AzureOpenAI:DeploymentName"]
                    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                    ?? model;
                var useAzureAD = _configuration.GetValue<bool>("SemanticKernel:AzureOpenAI:UseAzureAD");

                if (string.IsNullOrEmpty(azureOpenAIEndpoint))
                    throw new InvalidOperationException("Azure OpenAI endpoint not configured. Set SemanticKernel:AzureOpenAI:Endpoint or AZURE_OPENAI_ENDPOINT");

                if (useAzureAD)
                {
                    // Use Azure Active Directory authentication via DefaultAzureCredential
                    _logger.LogInformation("Using Azure AD authentication for Azure OpenAI");
                    var credential = new DefaultAzureCredential();
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        credential);
                }
                else
                {
                    // Use API key authentication
                    var azureOpenAIKey = _configuration["SemanticKernel:AzureOpenAI:ApiKey"]
                        ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

                    if (string.IsNullOrEmpty(azureOpenAIKey))
                        throw new InvalidOperationException("Azure OpenAI API key not configured. Set SemanticKernel:AzureOpenAI:ApiKey or AZURE_OPENAI_API_KEY");

                    _logger.LogInformation("Using API key authentication for Azure OpenAI");
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        apiKey: azureOpenAIKey);
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown AI provider: {provider}. Supported: openai, anthropic, google, deepseek, ollama, azure, azureopenai");
        }

        return builder.Build();
    }

    public bool IsProviderConfigured(string provider)
    {
        var normalizedProvider = provider.ToLower();

        return normalizedProvider switch
        {
            "openai" => !string.IsNullOrEmpty(GetApiKey("openai")),
            "anthropic" => !string.IsNullOrEmpty(GetApiKey("anthropic")),
            "google" => !string.IsNullOrEmpty(GetApiKey("google")),
            "deepseek" => !string.IsNullOrEmpty(GetApiKey("deepseek")),
            "azure" => !string.IsNullOrEmpty(GetApiKey("azure")) &&
                       !string.IsNullOrEmpty(_configuration["AI:AzureEndpoint"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
            "azureopenai" => IsAzureOpenAIConfigured(),
            "ollama" => true, // Always available if Ollama is running locally
            _ => false
        };
    }

    private string? GetApiKey(string provider)
    {
        return provider.ToLower() switch
        {
            "openai" => _configuration["AI:ApiKey"]
                ?? _configuration["AI:OpenAIApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),

            "anthropic" => _configuration["AI:AnthropicApiKey"]
                ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),

            "google" => _configuration["AI:GoogleApiKey"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),

            "deepseek" => _configuration["AI:DeepSeekApiKey"]
                ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),

            "azure" => _configuration["AI:AzureApiKey"]
                ?? _configuration["AI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),

            _ => null
        };
    }

    private static string NormalizeDeepSeekModel(string modelId)
    {
        return modelId.ToLower() switch
        {
            "deepseek-chat" or "chat" => "deepseek-chat",
            "deepseek-reasoner" or "reasoner" or "r1" => "deepseek-reasoner",
            "deepseek-coder" or "coder" => "deepseek-coder",
            _ when modelId.StartsWith("gpt-") => "deepseek-chat",
            _ => modelId
        };
    }

    private bool IsAzureOpenAIConfigured()
    {
        var endpoint = _configuration["SemanticKernel:AzureOpenAI:Endpoint"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        if (string.IsNullOrEmpty(endpoint))
            return false;

        var useAzureAD = _configuration.GetValue<bool>("SemanticKernel:AzureOpenAI:UseAzureAD");

        // If using Azure AD, we don't need an API key
        if (useAzureAD)
            return true;

        // Otherwise, we need an API key
        var apiKey = _configuration["SemanticKernel:AzureOpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        return !string.IsNullOrEmpty(apiKey);
    }
}
