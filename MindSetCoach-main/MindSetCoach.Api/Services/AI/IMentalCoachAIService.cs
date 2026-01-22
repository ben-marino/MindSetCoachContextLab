namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// AI service for generating mental coaching insights using Semantic Kernel.
/// Supports persona-based responses (Goggins/Lasso) and context engineering experiments.
/// </summary>
public interface IMentalCoachAIService
{
    /// <summary>
    /// Generate a weekly summary for an athlete with a specific coaching persona.
    /// </summary>
    Task<WeeklySummaryResponse> GenerateWeeklySummaryAsync(
        int athleteId,
        string persona,
        ContextOptions? options = null,
        string? provider = null,
        string? model = null);

    /// <summary>
    /// Analyze patterns across journal entries (anxiety trends, recurring barriers, etc.)
    /// </summary>
    Task<PatternAnalysisResponse> AnalyzePatternsAsync(
        int athleteId,
        int daysToAnalyze = 30);

    /// <summary>
    /// Determine if a journal entry should be flagged for coach attention.
    /// </summary>
    Task<FlagRecommendation> ShouldFlagEntryAsync(int entryId);

    /// <summary>
    /// Run a position test experiment - inject a fact at different positions
    /// and measure retrieval accuracy.
    /// </summary>
    Task<PositionTestResult> RunPositionTestAsync(
        int athleteId,
        string needleFact,
        string persona = "lasso",
        string? provider = null,
        string? model = null);

    /// <summary>
    /// Compare output quality across different context configurations.
    /// </summary>
    Task<CompressionTestResult> RunCompressionTestAsync(
        int athleteId,
        string persona = "lasso",
        string? provider = null,
        string? model = null);
}

#region Response DTOs

public class WeeklySummaryResponse
{
    public string Summary { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public int EntriesAnalyzed { get; set; }
    public int TokensUsed { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ContextOptions? ContextOptionsUsed { get; set; }
}

public class PatternAnalysisResponse
{
    public List<string> RecurringBarriers { get; set; } = new();
    public string OverallTrend { get; set; } = string.Empty; // "improving", "declining", "stable"
    public List<string> Recommendations { get; set; } = new();
    public int DaysAnalyzed { get; set; }
}

public class FlagRecommendation
{
    public bool ShouldFlag { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "low", "medium", "high"
    public List<string> ConcernIndicators { get; set; } = new();
}

public class PositionTestResult
{
    public string NeedleFact { get; set; } = string.Empty;
    public List<PositionTestOutcome> Results { get; set; } = new();
    public string Conclusion { get; set; } = string.Empty;
}

public class PositionTestOutcome
{
    public string Position { get; set; } = string.Empty; // "start", "middle", "end"
    public bool FactRetrieved { get; set; }
    public string GeneratedSummary { get; set; } = string.Empty;
    public int TotalEntries { get; set; }
    public int FactPosition { get; set; } // 1-indexed position
}

public class CompressionTestResult
{
    public CompressionTestOutcome FullContext { get; set; } = new();
    public CompressionTestOutcome CompressedContext { get; set; } = new();
    public CompressionTestOutcome LimitedContext { get; set; } = new();
    public string Conclusion { get; set; } = string.Empty;
}

public class CompressionTestOutcome
{
    public string Label { get; set; } = string.Empty;
    public int EntriesUsed { get; set; }
    public int EstimatedTokens { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool WasCompressed { get; set; }
}

#endregion

#region Context Options

/// <summary>
/// Options for controlling what context is sent to the LLM.
/// Use this to run context engineering experiments.
/// </summary>
public class ContextOptions
{
    /// <summary>
    /// Maximum number of entries to include. Null = all entries.
    /// </summary>
    public int? MaxEntries { get; set; }

    /// <summary>
    /// If true, pre-summarize entries before sending to reduce token count.
    /// </summary>
    public bool CompressEntries { get; set; } = false;

    /// <summary>
    /// Order of entries: "chronological" (oldest first) or "reverse" (newest first).
    /// </summary>
    public string EntryOrder { get; set; } = "reverse";

    /// <summary>
    /// Include metadata like dates, flags, entry IDs.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Include only flagged entries.
    /// </summary>
    public bool FlaggedOnly { get; set; } = false;

    /// <summary>
    /// Number of days to look back. Null = all time.
    /// </summary>
    public int? DaysBack { get; set; }
}

#endregion
