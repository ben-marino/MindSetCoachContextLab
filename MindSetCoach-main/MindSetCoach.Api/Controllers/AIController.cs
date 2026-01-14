using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindSetCoach.Api.Configuration;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Api.Controllers;

/// <summary>
/// AI-powered endpoints for mental coaching features.
/// Includes context engineering experiment endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly IMentalCoachAIService _aiService;
    private readonly ContextExperimentLogger _experimentLogger;
    private readonly AIProviderInfo _providerInfo;
    private readonly ILogger<AIController> _logger;

    public AIController(
        IMentalCoachAIService aiService,
        ContextExperimentLogger experimentLogger,
        AIProviderInfo providerInfo,
        ILogger<AIController> logger)
    {
        _aiService = aiService;
        _experimentLogger = experimentLogger;
        _providerInfo = providerInfo;
        _logger = logger;
    }

    #region Core AI Features

    /// <summary>
    /// Get information about the currently configured AI provider.
    /// </summary>
    [HttpGet("provider")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AIProviderInfo), StatusCodes.Status200OK)]
    public ActionResult<object> GetProviderInfo()
    {
        return Ok(new
        {
            _providerInfo.Provider,
            _providerInfo.ModelId,
            _providerInfo.IsLocal,
            CostPer1KInputTokens = _providerInfo.CostPer1KInputTokens,
            CostPer1KOutputTokens = _providerInfo.CostPer1KOutputTokens,
            CostSummary = _providerInfo.GetCostComparison(),
            ProviderTier = _providerInfo.GetProviderTier(),
            Comparison = GetFullCostComparisonTable()
        });
    }
    
    private static object GetFullCostComparisonTable() => new
    {
        Note = "Cost per 1M tokens (approximate, as of late 2024/early 2025)",
        
        OpenAI = new[]
        {
            new { Model = "gpt-4o-mini", Input = "$0.15", Output = "$0.60", Notes = "Fast, cheap, good quality" },
            new { Model = "gpt-4o", Input = "$2.50", Output = "$10.00", Notes = "Flagship multimodal" },
            new { Model = "o1-preview", Input = "$15.00", Output = "$60.00", Notes = "Reasoning model" },
            new { Model = "o1-mini", Input = "$3.00", Output = "$12.00", Notes = "Smaller reasoning" },
            new { Model = "o3-mini", Input = "$1.10", Output = "$4.40", Notes = "Latest reasoning" }
        },
        
        Anthropic = new[]
        {
            new { Model = "claude-opus-4", Input = "$15.00", Output = "$75.00", Notes = "Most capable" },
            new { Model = "claude-sonnet-4", Input = "$3.00", Output = "$15.00", Notes = "Balanced" },
            new { Model = "claude-3.5-haiku", Input = "$0.80", Output = "$4.00", Notes = "Fast, efficient" }
        },
        
        Google = new[]
        {
            new { Model = "gemini-2.5-pro", Input = "$1.25", Output = "$10.00", Notes = "Latest flagship" },
            new { Model = "gemini-2.5-flash", Input = "$0.15", Output = "$0.60", Notes = "Fast, cheap" },
            new { Model = "gemini-2.0-flash", Input = "$0.10", Output = "$0.40", Notes = "Very fast" }
        },
        
        DeepSeek = new[]
        {
            new { Model = "deepseek-chat", Input = "$0.14", Output = "$0.28", Notes = "Great value" },
            new { Model = "deepseek-reasoner", Input = "$0.55", Output = "$2.19", Notes = "R1 reasoning" }
        },
        
        Local = new[]
        {
            new { Model = "llama3.1:8b", Input = "$0.00", Output = "$0.00", Notes = "Via Ollama" },
            new { Model = "deepseek-r1:14b", Input = "$0.00", Output = "$0.00", Notes = "Via Ollama" },
            new { Model = "mistral:7b", Input = "$0.00", Output = "$0.00", Notes = "Via Ollama" }
        }
    };

    /// <summary>
    /// Generate a weekly summary for an athlete with Goggins or Lasso persona.
    /// </summary>
    /// <param name="athleteId">The athlete's ID</param>
    /// <param name="persona">Coach persona: "goggins" or "lasso"</param>
    /// <param name="maxEntries">Optional: limit number of entries to include</param>
    /// <param name="compress">Optional: compress entries before sending</param>
    /// <param name="daysBack">Optional: only include entries from last N days</param>
    [HttpPost("summary/{athleteId}")]
    [ProducesResponseType(typeof(WeeklySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeeklySummaryResponse>> GenerateSummary(
        int athleteId,
        [FromQuery] string persona = "lasso",
        [FromQuery] int? maxEntries = null,
        [FromQuery] bool compress = false,
        [FromQuery] int? daysBack = null)
    {
        if (persona != "goggins" && persona != "lasso")
        {
            return BadRequest("Persona must be 'goggins' or 'lasso'");
        }

        try
        {
            var options = new ContextOptions
            {
                MaxEntries = maxEntries,
                CompressEntries = compress,
                DaysBack = daysBack
            };

            var result = await _aiService.GenerateWeeklySummaryAsync(athleteId, persona, options);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary for athlete {AthleteId}", athleteId);
            return StatusCode(500, "Error generating summary");
        }
    }

    /// <summary>
    /// Analyze patterns in an athlete's journal entries.
    /// </summary>
    [HttpGet("patterns/{athleteId}")]
    [ProducesResponseType(typeof(PatternAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatternAnalysisResponse>> AnalyzePatterns(
        int athleteId,
        [FromQuery] int daysToAnalyze = 30)
    {
        try
        {
            var result = await _aiService.AnalyzePatternsAsync(athleteId, daysToAnalyze);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get AI recommendation on whether an entry should be flagged.
    /// </summary>
    [HttpGet("flag-recommendation/{entryId}")]
    [ProducesResponseType(typeof(FlagRecommendation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlagRecommendation>> GetFlagRecommendation(int entryId)
    {
        try
        {
            var result = await _aiService.ShouldFlagEntryAsync(entryId);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
    }

    #endregion

    #region Context Engineering Experiments

    /// <summary>
    /// Run a position test to validate the "Lost in the Middle" U-curve.
    /// Injects a fact at start, middle, and end positions to test retrieval.
    /// </summary>
    /// <param name="athleteId">Athlete with existing journal entries</param>
    /// <param name="needleFact">The fact to inject and search for (e.g., "shin splints")</param>
    /// <param name="persona">Persona to use for generation</param>
    [HttpPost("experiments/position-test/{athleteId}")]
    [ProducesResponseType(typeof(PositionTestResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionTestResult>> RunPositionTest(
        int athleteId,
        [FromQuery] string needleFact = "shin splints on Tuesday",
        [FromQuery] string persona = "lasso")
    {
        _logger.LogInformation(
            "Starting position test for athlete {AthleteId} with needle fact: {NeedleFact}",
            athleteId, needleFact);

        var result = await _aiService.RunPositionTestAsync(athleteId, needleFact, persona);
        return Ok(result);
    }

    /// <summary>
    /// Run a compression test to compare different context configurations.
    /// Tests: full context, compressed context, limited context.
    /// </summary>
    [HttpPost("experiments/compression-test/{athleteId}")]
    [ProducesResponseType(typeof(CompressionTestResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompressionTestResult>> RunCompressionTest(
        int athleteId,
        [FromQuery] string persona = "lasso")
    {
        _logger.LogInformation("Starting compression test for athlete {AthleteId}", athleteId);

        var result = await _aiService.RunCompressionTestAsync(athleteId, persona);
        return Ok(result);
    }

    /// <summary>
    /// Get summary of all experiments run in this session.
    /// </summary>
    [HttpGet("experiments/summary")]
    [ProducesResponseType(typeof(ExperimentSummary), StatusCodes.Status200OK)]
    public ActionResult<ExperimentSummary> GetExperimentSummary()
    {
        var summary = _experimentLogger.GetSessionSummary();
        return Ok(summary);
    }

    /// <summary>
    /// Export all experiment logs for analysis.
    /// </summary>
    [HttpGet("experiments/export")]
    [ProducesResponseType(typeof(ExperimentExport), StatusCodes.Status200OK)]
    public ActionResult<ExperimentExport> ExportExperimentLogs()
    {
        var export = _experimentLogger.ExportLogs();
        return Ok(export);
    }

    /// <summary>
    /// Clear experiment logs to start fresh.
    /// </summary>
    [HttpDelete("experiments/logs")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult ClearExperimentLogs()
    {
        _experimentLogger.ClearLogs();
        return NoContent();
    }

    /// <summary>
    /// Compare Goggins vs Lasso output for the same context.
    /// </summary>
    [HttpPost("experiments/persona-compare/{athleteId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ComparePersonas(
        int athleteId,
        [FromQuery] int? maxEntries = 7)
    {
        var options = new ContextOptions { MaxEntries = maxEntries };

        var gogginsResult = await _aiService.GenerateWeeklySummaryAsync(athleteId, "goggins", options);
        var lassoResult = await _aiService.GenerateWeeklySummaryAsync(athleteId, "lasso", options);

        return Ok(new
        {
            Goggins = gogginsResult,
            Lasso = lassoResult,
            TokenComparison = new
            {
                GogginsTokens = gogginsResult.TokensUsed,
                LassoTokens = lassoResult.TokensUsed
            }
        });
    }

    #endregion
}
