namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Logs context engineering experiments for later analysis.
/// This is key to understanding what context configurations work best.
/// </summary>
public class ContextExperimentLogger
{
    private readonly ILogger<ContextExperimentLogger> _logger;
    private readonly List<ContextLog> _contextLogs = new();
    private readonly List<ResultLog> _resultLogs = new();
    private readonly List<PositionTestLog> _positionTestLogs = new();

    public ContextExperimentLogger(ILogger<ContextExperimentLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Log what context was sent to the LLM.
    /// </summary>
    public void LogContextSent(ContextLog log)
    {
        _contextLogs.Add(log);

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
    /// Log the result of an LLM call.
    /// </summary>
    public void LogResult(ResultLog log)
    {
        _resultLogs.Add(log);

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
    /// Log a position test result (for U-curve experiments).
    /// </summary>
    public void LogPositionTest(PositionTestLog log)
    {
        _positionTestLogs.Add(log);

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

    /// <summary>
    /// Get summary of all experiments run in this session.
    /// </summary>
    public ExperimentSummary GetSessionSummary()
    {
        return new ExperimentSummary
        {
            TotalContextLogs = _contextLogs.Count,
            TotalResultLogs = _resultLogs.Count,
            TotalPositionTests = _positionTestLogs.Count,
            SuccessRate = _resultLogs.Any()
                ? (double)_resultLogs.Count(r => r.Success) / _resultLogs.Count * 100
                : 0,
            AverageTokensUsed = _contextLogs.Any()
                ? (int)_contextLogs.Average(c => c.TotalTokensEstimate)
                : 0,
            PositionTestResults = _positionTestLogs
                .GroupBy(p => p.Position)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(p => p.FactRetrieved) / (double)g.Count() * 100)
        };
    }

    /// <summary>
    /// Export all logs for external analysis.
    /// </summary>
    public ExperimentExport ExportLogs()
    {
        return new ExperimentExport
        {
            ContextLogs = _contextLogs.ToList(),
            ResultLogs = _resultLogs.ToList(),
            PositionTestLogs = _positionTestLogs.ToList(),
            ExportedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clear all logs (for starting a fresh experiment session).
    /// </summary>
    public void ClearLogs()
    {
        _contextLogs.Clear();
        _resultLogs.Clear();
        _positionTestLogs.Clear();
        _logger.LogInformation("Experiment logs cleared");
    }
}

#region Log Types

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
    public int TotalContextLogs { get; set; }
    public int TotalResultLogs { get; set; }
    public int TotalPositionTests { get; set; }
    public double SuccessRate { get; set; }
    public int AverageTokensUsed { get; set; }
    public Dictionary<string, double> PositionTestResults { get; set; } = new();
}

public class ExperimentExport
{
    public List<ContextLog> ContextLogs { get; set; } = new();
    public List<ResultLog> ResultLogs { get; set; } = new();
    public List<PositionTestLog> PositionTestLogs { get; set; } = new();
    public DateTime ExportedAt { get; set; }
}

#endregion
