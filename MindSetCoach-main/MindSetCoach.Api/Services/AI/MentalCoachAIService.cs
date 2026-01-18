using Microsoft.SemanticKernel;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// Mental coaching AI service powered by Semantic Kernel.
/// Supports Goggins/Lasso personas and context engineering experiments.
/// </summary>
public class MentalCoachAIService : IMentalCoachAIService
{
    private readonly Kernel _defaultKernel;
    private readonly IKernelFactory _kernelFactory;
    private readonly IJournalService _journalService;
    private readonly ILogger<MentalCoachAIService> _logger;
    private readonly ContextExperimentLogger _experimentLogger;

    public MentalCoachAIService(
        Kernel kernel,
        IKernelFactory kernelFactory,
        IJournalService journalService,
        ILogger<MentalCoachAIService> logger,
        ContextExperimentLogger experimentLogger)
    {
        _defaultKernel = kernel;
        _kernelFactory = kernelFactory;
        _journalService = journalService;
        _logger = logger;
        _experimentLogger = experimentLogger;
    }

    private Kernel GetKernel(string? provider, string? model)
    {
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
        {
            return _defaultKernel;
        }

        return _kernelFactory.CreateKernel(provider, model);
    }

    #region Weekly Summary

    public async Task<WeeklySummaryResponse> GenerateWeeklySummaryAsync(
        int athleteId,
        string persona,
        ContextOptions? options = null,
        string? provider = null,
        string? model = null)
    {
        options ??= new ContextOptions();

        // Get entries
        var allEntries = await _journalService.GetAthleteEntriesAsync(athleteId);

        // Apply context engineering options
        var contextEntries = ApplyContextOptions(allEntries, options);

        if (!contextEntries.Any())
        {
            return new WeeklySummaryResponse
            {
                Summary = "No journal entries found for this period.",
                Persona = persona,
                EntriesAnalyzed = 0,
                ContextOptionsUsed = options
            };
        }

        // Build prompts
        var systemPrompt = GetPersonaPrompt(persona);
        var userPrompt = BuildSummaryPrompt(contextEntries, options);

        // Estimate tokens (rough: ~4 chars per token)
        var estimatedTokens = (systemPrompt.Length + userPrompt.Length) / 4;

        // Log for experiments
        _experimentLogger.LogContextSent(new ContextLog
        {
            AthleteId = athleteId,
            Persona = persona,
            EntryCount = contextEntries.Count,
            TotalTokensEstimate = estimatedTokens,
            ContextOptions = options,
            Timestamp = DateTime.UtcNow,
            PromptLength = userPrompt.Length
        });

        try
        {
            // Get the appropriate kernel (default or provider-specific)
            var kernel = GetKernel(provider, model);

            // Call the LLM
            var result = await kernel.InvokePromptAsync(
                userPrompt,
                new KernelArguments
                {
                    ["system_prompt"] = systemPrompt
                });

            var summary = result.ToString();

            // Log result
            _experimentLogger.LogResult(new ResultLog
            {
                AthleteId = athleteId,
                Persona = persona,
                Success = true,
                ResponseLength = summary.Length,
                Timestamp = DateTime.UtcNow
            });

            return new WeeklySummaryResponse
            {
                Summary = summary,
                Persona = persona,
                EntriesAnalyzed = contextEntries.Count,
                TokensUsed = estimatedTokens,
                ContextOptionsUsed = options
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary for athlete {AthleteId} with provider {Provider}/{Model}",
                athleteId, provider ?? "default", model ?? "default");
            _experimentLogger.LogResult(new ResultLog
            {
                AthleteId = athleteId,
                Persona = persona,
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
            throw;
        }
    }

    #endregion

    #region Pattern Analysis

    public async Task<PatternAnalysisResponse> AnalyzePatternsAsync(
        int athleteId,
        int daysToAnalyze = 30)
    {
        var allEntries = await _journalService.GetAthleteEntriesAsync(athleteId);
        var entries = allEntries
            .Where(e => e.EntryDate >= DateTime.UtcNow.AddDays(-daysToAnalyze))
            .OrderByDescending(e => e.EntryDate)
            .ToList();

        if (!entries.Any())
        {
            return new PatternAnalysisResponse
            {
                OverallTrend = "insufficient_data",
                DaysAnalyzed = daysToAnalyze
            };
        }

        var prompt = BuildPatternAnalysisPrompt(entries);

        var result = await _defaultKernel.InvokePromptAsync(prompt);

        // Parse structured response (in production, use function calling for structured output)
        return ParsePatternResponse(result.ToString(), daysToAnalyze);
    }

    #endregion

    #region Flag Recommendation

    public async Task<FlagRecommendation> ShouldFlagEntryAsync(int entryId)
    {
        var entry = await _journalService.GetEntryAsync(entryId);

        var prompt = $"""
            Analyze this athlete's journal entry for concerning patterns that a coach should be aware of.
            
            Entry Date: {entry.EntryDate:yyyy-MM-dd}
            Emotional State: {entry.EmotionalState}
            Session Reflection: {entry.SessionReflection}
            Mental Barriers: {entry.MentalBarriers}
            
            Should this entry be flagged for coach attention?
            Consider: signs of burnout, injury risk, excessive anxiety, negative self-talk, or declining motivation.
            
            Respond in this exact format:
            SHOULD_FLAG: [yes/no]
            SEVERITY: [low/medium/high]
            REASON: [one sentence explanation]
            INDICATORS: [comma-separated list of concern indicators found]
            """;

        var result = await _defaultKernel.InvokePromptAsync(prompt);
        return ParseFlagResponse(result.ToString());
    }

    #endregion

    #region Experiments

    public async Task<PositionTestResult> RunPositionTestAsync(
        int athleteId,
        string needleFact,
        string persona = "lasso",
        string? provider = null,
        string? model = null)
    {
        var allEntries = await _journalService.GetAthleteEntriesAsync(athleteId);

        if (allEntries.Count < 3)
        {
            return new PositionTestResult
            {
                NeedleFact = needleFact,
                Conclusion = "Insufficient entries for position test (need at least 3)"
            };
        }

        var results = new List<PositionTestOutcome>();

        foreach (var position in new[] { "start", "middle", "end" })
        {
            var modifiedEntries = InjectFactAtPosition(allEntries.ToList(), needleFact, position);

            var summary = await GenerateWeeklySummaryAsync(athleteId, persona,
                new ContextOptions
                {
                    MaxEntries = modifiedEntries.Count,
                    CompressEntries = false,
                    IncludeMetadata = true
                },
                provider,
                model);

            // Check if the needle fact was retrieved
            var factRetrieved = summary.Summary.Contains(needleFact, StringComparison.OrdinalIgnoreCase) ||
                               ContainsKeyTerms(summary.Summary, needleFact);

            var factPosition = position switch
            {
                "start" => 1,
                "middle" => modifiedEntries.Count / 2,
                "end" => modifiedEntries.Count,
                _ => 0
            };

            results.Add(new PositionTestOutcome
            {
                Position = position,
                FactRetrieved = factRetrieved,
                GeneratedSummary = summary.Summary,
                TotalEntries = modifiedEntries.Count,
                FactPosition = factPosition
            });

            _experimentLogger.LogPositionTest(new PositionTestLog
            {
                AthleteId = athleteId,
                NeedleFact = needleFact,
                Position = position,
                FactRetrieved = factRetrieved,
                TotalEntries = modifiedEntries.Count,
                Timestamp = DateTime.UtcNow
            });
        }

        // Analyze results
        var startFound = results.First(r => r.Position == "start").FactRetrieved;
        var middleFound = results.First(r => r.Position == "middle").FactRetrieved;
        var endFound = results.First(r => r.Position == "end").FactRetrieved;

        var conclusion = (startFound, middleFound, endFound) switch
        {
            (true, false, true) => "U-CURVE CONFIRMED: Middle position showed retrieval failure.",
            (true, true, true) => "No position effect detected - all positions retrieved successfully.",
            (false, false, false) => "Fact not retrieved in any position - may need different needle fact.",
            _ => $"Mixed results: Start={startFound}, Middle={middleFound}, End={endFound}"
        };

        return new PositionTestResult
        {
            NeedleFact = needleFact,
            Results = results,
            Conclusion = conclusion
        };
    }

    public async Task<CompressionTestResult> RunCompressionTestAsync(
        int athleteId,
        string persona = "lasso",
        string? provider = null,
        string? model = null)
    {
        var allEntries = await _journalService.GetAthleteEntriesAsync(athleteId);

        // Test 1: Full context (all entries, raw)
        var fullResult = await GenerateWeeklySummaryAsync(athleteId, persona,
            new ContextOptions
            {
                MaxEntries = null,
                CompressEntries = false,
                IncludeMetadata = true
            },
            provider,
            model);

        // Test 2: Compressed context (all entries, pre-summarized)
        var compressedResult = await GenerateWeeklySummaryAsync(athleteId, persona,
            new ContextOptions
            {
                MaxEntries = null,
                CompressEntries = true,
                IncludeMetadata = false
            },
            provider,
            model);

        // Test 3: Limited context (last 7 entries only)
        var limitedResult = await GenerateWeeklySummaryAsync(athleteId, persona,
            new ContextOptions
            {
                MaxEntries = 7,
                CompressEntries = false,
                IncludeMetadata = true
            },
            provider,
            model);

        return new CompressionTestResult
        {
            FullContext = new CompressionTestOutcome
            {
                Label = "Full Context (Raw)",
                EntriesUsed = allEntries.Count,
                EstimatedTokens = fullResult.TokensUsed,
                Summary = fullResult.Summary,
                WasCompressed = false
            },
            CompressedContext = new CompressionTestOutcome
            {
                Label = "All Entries (Compressed)",
                EntriesUsed = allEntries.Count,
                EstimatedTokens = compressedResult.TokensUsed,
                Summary = compressedResult.Summary,
                WasCompressed = true
            },
            LimitedContext = new CompressionTestOutcome
            {
                Label = "Last 7 Entries (Raw)",
                EntriesUsed = Math.Min(7, allEntries.Count),
                EstimatedTokens = limitedResult.TokensUsed,
                Summary = limitedResult.Summary,
                WasCompressed = false
            },
            Conclusion = $"Token comparison: Full={fullResult.TokensUsed}, Compressed={compressedResult.TokensUsed}, Limited={limitedResult.TokensUsed}"
        };
    }

    #endregion

    #region Private Methods

    private string GetPersonaPrompt(string persona) => persona.ToLower() switch
    {
        "goggins" => """
            You are a mental performance coach in the style of David Goggins.
            
            VOICE & TONE:
            - Direct, challenging, no-nonsense
            - Push the athlete to embrace discomfort
            - Call out excuses without being cruel
            - Acknowledge genuine effort and progress
            - Use short, punchy sentences
            - Occasional intensity: "Stay hard." "Who's gonna carry the boats?"
            
            CRITICAL RULES:
            - ALWAYS accurately reference specific facts from their journal entries
            - NEVER make up details that aren't in the entries
            - If they mentioned a specific barrier (e.g., "shin splints"), address it directly
            - Challenge their mental barriers, not their physical limitations
            
            Generate a weekly mental performance summary for this athlete.
            """,

        "lasso" => """
            You are a mental performance coach in the style of Ted Lasso.
            
            VOICE & TONE:
            - Warm, encouraging, genuinely optimistic
            - Believe in the athlete's potential
            - Find positives even in difficult weeks
            - Acknowledge struggles with empathy, not dismissal
            - Use folksy wisdom and occasional humor
            - "Be a goldfish" energy - help them let go of bad days
            
            CRITICAL RULES:
            - ALWAYS accurately reference specific facts from their journal entries
            - NEVER make up details that aren't in the entries
            - If they mentioned a specific struggle, acknowledge it with compassion
            - Build confidence without being fake or saccharine
            
            Generate a weekly mental performance summary for this athlete.
            """,

        _ => throw new ArgumentException($"Unknown persona: {persona}. Use 'goggins' or 'lasso'.")
    };

    private List<JournalEntryResponse> ApplyContextOptions(
        List<JournalEntryResponse> entries,
        ContextOptions options)
    {
        var result = entries.AsEnumerable();

        // Filter by days
        if (options.DaysBack.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-options.DaysBack.Value);
            result = result.Where(e => e.EntryDate >= cutoff);
        }

        // Filter flagged only
        if (options.FlaggedOnly)
        {
            result = result.Where(e => e.IsFlagged);
        }

        // Order
        result = options.EntryOrder == "chronological"
            ? result.OrderBy(e => e.EntryDate)
            : result.OrderByDescending(e => e.EntryDate);

        // Limit
        if (options.MaxEntries.HasValue)
        {
            result = result.Take(options.MaxEntries.Value);
        }

        return result.ToList();
    }

    private string BuildSummaryPrompt(List<JournalEntryResponse> entries, ContextOptions options)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Here are the athlete's recent journal entries:");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            if (options.IncludeMetadata)
            {
                sb.AppendLine($"--- Entry #{entry.Id} | {entry.EntryDate:yyyy-MM-dd} {(entry.IsFlagged ? "[FLAGGED]" : "")} ---");
            }

            if (options.CompressEntries)
            {
                // Compressed format: single line summary
                sb.AppendLine($"Feeling: {Truncate(entry.EmotionalState, 50)} | Reflection: {Truncate(entry.SessionReflection, 50)} | Barriers: {Truncate(entry.MentalBarriers, 50)}");
            }
            else
            {
                // Full format
                sb.AppendLine($"Emotional State: {entry.EmotionalState}");
                sb.AppendLine($"Session Reflection: {entry.SessionReflection}");
                sb.AppendLine($"Mental Barriers: {entry.MentalBarriers}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Based on these entries, generate a weekly mental performance summary.");
        sb.AppendLine("Include: key patterns observed, areas of strength, concerns to address, and one specific actionable recommendation.");

        return sb.ToString();
    }

    private string BuildPatternAnalysisPrompt(List<JournalEntryResponse> entries)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Analyze these journal entries for mental performance patterns:");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.AppendLine($"[{entry.EntryDate:yyyy-MM-dd}] Emotional: {entry.EmotionalState} | Barriers: {entry.MentalBarriers}");
        }

        sb.AppendLine();
        sb.AppendLine("""
            Provide analysis in this format:
            TREND: [improving/declining/stable]
            RECURRING_BARRIERS: [comma-separated list]
            RECOMMENDATIONS: [numbered list, max 3]
            """);

        return sb.ToString();
    }

    private List<JournalEntryResponse> InjectFactAtPosition(
        List<JournalEntryResponse> entries,
        string needleFact,
        string position)
    {
        var result = entries.ToList();

        var injectedEntry = new JournalEntryResponse
        {
            Id = -1,
            AthleteId = entries.First().AthleteId,
            AthleteName = entries.First().AthleteName,
            EntryDate = DateTime.UtcNow.AddDays(-1),
            EmotionalState = "Concerned about a specific issue",
            SessionReflection = $"Today's session was affected by {needleFact}. This has been bothering me.",
            MentalBarriers = $"Dealing with {needleFact} and trying to stay focused.",
            IsFlagged = false,
            CreatedAt = DateTime.UtcNow
        };

        var insertIndex = position switch
        {
            "start" => 0,
            "middle" => result.Count / 2,
            "end" => result.Count,
            _ => result.Count / 2
        };

        result.Insert(insertIndex, injectedEntry);
        return result;
    }

    private bool ContainsKeyTerms(string text, string needleFact)
    {
        // Extract key terms from needle fact and check if any appear
        var terms = needleFact.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 3); // Skip short words

        return terms.Any(term => text.ToLower().Contains(term));
    }

    private PatternAnalysisResponse ParsePatternResponse(string response, int daysAnalyzed)
    {
        // Simple parsing - in production, use function calling for structured output
        var result = new PatternAnalysisResponse { DaysAnalyzed = daysAnalyzed };

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("TREND:", StringComparison.OrdinalIgnoreCase))
                result.OverallTrend = line.Substring(6).Trim().ToLower();
            else if (line.StartsWith("RECURRING_BARRIERS:", StringComparison.OrdinalIgnoreCase))
                result.RecurringBarriers = line.Substring(19).Split(',').Select(s => s.Trim()).ToList();
            else if (line.StartsWith("RECOMMENDATIONS:", StringComparison.OrdinalIgnoreCase))
                result.Recommendations.Add(line.Substring(16).Trim());
            else if (line.StartsWith("-") || char.IsDigit(line.FirstOrDefault()))
                result.Recommendations.Add(line.TrimStart('-', '1', '2', '3', '.', ' '));
        }

        return result;
    }

    private FlagRecommendation ParseFlagResponse(string response)
    {
        var result = new FlagRecommendation();

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("SHOULD_FLAG:", StringComparison.OrdinalIgnoreCase))
                result.ShouldFlag = line.ToLower().Contains("yes");
            else if (line.StartsWith("SEVERITY:", StringComparison.OrdinalIgnoreCase))
                result.Severity = line.Substring(9).Trim().ToLower();
            else if (line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase))
                result.Reason = line.Substring(7).Trim();
            else if (line.StartsWith("INDICATORS:", StringComparison.OrdinalIgnoreCase))
                result.ConcernIndicators = line.Substring(11).Split(',').Select(s => s.Trim()).ToList();
        }

        return result;
    }

    private string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    #endregion
}
