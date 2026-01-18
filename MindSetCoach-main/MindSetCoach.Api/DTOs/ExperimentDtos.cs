using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Api.DTOs;

#region Request DTOs

/// <summary>
/// Request body for starting a new experiment run.
/// </summary>
public class RunExperimentRequest
{
    [Required]
    public int AthleteId { get; set; }

    [Required]
    public string ExperimentType { get; set; } = "persona"; // "position", "persona", "compression"

    [Required]
    public string Provider { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    public string Persona { get; set; } = "lasso";

    public double Temperature { get; set; } = 0.7;

    public int? MaxEntries { get; set; }

    public string EntryOrder { get; set; } = "reverse";

    public string? NeedleFact { get; set; } // For position tests
}

/// <summary>
/// Request body for creating a new experiment preset.
/// </summary>
public class CreatePresetRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public PresetConfigDto Config { get; set; } = new();
}

/// <summary>
/// Configuration settings stored in a preset.
/// </summary>
public class PresetConfigDto
{
    public string ExperimentType { get; set; } = "persona";
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Persona { get; set; }
    public string? ComparePersona { get; set; } // For persona comparison experiments
    public double Temperature { get; set; } = 0.7;
    public int? MaxEntries { get; set; }
    public string EntryOrder { get; set; } = "reverse";
    public string? NeedleFact { get; set; }

    /// <summary>
    /// For provider sweep: list of providers to test.
    /// </summary>
    public List<ProviderModelPair>? ProviderSweep { get; set; }
}

/// <summary>
/// Provider and model pair for provider sweep experiments.
/// </summary>
public class ProviderModelPair
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Request body for starting a batch experiment across multiple providers.
/// </summary>
public class BatchExperimentRequest
{
    [Required]
    public int AthleteId { get; set; }

    [Required]
    public string ExperimentType { get; set; } = "position"; // "position", "persona", "compression"

    public string? NeedleFact { get; set; }

    [Required]
    [MinLength(1)]
    public List<ProviderModelPair> Providers { get; set; } = new();

    public double Temperature { get; set; } = 0.7;

    public int? MaxEntries { get; set; }

    public string Persona { get; set; } = "lasso";

    public string EntryOrder { get; set; } = "reverse";
}

#endregion

#region Response DTOs

/// <summary>
/// Response when starting an experiment - returns immediately with run ID.
/// </summary>
public class StartExperimentResponse
{
    public int RunId { get; set; }
    public string Status { get; set; } = "running";
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Summary DTO for listing experiment runs.
/// </summary>
public class ExperimentRunDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string ExperimentType { get; set; } = string.Empty;
    public int AthleteId { get; set; }
    public string Persona { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public int ClaimCount { get; set; }
    public int SupportedClaimCount { get; set; }
    public int PositionTestCount { get; set; }
    public string? BatchId { get; set; }
}

/// <summary>
/// Detailed DTO for a single experiment run including claims and receipts.
/// Formatted for the HTML experiment viewer.
/// </summary>
public class ExperimentRunDetailDto
{
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string PromptVersion { get; set; } = string.Empty;
    public int Entries { get; set; }
    public string Order { get; set; } = string.Empty;
    public int Tokens { get; set; }
    public decimal EstCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public int AthleteId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Claims grouped by persona with their receipts.
    /// </summary>
    public Dictionary<string, List<ClaimDto>> PersonaClaims { get; set; } = new();

    /// <summary>
    /// Position test results (for position experiments).
    /// </summary>
    public PositionResultsDto? PositionResults { get; set; }
}

/// <summary>
/// A single claim with its supporting receipts.
/// </summary>
public class ClaimDto
{
    public int Id { get; set; }
    public string Claim { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public List<ReceiptDto> Receipts { get; set; } = new();
}

/// <summary>
/// Evidence linking a claim to a journal entry.
/// </summary>
public class ReceiptDto
{
    public int EntryId { get; set; }
    public DateTime Date { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

/// <summary>
/// Position test results for start, middle, and end positions.
/// </summary>
public class PositionResultsDto
{
    public PositionOutcomeDto Start { get; set; } = new();
    public PositionOutcomeDto Middle { get; set; } = new();
    public PositionOutcomeDto End { get; set; } = new();
}

/// <summary>
/// Outcome for a single position in a position test.
/// </summary>
public class PositionOutcomeDto
{
    public bool Found { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public string NeedleFact { get; set; } = string.Empty;
}

/// <summary>
/// Server-Sent Event message for experiment progress updates.
/// </summary>
public class ExperimentProgressEvent
{
    public string Type { get; set; } = string.Empty; // "progress", "claim", "position", "complete", "error"
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response DTO for experiment preset.
/// </summary>
public class ExperimentPresetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetConfigDto Config { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Response when starting a batch experiment - returns immediately with batch ID and run IDs.
/// </summary>
public class BatchExperimentResponse
{
    public string BatchId { get; set; } = string.Empty;
    public List<int> RunIds { get; set; } = new();
    public string Status { get; set; } = "running";
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Aggregated results for a batch experiment across multiple providers.
/// </summary>
public class BatchResultsDto
{
    public string BatchId { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public int AthleteId { get; set; }
    public string Status { get; set; } = string.Empty; // "running", "completed", "partial", "failed"
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProviders { get; set; }
    public int CompletedProviders { get; set; }
    public List<ProviderResultDto> ProviderResults { get; set; } = new();
    public BatchComparisonDto? Comparison { get; set; }
}

/// <summary>
/// Results for a single provider in a batch experiment.
/// </summary>
public class ProviderResultDto
{
    public int RunId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public double? DurationSeconds { get; set; }
    public PositionResultsDto? PositionResults { get; set; }
    public Dictionary<string, List<ClaimDto>>? PersonaClaims { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Comparison metrics across providers in a batch experiment.
/// </summary>
public class BatchComparisonDto
{
    public PositionComparisonDto? PositionComparison { get; set; }
    public CostComparisonDto CostComparison { get; set; } = new();
}

/// <summary>
/// Position test comparison across providers.
/// </summary>
public class PositionComparisonDto
{
    public Dictionary<string, bool> StartFound { get; set; } = new();
    public Dictionary<string, bool> MiddleFound { get; set; } = new();
    public Dictionary<string, bool> EndFound { get; set; } = new();
}

/// <summary>
/// Cost comparison across providers.
/// </summary>
public class CostComparisonDto
{
    public string CheapestProvider { get; set; } = string.Empty;
    public string FastestProvider { get; set; } = string.Empty;
    public Dictionary<string, decimal> CostByProvider { get; set; } = new();
    public Dictionary<string, double> DurationByProvider { get; set; } = new();
}

/// <summary>
/// SSE event for batch experiment progress.
/// </summary>
public class BatchProgressEvent
{
    public string Type { get; set; } = string.Empty; // "provider_started", "provider_complete", "provider_error", "batch_complete"
    public string BatchId { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? RunId { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region Mapping Extensions

public static class ExperimentDtoMapper
{
    public static ExperimentRunDto ToDto(this ExperimentRun run)
    {
        return new ExperimentRunDto
        {
            Id = run.Id,
            Provider = run.Provider,
            Model = run.Model,
            Temperature = run.Temperature,
            ExperimentType = run.ExperimentType.ToString().ToLower(),
            AthleteId = run.AthleteId,
            Persona = run.Persona,
            Status = run.Status.ToString().ToLower(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            TokensUsed = run.TokensUsed,
            EstimatedCost = run.EstimatedCost,
            ClaimCount = run.Claims?.Count ?? 0,
            SupportedClaimCount = run.Claims?.Count(c => c.IsSupported) ?? 0,
            PositionTestCount = run.PositionTests?.Count ?? 0,
            BatchId = run.BatchId
        };
    }

    public static ExperimentRunDetailDto ToDetailDto(this ExperimentRun run)
    {
        var detail = new ExperimentRunDetailDto
        {
            Model = run.Model,
            Temperature = run.Temperature,
            PromptVersion = run.PromptVersion,
            Entries = run.EntriesUsed,
            Order = run.EntryOrder,
            Tokens = run.TokensUsed,
            EstCost = run.EstimatedCost,
            Status = run.Status.ToString().ToLower(),
            ExperimentType = run.ExperimentType.ToString().ToLower(),
            AthleteId = run.AthleteId,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt
        };

        // Group claims by persona
        if (run.Claims != null && run.Claims.Any())
        {
            var groupedClaims = run.Claims
                .GroupBy(c => c.Persona.ToLower())
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(c => new ClaimDto
                    {
                        Id = c.Id,
                        Claim = c.ClaimText,
                        IsSupported = c.IsSupported,
                        Receipts = c.Receipts?.Select(r => new ReceiptDto
                        {
                            EntryId = r.JournalEntryId,
                            Date = r.EntryDate,
                            Snippet = r.MatchedSnippet,
                            Source = $"Entry #{r.JournalEntryId}",
                            Confidence = r.Confidence
                        }).ToList() ?? new List<ReceiptDto>()
                    }).ToList()
                );

            detail.PersonaClaims = groupedClaims;
        }

        // Map position test results
        if (run.PositionTests != null && run.PositionTests.Any())
        {
            detail.PositionResults = new PositionResultsDto();

            foreach (var test in run.PositionTests)
            {
                var outcome = new PositionOutcomeDto
                {
                    Found = test.FactRetrieved,
                    Snippet = test.ResponseSnippet,
                    NeedleFact = test.NeedleFact
                };

                switch (test.Position)
                {
                    case NeedlePosition.Start:
                        detail.PositionResults.Start = outcome;
                        break;
                    case NeedlePosition.Middle:
                        detail.PositionResults.Middle = outcome;
                        break;
                    case NeedlePosition.End:
                        detail.PositionResults.End = outcome;
                        break;
                }
            }
        }

        return detail;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ExperimentPresetDto ToDto(this ExperimentPreset preset)
    {
        PresetConfigDto? config = null;
        try
        {
            config = JsonSerializer.Deserialize<PresetConfigDto>(preset.Config, _jsonOptions);
        }
        catch
        {
            config = new PresetConfigDto();
        }

        return new ExperimentPresetDto
        {
            Id = preset.Id,
            Name = preset.Name,
            Description = preset.Description,
            Config = config ?? new PresetConfigDto(),
            CreatedAt = preset.CreatedAt,
            IsDefault = preset.IsDefault
        };
    }

    public static ExperimentPreset ToEntity(this CreatePresetRequest request)
    {
        return new ExperimentPreset
        {
            Name = request.Name,
            Description = request.Description,
            Config = JsonSerializer.Serialize(request.Config, _jsonOptions),
            CreatedAt = DateTime.UtcNow,
            IsDefault = false
        };
    }
}

#endregion
