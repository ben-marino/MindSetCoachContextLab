using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Service for generating LinkedIn post content from experiment insights.
/// </summary>
public interface IInsightSummarizerService
{
    /// <summary>
    /// Generate a LinkedIn post from experiment batch results.
    /// </summary>
    /// <param name="batchId">The batch ID to generate insights for</param>
    /// <param name="tone">The tone to use for the post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated LinkedIn post content</returns>
    Task<LinkedInPostContent> GeneratePostAsync(string batchId, PostTone tone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate carousel slide captions for experiment results.
    /// </summary>
    /// <param name="batchId">The batch ID to generate captions for</param>
    /// <param name="slideCount">Number of slide captions to generate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of captions for each slide</returns>
    Task<List<string>> GenerateCarouselCaptionsAsync(string batchId, int slideCount, CancellationToken cancellationToken = default);
}

public class InsightSummarizerService : IInsightSummarizerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKernelFactory _kernelFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InsightSummarizerService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public InsightSummarizerService(
        IServiceScopeFactory scopeFactory,
        IKernelFactory kernelFactory,
        IConfiguration configuration,
        ILogger<InsightSummarizerService> logger)
    {
        _scopeFactory = scopeFactory;
        _kernelFactory = kernelFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LinkedInPostContent> GeneratePostAsync(
        string batchId,
        PostTone tone,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating LinkedIn post for batch {BatchId} with tone {Tone}", batchId, tone);

        // Get experiment data
        var experimentData = await GetExperimentDataAsync(batchId, cancellationToken);
        if (experimentData == null)
        {
            throw new InvalidOperationException($"Batch {batchId} not found or has no completed runs");
        }

        // Load prompt template
        var promptTemplate = await LoadPromptTemplateAsync();

        // Build the prompt
        var prompt = promptTemplate
            .Replace("{{experiment_data}}", experimentData)
            .Replace("{{tone}}", tone.ToString());

        // Call AI
        var kernel = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        var responseText = response.Content ?? string.Empty;

        // Parse the response
        var postContent = ParsePostResponse(responseText, batchId, tone);

        _logger.LogInformation(
            "Generated LinkedIn post for batch {BatchId}: {CharCount} characters",
            batchId, postContent.CharacterCount);

        return postContent;
    }

    public async Task<List<string>> GenerateCarouselCaptionsAsync(
        string batchId,
        int slideCount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating {SlideCount} carousel captions for batch {BatchId}",
            slideCount, batchId);

        // Get experiment data
        var experimentData = await GetExperimentDataAsync(batchId, cancellationToken);
        if (experimentData == null)
        {
            throw new InvalidOperationException($"Batch {batchId} not found or has no completed runs");
        }

        var prompt = $@"Generate {slideCount} short, punchy captions for LinkedIn carousel slides based on this experiment data.

EXPERIMENT DATA:
{experimentData}

REQUIREMENTS:
- Each caption should be 1-2 sentences max
- Focus on one key insight per slide
- Use specific numbers from the data
- Make them scroll-stopping
- Progress from hook to conclusion

Return ONLY a JSON array of strings (no markdown code blocks):
[""Caption 1"", ""Caption 2"", ...]";

        var kernel = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        var responseText = response.Content ?? "[]";

        try
        {
            // Clean up response - remove markdown code blocks if present
            responseText = CleanJsonResponse(responseText);
            var captions = JsonSerializer.Deserialize<List<string>>(responseText) ?? new List<string>();
            return captions;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse carousel captions JSON, returning default captions");
            return Enumerable.Range(1, slideCount)
                .Select(i => $"Slide {i}: AI experiment insight")
                .ToList();
        }
    }

    private async Task<string?> GetExperimentDataAsync(string batchId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

        var runs = await dbContext.ExperimentRuns
            .Include(r => r.Claims)
                .ThenInclude(c => c.Receipts)
            .Include(r => r.PositionTests)
            .Where(r => r.BatchId == batchId && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        if (!runs.Any())
        {
            return null;
        }

        // Build a summary for the AI
        var completedRuns = runs.Where(r => r.Status == Models.Experiments.ExperimentStatus.Completed).ToList();
        var firstRun = runs.First();

        var summary = new
        {
            BatchId = batchId,
            ExperimentType = firstRun.ExperimentType.ToString(),
            TotalProviders = runs.Count,
            CompletedProviders = completedRuns.Count,
            TotalTokens = runs.Sum(r => r.TokensUsed),
            TotalCost = runs.Sum(r => r.EstimatedCost),
            Configuration = new
            {
                Persona = firstRun.Persona,
                Temperature = firstRun.Temperature,
                EntryOrder = firstRun.EntryOrder
            },
            ProviderResults = runs.Select(r => new
            {
                Provider = r.Provider,
                Model = r.Model,
                Status = r.Status.ToString(),
                TokensUsed = r.TokensUsed,
                EstimatedCost = r.EstimatedCost,
                DurationSeconds = r.CompletedAt.HasValue
                    ? (r.CompletedAt.Value - r.StartedAt).TotalSeconds
                    : (double?)null,
                PositionResults = r.PositionTests?.Any() == true
                    ? new
                    {
                        StartFound = r.PositionTests.Any(t => t.Position == Models.Experiments.NeedlePosition.Start && t.FactRetrieved),
                        MiddleFound = r.PositionTests.Any(t => t.Position == Models.Experiments.NeedlePosition.Middle && t.FactRetrieved),
                        EndFound = r.PositionTests.Any(t => t.Position == Models.Experiments.NeedlePosition.End && t.FactRetrieved)
                    }
                    : null,
                ClaimCount = r.Claims?.Count ?? 0,
                SupportedClaims = r.Claims?.Count(c => c.IsSupported) ?? 0
            }).ToList(),
            Insights = GenerateInsights(runs)
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private object GenerateInsights(List<Models.Experiments.ExperimentRun> runs)
    {
        var completedRuns = runs.Where(r => r.Status == Models.Experiments.ExperimentStatus.Completed).ToList();
        if (!completedRuns.Any())
        {
            return new { };
        }

        var insights = new List<string>();

        // Cost insights
        var cheapest = completedRuns.OrderBy(r => r.EstimatedCost).First();
        var mostExpensive = completedRuns.OrderByDescending(r => r.EstimatedCost).First();
        if (cheapest.Provider != mostExpensive.Provider && mostExpensive.EstimatedCost > 0)
        {
            var costRatio = mostExpensive.EstimatedCost / Math.Max(cheapest.EstimatedCost, 0.000001m);
            insights.Add($"{mostExpensive.Provider}/{mostExpensive.Model} costs {costRatio:F1}x more than {cheapest.Provider}/{cheapest.Model}");
        }

        // Speed insights
        var withDuration = completedRuns
            .Where(r => r.CompletedAt.HasValue)
            .Select(r => new { r.Provider, r.Model, Duration = (r.CompletedAt!.Value - r.StartedAt).TotalSeconds })
            .OrderBy(r => r.Duration)
            .ToList();

        if (withDuration.Any())
        {
            var fastest = withDuration.First();
            var slowest = withDuration.Last();
            if (fastest.Provider != slowest.Provider)
            {
                insights.Add($"{fastest.Provider}/{fastest.Model} was {slowest.Duration / Math.Max(fastest.Duration, 0.1):F1}x faster than {slowest.Provider}/{slowest.Model}");
            }
        }

        // Position test insights (U-curve)
        var positionRuns = completedRuns.Where(r => r.PositionTests?.Any() == true).ToList();
        if (positionRuns.Any())
        {
            var middleMissedCount = positionRuns.Count(r =>
                r.PositionTests!.Any(t => t.Position == Models.Experiments.NeedlePosition.Middle && !t.FactRetrieved));

            if (middleMissedCount > 0)
            {
                insights.Add($"{middleMissedCount} out of {positionRuns.Count} providers failed to retrieve facts from the middle of context (U-curve attention pattern)");
            }

            var allPositionsFound = positionRuns.Where(r =>
                r.PositionTests!.All(t => t.FactRetrieved)).ToList();

            if (allPositionsFound.Any())
            {
                var providers = string.Join(", ", allPositionsFound.Select(r => $"{r.Provider}/{r.Model}"));
                insights.Add($"Perfect retrieval across all positions: {providers}");
            }
        }

        return new
        {
            KeyFindings = insights,
            CheapestProvider = $"{cheapest.Provider}/{cheapest.Model}",
            CheapestCost = cheapest.EstimatedCost,
            FastestProvider = withDuration.Any() ? $"{withDuration.First().Provider}/{withDuration.First().Model}" : null,
            FastestDuration = withDuration.Any() ? withDuration.First().Duration : (double?)null
        };
    }

    private async Task<string> LoadPromptTemplateAsync()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var promptPath = Path.Combine(basePath, "Prompts", "LinkedInInsights.txt");

        // Try different locations for the prompt file
        var searchPaths = new[]
        {
            promptPath,
            Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "LinkedInInsights.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "MindSetCoach.Api", "Prompts", "LinkedInInsights.txt")
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Loading prompt template from {Path}", path);
                return await File.ReadAllTextAsync(path);
            }
        }

        _logger.LogWarning("Prompt template not found, using embedded default");
        return GetDefaultPromptTemplate();
    }

    private static string GetDefaultPromptTemplate()
    {
        return @"You are an expert content creator. Generate a LinkedIn post from this experiment data.

## EXPERIMENT DATA
{{experiment_data}}

## TONE: {{tone}}

Return ONLY valid JSON:
{
  ""hook"": ""Attention-grabbing first line"",
  ""body"": ""Main content with insights"",
  ""callToAction"": ""Engagement driver"",
  ""hashtags"": [""#AI"", ""#MachineLearning"", ""#LLM"", ""#TechInsights"", ""#DataScience""]
}";
    }

    private LinkedInPostContent ParsePostResponse(string response, string batchId, PostTone tone)
    {
        try
        {
            // Clean up response - remove markdown code blocks if present
            var cleanResponse = CleanJsonResponse(response);

            var parsed = JsonSerializer.Deserialize<LinkedInPostResponse>(cleanResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                throw new JsonException("Parsed result was null");
            }

            var content = new LinkedInPostContent
            {
                Hook = parsed.Hook ?? string.Empty,
                Body = parsed.Body ?? string.Empty,
                CallToAction = parsed.CallToAction ?? string.Empty,
                Hashtags = parsed.Hashtags ?? new List<string>(),
                BatchId = batchId,
                Tone = tone.ToString()
            };

            // Build the full post
            content.FullPost = BuildFullPost(content);

            return content;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON, extracting content manually");

            // Fallback: try to extract content from the response
            return new LinkedInPostContent
            {
                Hook = "AI Experiment Results: Surprising Findings",
                Body = response.Length > 500 ? response.Substring(0, 500) + "..." : response,
                CallToAction = "What patterns have you noticed in your AI experiments?",
                Hashtags = new List<string> { "#AI", "#MachineLearning", "#LLM", "#TechInsights", "#DataScience" },
                BatchId = batchId,
                Tone = tone.ToString(),
                FullPost = response
            };
        }
    }

    private static string CleanJsonResponse(string response)
    {
        var clean = response.Trim();

        // Remove markdown code blocks
        if (clean.StartsWith("```json"))
        {
            clean = clean.Substring(7);
        }
        else if (clean.StartsWith("```"))
        {
            clean = clean.Substring(3);
        }

        if (clean.EndsWith("```"))
        {
            clean = clean.Substring(0, clean.Length - 3);
        }

        return clean.Trim();
    }

    private static string BuildFullPost(LinkedInPostContent content)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(content.Hook))
        {
            parts.Add(content.Hook);
        }

        if (!string.IsNullOrWhiteSpace(content.Body))
        {
            parts.Add(string.Empty); // Empty line for spacing
            parts.Add(content.Body);
        }

        if (!string.IsNullOrWhiteSpace(content.CallToAction))
        {
            parts.Add(string.Empty); // Empty line for spacing
            parts.Add(content.CallToAction);
        }

        if (content.Hashtags.Any())
        {
            parts.Add(string.Empty); // Empty line for spacing
            parts.Add(string.Join(" ", content.Hashtags));
        }

        return string.Join("\n", parts);
    }

    private Kernel CreateKernel()
    {
        // Get provider/model from config or use defaults
        var provider = _configuration["AI:DefaultProvider"] ?? "openai";
        var model = _configuration["AI:DefaultModel"] ?? "gpt-4o-mini";

        // Check if provider is configured
        if (!_kernelFactory.IsProviderConfigured(provider))
        {
            // Fallback to any configured provider
            var providers = new[] { "openai", "anthropic", "deepseek", "google" };
            provider = providers.FirstOrDefault(p => _kernelFactory.IsProviderConfigured(p)) ?? "openai";

            // Adjust model for provider
            model = provider switch
            {
                "anthropic" => "claude-3-haiku-20240307",
                "deepseek" => "deepseek-chat",
                "google" => "gemini-1.5-flash",
                _ => "gpt-4o-mini"
            };
        }

        _logger.LogDebug("Creating kernel with provider {Provider} and model {Model}", provider, model);
        return _kernelFactory.CreateKernel(provider, model);
    }

    private class LinkedInPostResponse
    {
        public string? Hook { get; set; }
        public string? Body { get; set; }
        public string? CallToAction { get; set; }
        public List<string>? Hashtags { get; set; }
    }
}
