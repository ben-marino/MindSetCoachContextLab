using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Logs context engineering experiments to SQLite for later analysis.
/// This is key to understanding what context configurations work best.
/// </summary>
public class ContextExperimentLogger
{
    private readonly ILogger<ContextExperimentLogger> _logger;
    private readonly ExperimentsDbContext _dbContext;

    public ContextExperimentLogger(
        ILogger<ContextExperimentLogger> logger,
        ExperimentsDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Start a new experiment run and return its ID for tracking.
    /// </summary>
    public async Task<ExperimentRun> StartRunAsync(
        string provider,
        string model,
        double temperature,
        string promptVersion,
        int athleteId,
        string persona)
    {
        var run = new ExperimentRun
        {
            Provider = provider,
            Model = model,
            Temperature = temperature,
            PromptVersion = promptVersion,
            AthleteId = athleteId,
            Persona = persona,
            StartedAt = DateTime.UtcNow,
            Status = ExperimentStatus.Running
        };

        _dbContext.ExperimentRuns.Add(run);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "EXPERIMENT_STARTED: RunId={RunId}, Provider={Provider}, Model={Model}, " +
            "Athlete={AthleteId}, Persona={Persona}",
            run.Id, provider, model, athleteId, persona);

        return run;
    }

    /// <summary>
    /// Complete an experiment run with results.
    /// </summary>
    public async Task CompleteRunAsync(
        int runId,
        int tokensUsed,
        decimal estimatedCost,
        bool success)
    {
        var run = await _dbContext.ExperimentRuns.FindAsync(runId);
        if (run == null)
        {
            _logger.LogWarning("EXPERIMENT_NOT_FOUND: RunId={RunId}", runId);
            return;
        }

        run.CompletedAt = DateTime.UtcNow;
        run.TokensUsed = tokensUsed;
        run.EstimatedCost = estimatedCost;
        run.Status = success ? ExperimentStatus.Completed : ExperimentStatus.Failed;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "EXPERIMENT_COMPLETED: RunId={RunId}, Status={Status}, " +
            "Tokens={Tokens}, Cost={Cost:C6}",
            runId, run.Status, tokensUsed, estimatedCost);
    }

    /// <summary>
    /// Log a claim made by the AI response.
    /// </summary>
    public async Task<ExperimentClaim> LogClaimAsync(
        int runId,
        string claimText,
        bool isSupported,
        string persona)
    {
        var claim = new ExperimentClaim
        {
            RunId = runId,
            ClaimText = claimText,
            IsSupported = isSupported,
            Persona = persona
        };

        _dbContext.ExperimentClaims.Add(claim);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "CLAIM_LOGGED: RunId={RunId}, Supported={IsSupported}, " +
            "ClaimText={ClaimText}",
            runId, isSupported, claimText.Length > 100 ? claimText[..100] + "..." : claimText);

        return claim;
    }

    /// <summary>
    /// Log a receipt (evidence) for a claim.
    /// </summary>
    public async Task LogClaimReceiptAsync(
        int claimId,
        int journalEntryId,
        string matchedSnippet,
        DateTime entryDate,
        double confidence)
    {
        var receipt = new ClaimReceipt
        {
            ClaimId = claimId,
            JournalEntryId = journalEntryId,
            MatchedSnippet = matchedSnippet,
            EntryDate = entryDate,
            Confidence = confidence
        };

        _dbContext.ClaimReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "RECEIPT_LOGGED: ClaimId={ClaimId}, EntryId={EntryId}, " +
            "Confidence={Confidence:P0}",
            claimId, journalEntryId, confidence);
    }

    /// <summary>
    /// Log a position test result (for U-curve experiments).
    /// </summary>
    public async Task LogPositionTestAsync(
        int runId,
        NeedlePosition position,
        string needleFact,
        bool factRetrieved,
        string responseSnippet)
    {
        var result = new PositionTest
        {
            RunId = runId,
            Position = position,
            NeedleFact = needleFact,
            FactRetrieved = factRetrieved,
            ResponseSnippet = responseSnippet
        };

        _dbContext.PositionTests.Add(result);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "POSITION_TEST: RunId={RunId}, Position={Position}, " +
            "NeedleFact={NeedleFact}, Retrieved={Retrieved}",
            runId, position, needleFact, factRetrieved);
    }

    /// <summary>
    /// Get summary of all experiments (async version).
    /// </summary>
    public async Task<ExperimentSummary> GetSummaryAsync()
    {
        var runs = await _dbContext.ExperimentRuns.ToListAsync();
        var claims = await _dbContext.ExperimentClaims.ToListAsync();
        var positionTests = await _dbContext.PositionTests.ToListAsync();

        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed).ToList();

        return new ExperimentSummary
        {
            TotalRuns = runs.Count,
            CompletedRuns = completedRuns.Count,
            FailedRuns = runs.Count(r => r.Status == ExperimentStatus.Failed),
            TotalClaims = claims.Count,
            SupportedClaims = claims.Count(c => c.IsSupported),
            TotalPositionTests = positionTests.Count,
            SuccessRate = completedRuns.Count > 0
                ? (double)completedRuns.Count / runs.Count * 100
                : 0,
            AverageTokensUsed = completedRuns.Count > 0
                ? (int)completedRuns.Average(r => r.TokensUsed)
                : 0,
            TotalCost = completedRuns.Sum(r => r.EstimatedCost),
            PositionTestResults = positionTests
                .GroupBy(p => p.Position.ToString())
                .ToDictionary(
                    g => g.Key,
                    g => g.Count() > 0 ? g.Count(p => p.FactRetrieved) / (double)g.Count() * 100 : 0)
        };
    }

    /// <summary>
    /// Get session summary (sync wrapper for backwards compatibility).
    /// </summary>
    public ExperimentSummary GetSessionSummary()
    {
        return GetSummaryAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get recent experiment runs with optional filtering.
    /// </summary>
    public async Task<List<ExperimentRun>> GetRunsAsync(
        int? athleteId = null,
        string? persona = null,
        ExperimentStatus? status = null,
        int limit = 50)
    {
        var query = _dbContext.ExperimentRuns
            .Include(r => r.Claims)
            .Include(r => r.PositionTests)
            .AsQueryable();

        if (athleteId.HasValue)
            query = query.Where(r => r.AthleteId == athleteId.Value);

        if (!string.IsNullOrEmpty(persona))
            query = query.Where(r => r.Persona == persona);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific run with all related data.
    /// </summary>
    public async Task<ExperimentRun?> GetRunAsync(int runId)
    {
        return await _dbContext.ExperimentRuns
            .Include(r => r.Claims)
                .ThenInclude(c => c.Receipts)
            .Include(r => r.PositionTests)
            .FirstOrDefaultAsync(r => r.Id == runId);
    }

    /// <summary>
    /// Export all experiment logs for external analysis.
    /// </summary>
    public ExperimentExport ExportLogs()
    {
        var runs = _dbContext.ExperimentRuns
            .Include(r => r.Claims)
            .Include(r => r.PositionTests)
            .ToList();

        // Convert to legacy format for backwards compatibility
        var contextLogs = runs.Select(r => new ContextLog
        {
            AthleteId = r.AthleteId,
            Persona = r.Persona,
            TotalTokensEstimate = r.TokensUsed,
            Timestamp = r.StartedAt
        }).ToList();

        var resultLogs = runs.Select(r => new ResultLog
        {
            AthleteId = r.AthleteId,
            Persona = r.Persona,
            Success = r.Status == ExperimentStatus.Completed,
            Timestamp = r.CompletedAt ?? r.StartedAt
        }).ToList();

        var positionTestLogs = runs
            .SelectMany(r => r.PositionTests.Select(p => new PositionTestLog
            {
                AthleteId = r.AthleteId,
                NeedleFact = p.NeedleFact,
                Position = p.Position.ToString(),
                FactRetrieved = p.FactRetrieved,
                Timestamp = r.StartedAt
            }))
            .ToList();

        return new ExperimentExport
        {
            ContextLogs = contextLogs,
            ResultLogs = resultLogs,
            PositionTestLogs = positionTestLogs,
            ExportedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clear all experiment logs from the database.
    /// </summary>
    public void ClearLogs()
    {
        _dbContext.ClaimReceipts.RemoveRange(_dbContext.ClaimReceipts);
        _dbContext.ExperimentClaims.RemoveRange(_dbContext.ExperimentClaims);
        _dbContext.PositionTests.RemoveRange(_dbContext.PositionTests);
        _dbContext.ExperimentRuns.RemoveRange(_dbContext.ExperimentRuns);
        _dbContext.SaveChanges();
        _logger.LogInformation("Experiment logs cleared from database");
    }

    #region Legacy compatibility methods (for console output during transitions)

    /// <summary>
    /// Log what context was sent to the LLM (console only, for backwards compatibility).
    /// </summary>
    public void LogContextSent(ContextLog log)
    {
        _logger.LogInformation(
            "CONTEXT_SENT: Athlete={AthleteId}, Persona={Persona}, " +
            "Entries={EntryCount}, Tokens≈{Tokens}, Compressed={Compressed}, " +
            "MaxEntries={MaxEntries}, DaysBack={DaysBack}",
            log.AthleteId,
            log.Persona,
            log.EntryCount,
            log.TotalTokensEstimate,
            log.ContextOptions?.CompressEntries ?? false,
            log.ContextOptions?.MaxEntries,
            log.ContextOptions?.DaysBack);
    }

    /// <summary>
    /// Log the result of an LLM call (console only, for backwards compatibility).
    /// </summary>
    public void LogResult(ResultLog log)
    {
        if (log.Success)
        {
            _logger.LogInformation(
                "RESULT_SUCCESS: Athlete={AthleteId}, Persona={Persona}, " +
                "ResponseLength={ResponseLength}",
                log.AthleteId,
                log.Persona,
                log.ResponseLength);
        }
        else
        {
            _logger.LogWarning(
                "RESULT_FAILURE: Athlete={AthleteId}, Persona={Persona}, " +
                "Error={Error}",
                log.AthleteId,
                log.Persona,
                log.ErrorMessage);
        }
    }

    /// <summary>
    /// Log a position test result (console only, for backwards compatibility).
    /// </summary>
    public void LogPositionTest(PositionTestLog log)
    {
        _logger.LogInformation(
            "POSITION_TEST: Athlete={AthleteId}, Position={Position}, " +
            "NeedleFact={NeedleFact}, Retrieved={Retrieved}, " +
            "TotalEntries={TotalEntries}",
            log.AthleteId,
            log.Position,
            log.NeedleFact,
            log.FactRetrieved,
            log.TotalEntries);
    }

    #endregion
}

#region Log Types (kept for backwards compatibility)

public class ContextLog
{
    public int AthleteId { get; set; }
    public string Persona { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public int TotalTokensEstimate { get; set; }
    public int PromptLength { get; set; }
    public ContextOptions? ContextOptions { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ResultLog
{
    public int AthleteId { get; set; }
    public string Persona { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ResponseLength { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PositionTestLog
{
    public int AthleteId { get; set; }
    public string NeedleFact { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool FactRetrieved { get; set; }
    public int TotalEntries { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ExperimentSummary
{
    public int TotalRuns { get; set; }
    public int CompletedRuns { get; set; }
    public int FailedRuns { get; set; }
    public int TotalClaims { get; set; }
    public int SupportedClaims { get; set; }
    public int TotalPositionTests { get; set; }
    public double SuccessRate { get; set; }
    public int AverageTokensUsed { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, double> PositionTestResults { get; set; } = new();

    // Legacy properties for backwards compatibility
    public int TotalContextLogs => TotalRuns;
    public int TotalResultLogs => TotalRuns;
}

public class ExperimentExport
{
    public List<ContextLog> ContextLogs { get; set; } = new();
    public List<ResultLog> ResultLogs { get; set; } = new();
    public List<PositionTestLog> PositionTestLogs { get; set; } = new();
    public DateTime ExportedAt { get; set; }
}

#endregion
