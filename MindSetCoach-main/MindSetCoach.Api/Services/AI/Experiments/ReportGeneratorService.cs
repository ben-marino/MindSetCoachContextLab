using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Service for generating self-contained HTML or JSON reports from experiment results.
/// </summary>
public interface IReportGeneratorService
{
    /// <summary>
    /// Generate an HTML report for a batch experiment.
    /// </summary>
    Task<string> GenerateHtmlReportAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate an HTML report for a list of run IDs.
    /// </summary>
    Task<string> GenerateHtmlReportAsync(List<int> runIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a JSON report for a batch experiment.
    /// </summary>
    Task<ReportDataDto?> GenerateJsonReportAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a JSON report for a list of run IDs.
    /// </summary>
    Task<ReportDataDto?> GenerateJsonReportAsync(List<int> runIds, CancellationToken cancellationToken = default);
}

public class ReportGeneratorService : IReportGeneratorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportGeneratorService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ReportGeneratorService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReportGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> GenerateHtmlReportAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var reportData = await GenerateJsonReportAsync(batchId, cancellationToken);
        if (reportData == null)
        {
            return GenerateErrorHtml($"Batch experiment {batchId} not found");
        }

        return GenerateHtml(reportData);
    }

    public async Task<string> GenerateHtmlReportAsync(List<int> runIds, CancellationToken cancellationToken = default)
    {
        var reportData = await GenerateJsonReportAsync(runIds, cancellationToken);
        if (reportData == null)
        {
            return GenerateErrorHtml("No experiment runs found for the specified IDs");
        }

        return GenerateHtml(reportData);
    }

    public async Task<ReportDataDto?> GenerateJsonReportAsync(string batchId, CancellationToken cancellationToken = default)
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

        return BuildReportData(runs, batchId);
    }

    public async Task<ReportDataDto?> GenerateJsonReportAsync(List<int> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds == null || !runIds.Any())
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

        var runs = await dbContext.ExperimentRuns
            .Include(r => r.Claims)
                .ThenInclude(c => c.Receipts)
            .Include(r => r.PositionTests)
            .Where(r => runIds.Contains(r.Id) && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        if (!runs.Any())
        {
            return null;
        }

        var batchId = runs.First().BatchId ?? $"runs-{string.Join("-", runIds.Take(3))}";
        return BuildReportData(runs, batchId);
    }

    private ReportDataDto BuildReportData(List<ExperimentRun> runs, string batchId)
    {
        var firstRun = runs.First();
        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed).ToList();

        var reportData = new ReportDataDto
        {
            BatchId = batchId,
            GeneratedAt = DateTime.UtcNow,
            ExperimentType = firstRun.ExperimentType.ToString().ToLower(),
            AthleteId = firstRun.AthleteId,
            StartedAt = runs.Min(r => r.StartedAt),
            CompletedAt = runs.Max(r => r.CompletedAt),
            TotalProviders = runs.Count,
            CompletedProviders = completedRuns.Count,
            TotalCost = runs.Sum(r => r.EstimatedCost),
            TotalTokens = runs.Sum(r => r.TokensUsed),
            Configuration = new ExperimentConfigDto
            {
                Persona = firstRun.Persona,
                Temperature = firstRun.Temperature,
                EntryOrder = firstRun.EntryOrder,
                PromptVersion = firstRun.PromptVersion
            }
        };

        // Build provider results
        foreach (var run in runs)
        {
            var duration = run.CompletedAt.HasValue
                ? (run.CompletedAt.Value - run.StartedAt).TotalSeconds
                : (double?)null;

            var providerResult = new ReportProviderResultDto
            {
                RunId = run.Id,
                Provider = run.Provider,
                Model = run.Model,
                Status = run.Status.ToString().ToLower(),
                TokensUsed = run.TokensUsed,
                EstimatedCost = run.EstimatedCost,
                DurationSeconds = duration
            };

            // Add position results if applicable
            if (run.PositionTests != null && run.PositionTests.Any())
            {
                providerResult.PositionResults = new PositionResultsDto();

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
                            providerResult.PositionResults.Start = outcome;
                            break;
                        case NeedlePosition.Middle:
                            providerResult.PositionResults.Middle = outcome;
                            break;
                        case NeedlePosition.End:
                            providerResult.PositionResults.End = outcome;
                            break;
                    }
                }
            }

            // Add claim results if applicable
            if (run.Claims != null && run.Claims.Any())
            {
                providerResult.PersonaClaims = run.Claims
                    .GroupBy(c => c.Persona.ToLower())
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(c => new ReportClaimDto
                        {
                            Id = c.Id,
                            Claim = c.ClaimText,
                            IsSupported = c.IsSupported,
                            Receipts = c.Receipts?.Select(r => new ReportReceiptDto
                            {
                                EntryId = r.JournalEntryId,
                                Date = r.EntryDate,
                                Snippet = r.MatchedSnippet,
                                Confidence = r.Confidence
                            }).ToList() ?? new List<ReportReceiptDto>()
                        }).ToList()
                    );
            }

            reportData.ProviderResults.Add(providerResult);
        }

        // Build comparison data if we have completed results
        if (completedRuns.Any())
        {
            reportData.Comparison = BuildComparison(completedRuns);
        }

        return reportData;
    }

    private ReportComparisonDto BuildComparison(List<ExperimentRun> completedRuns)
    {
        var comparison = new ReportComparisonDto();

        // Build position comparison if applicable
        if (completedRuns.Any(r => r.PositionTests != null && r.PositionTests.Any()))
        {
            comparison.PositionComparison = new ReportPositionComparisonDto();

            foreach (var run in completedRuns)
            {
                var key = $"{run.Provider}/{run.Model}";
                var positionTests = run.PositionTests?.ToList() ?? new List<PositionTest>();

                var startTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.Start);
                var middleTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.Middle);
                var endTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.End);

                if (startTest != null)
                    comparison.PositionComparison.StartFound[key] = startTest.FactRetrieved;
                if (middleTest != null)
                    comparison.PositionComparison.MiddleFound[key] = middleTest.FactRetrieved;
                if (endTest != null)
                    comparison.PositionComparison.EndFound[key] = endTest.FactRetrieved;
            }
        }

        // Build cost comparison
        foreach (var run in completedRuns)
        {
            var key = $"{run.Provider}/{run.Model}";
            comparison.CostByProvider[key] = run.EstimatedCost;
            comparison.TokensByProvider[key] = run.TokensUsed;

            if (run.CompletedAt.HasValue)
            {
                comparison.DurationByProvider[key] = (run.CompletedAt.Value - run.StartedAt).TotalSeconds;
            }
        }

        // Find cheapest and fastest
        if (comparison.CostByProvider.Any())
        {
            comparison.CheapestProvider = comparison.CostByProvider
                .OrderBy(kvp => kvp.Value)
                .First().Key;
        }

        if (comparison.DurationByProvider.Any())
        {
            comparison.FastestProvider = comparison.DurationByProvider
                .OrderBy(kvp => kvp.Value)
                .First().Key;
        }

        return comparison;
    }

    private string GenerateErrorHtml(string message)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Report Error | MindSetCoach</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<body class=""bg-light"">
    <div class=""container py-5"">
        <div class=""alert alert-danger"">
            <h4>Report Generation Error</h4>
            <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateHtml(ReportDataDto reportData)
    {
        var jsonData = JsonSerializer.Serialize(reportData, _jsonOptions);

        var sb = new StringBuilder();
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Experiment Report | MindSetCoach Context Engineering Lab</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">");

        // Embed styles
        sb.AppendLine("<style>");
        sb.AppendLine(GetEmbeddedStyles());
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Embed data as JSON
        sb.AppendLine($@"<script>
const reportData = {jsonData};
</script>");

        // Main container
        sb.AppendLine(@"<div class=""container py-4"">");

        // Header
        sb.AppendLine(GenerateHeaderHtml(reportData));

        // Provider Comparison Grid (for position tests)
        if (reportData.Comparison?.PositionComparison != null)
        {
            sb.AppendLine(GeneratePositionComparisonHtml(reportData));
        }

        // Cost Comparison
        sb.AppendLine(GenerateCostComparisonHtml(reportData));

        // Persona Outputs (if persona experiment)
        if (reportData.ExperimentType == "persona" && reportData.ProviderResults.Any(r => r.PersonaClaims?.Any() == true))
        {
            sb.AppendLine(GeneratePersonaOutputsHtml(reportData));
        }

        // Claim Verification Receipts
        sb.AppendLine(GenerateClaimReceiptsHtml(reportData));

        // Footer
        sb.AppendLine(GenerateFooterHtml(reportData));

        sb.AppendLine("</div>");
        sb.AppendLine(@"<script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js""></script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetEmbeddedStyles()
    {
        return @"
body {
    background: #f8fafc;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
.experiment-card {
    border-radius: 16px;
    border: none;
    box-shadow: 0 4px 20px rgba(0,0,0,0.08);
    overflow: hidden;
}
.persona-goggins {
    background: linear-gradient(135deg, #0f0f0f 0%, #1a1a2e 50%, #16213e 100%);
    color: #ffffff;
}
.persona-goggins .quote {
    color: #ff6b6b;
    font-style: italic;
}
.persona-lasso {
    background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
    color: #78350f;
}
.persona-lasso .quote {
    color: #92400e;
    font-style: italic;
}
.position-bar {
    height: 80px;
    border-radius: 12px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    font-weight: 700;
    font-size: 1.1rem;
    transition: all 0.3s ease;
}
.position-found {
    background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%);
    color: white;
    box-shadow: 0 4px 15px rgba(34, 197, 94, 0.4);
}
.position-missed {
    background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
    color: white;
    box-shadow: 0 4px 15px rgba(239, 68, 68, 0.4);
}
.context-zone {
    padding: 20px;
    border-radius: 16px;
    margin: 10px 0;
    text-align: center;
}
.zone-bright {
    background: linear-gradient(135deg, #dcfce7 0%, #bbf7d0 100%);
    border: 3px solid #22c55e;
}
.zone-dim {
    background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
    border: 3px dashed #ef4444;
    position: relative;
}
.zone-dim::after {
    content: '!! LOW ATTENTION';
    position: absolute;
    bottom: -12px;
    left: 50%;
    transform: translateX(-50%);
    background: #ef4444;
    color: white;
    padding: 4px 12px;
    border-radius: 12px;
    font-size: 0.7rem;
    font-weight: 700;
}
.lab-header {
    background: linear-gradient(135deg, #4f46e5 0%, #7c3aed 50%, #a855f7 100%);
    color: white;
    padding: 2.5rem;
    border-radius: 20px;
    margin-bottom: 2rem;
    box-shadow: 0 10px 40px rgba(79, 70, 229, 0.3);
}
.summary-text {
    font-size: 0.95rem;
    line-height: 1.8;
    white-space: pre-wrap;
}
.highlight-fact {
    background: linear-gradient(90deg, #fef08a, #fde047);
    padding: 2px 8px;
    border-radius: 4px;
    font-weight: 600;
}
.token-bar {
    height: 40px;
    background: #e5e7eb;
    border-radius: 20px;
    overflow: hidden;
    position: relative;
}
.token-fill {
    height: 100%;
    border-radius: 20px;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding-right: 15px;
    font-weight: 700;
    color: white;
    font-size: 0.85rem;
}
.token-full { background: linear-gradient(90deg, #22c55e, #16a34a); }
.token-medium { background: linear-gradient(90deg, #eab308, #ca8a04); }
.token-low { background: linear-gradient(90deg, #ef4444, #dc2626); }
.insight-callout {
    background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
    border-left: 5px solid #3b82f6;
    padding: 1.5rem;
    border-radius: 0 12px 12px 0;
    margin: 1rem 0;
}
.badge-experiment {
    background: rgba(255,255,255,0.2);
    padding: 0.5rem 1rem;
    border-radius: 20px;
    font-weight: 600;
}
.run-meta .badge {
    padding: 0.55rem 0.8rem;
    font-weight: 600;
    letter-spacing: 0.1px;
    font-size: 0.75rem;
}
.run-meta .text-bg-light {
    background: rgba(255,255,255,0.75) !important;
    backdrop-filter: blur(6px);
}
.experiment-section-header {
    background: linear-gradient(135deg, #f1f5f9 0%, #e2e8f0 100%);
    padding: 1.5rem;
    border-radius: 16px;
    margin-bottom: 1.5rem;
}
.receipt-card {
    border: 1px solid #e5e7eb;
    border-radius: 12px;
    padding: 12px 14px;
    margin-bottom: 10px;
    background: #fff;
}
.receipt-meta {
    font-size: 0.8rem;
    color: #6b7280;
    display: flex;
    gap: 10px;
    flex-wrap: wrap;
    margin-top: 6px;
}
.stamp {
    display: inline-block;
    padding: 10px 14px;
    border: 3px solid #ef4444;
    color: #ef4444;
    border-radius: 10px;
    font-weight: 900;
    letter-spacing: 1px;
    transform: rotate(-6deg);
    background: rgba(239, 68, 68, 0.05);
    text-transform: uppercase;
}
mark {
    background: linear-gradient(90deg, #fef08a, #fde047);
    padding: 0 4px;
    border-radius: 4px;
}
.comparison-grid {
    display: grid;
    gap: 2px;
    background: #e5e7eb;
    border-radius: 12px;
    overflow: hidden;
}
.comparison-cell {
    background: white;
    padding: 12px 8px;
    text-align: center;
    min-height: 60px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    position: relative;
    transition: all 0.2s ease;
}
.comparison-cell:hover {
    background: #f8fafc;
}
.comparison-cell.header {
    background: #f1f5f9;
    font-weight: 700;
    font-size: 0.85rem;
    color: #374151;
}
.comparison-cell.row-header {
    background: #f8fafc;
    font-weight: 600;
    font-size: 0.9rem;
    color: #4b5563;
}
.comparison-cell .result-icon {
    font-size: 1.5rem;
    margin-bottom: 4px;
}
.cost-bar-container {
    margin-bottom: 12px;
}
.cost-bar-label {
    display: flex;
    justify-content: space-between;
    margin-bottom: 4px;
    font-size: 0.85rem;
}
.cost-bar-label .provider-name {
    font-weight: 600;
    color: #374151;
}
.cost-bar-label .cost-value {
    color: #6b7280;
}
.cost-bar {
    height: 28px;
    background: #e5e7eb;
    border-radius: 6px;
    overflow: hidden;
    position: relative;
}
.cost-bar-fill {
    height: 100%;
    border-radius: 6px;
    display: flex;
    align-items: center;
    padding-left: 10px;
    font-size: 0.75rem;
    font-weight: 600;
    color: white;
    transition: width 0.5s ease-out;
    min-width: fit-content;
}
.cost-bar-fill.openai { background: linear-gradient(90deg, #10b981, #059669); }
.cost-bar-fill.anthropic { background: linear-gradient(90deg, #f59e0b, #d97706); }
.cost-bar-fill.google { background: linear-gradient(90deg, #4285f4, #1a73e8); }
.cost-bar-fill.deepseek { background: linear-gradient(90deg, #6366f1, #4f46e5); }
.cost-bar-fill.ollama { background: linear-gradient(90deg, #8b5cf6, #7c3aed); }
.cost-bar-fill.default { background: linear-gradient(90deg, #6b7280, #4b5563); }
.claim-item {
    border: 1px solid #e5e7eb;
    border-radius: 8px;
    padding: 12px;
    margin-bottom: 8px;
    background: #fff;
}
.claim-item.supported {
    border-left: 4px solid #22c55e;
}
.claim-item.unsupported {
    border-left: 4px solid #ef4444;
}
.report-footer {
    background: #f1f5f9;
    padding: 1.5rem;
    border-radius: 12px;
    margin-top: 2rem;
    text-align: center;
    color: #6b7280;
    font-size: 0.85rem;
}
@media print {
    .lab-header {
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }
    .position-found, .position-missed, .cost-bar-fill {
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }
}";
    }

    private string GenerateHeaderHtml(ReportDataDto data)
    {
        var statusBadge = data.CompletedProviders == data.TotalProviders
            ? @"<span class=""badge bg-success"">Completed</span>"
            : @"<span class=""badge bg-warning"">Partial</span>";

        return $@"
<div class=""lab-header"">
    <div class=""d-flex justify-content-between align-items-start flex-wrap gap-3"">
        <div>
            <h1 class=""mb-2 fw-bold"">Context Engineering Lab - Experiment Report</h1>
            <p class=""mb-3 opacity-75"">Self-contained experiment results | MindSetCoach Research</p>
            <div class=""d-flex flex-wrap gap-2"">
                <span class=""badge rounded-pill text-bg-light border text-dark"">Batch: {data.BatchId}</span>
                <span class=""badge rounded-pill text-bg-light border text-dark"">Type: {data.ExperimentType}</span>
                <span class=""badge rounded-pill text-bg-light border text-dark"">Athlete: {data.AthleteId}</span>
                {statusBadge}
            </div>
        </div>
        <div class=""text-end"">
            <span class=""badge-experiment"">Generated: {data.GeneratedAt:MMM dd, yyyy HH:mm} UTC</span>
        </div>
    </div>
</div>

<div class=""experiment-section-header"">
    <h2 class=""mb-2"">Experiment Configuration</h2>
    <div class=""run-meta d-flex flex-wrap gap-2 align-items-center"">
        <span class=""badge rounded-pill text-bg-light border"">Persona: <strong>{data.Configuration.Persona}</strong></span>
        <span class=""badge rounded-pill text-bg-light border"">Temperature: <strong>{data.Configuration.Temperature}</strong></span>
        <span class=""badge rounded-pill text-bg-light border"">Entry Order: <strong>{data.Configuration.EntryOrder}</strong></span>
        <span class=""badge rounded-pill text-bg-light border"">Prompt Version: <strong>{data.Configuration.PromptVersion}</strong></span>
        <span class=""badge rounded-pill text-bg-dark"">Total Tokens: <strong>{data.TotalTokens:N0}</strong></span>
        <span class=""badge rounded-pill text-bg-success"">Total Cost: <strong>${data.TotalCost:F6}</strong></span>
        <span class=""badge rounded-pill text-bg-info"">Providers: <strong>{data.CompletedProviders}/{data.TotalProviders}</strong></span>
    </div>
</div>";
    }

    private string GeneratePositionComparisonHtml(ReportDataDto data)
    {
        if (data.Comparison?.PositionComparison == null)
        {
            return string.Empty;
        }

        var providers = data.ProviderResults.Select(p => new { p.Provider, p.Model, Key = $"{p.Provider}/{p.Model}" }).ToList();
        var gridColumns = $"120px repeat({providers.Count}, 1fr)";

        var sb = new StringBuilder();
        sb.AppendLine($@"
<div class=""mb-5"">
    <div class=""experiment-section-header"">
        <h2 class=""mb-2"">Position Retrieval by Provider</h2>
        <p class=""text-muted mb-0"">Testing the ""Lost in the Middle"" phenomenon - where does AI attention drop?</p>
    </div>

    <div class=""card experiment-card mb-4"">
        <div class=""card-header bg-light fw-bold"">Comparison Grid</div>
        <div class=""card-body p-3"">
            <div class=""comparison-grid"" style=""grid-template-columns: {gridColumns};"">");

        // Header row
        sb.AppendLine(@"<div class=""comparison-cell header"">Position</div>");
        foreach (var provider in providers)
        {
            sb.AppendLine($@"<div class=""comparison-cell header"">
                <div>{Encode(provider.Provider)}</div>
                <small class=""text-muted"">{Encode(provider.Model)}</small>
            </div>");
        }

        // Start row
        sb.AppendLine(@"<div class=""comparison-cell row-header"">Start</div>");
        foreach (var provider in providers)
        {
            var found = data.Comparison.PositionComparison.StartFound.TryGetValue(provider.Key, out var startFound) && startFound;
            var icon = found ? "&#10003;" : "&#10007;";
            var color = found ? "color: #22c55e;" : "color: #ef4444;";
            sb.AppendLine($@"<div class=""comparison-cell""><span class=""result-icon"" style=""{color}"">{icon}</span></div>");
        }

        // Middle row
        sb.AppendLine(@"<div class=""comparison-cell row-header"">Middle</div>");
        foreach (var provider in providers)
        {
            var found = data.Comparison.PositionComparison.MiddleFound.TryGetValue(provider.Key, out var middleFound) && middleFound;
            var icon = found ? "&#10003;" : "&#10007;";
            var color = found ? "color: #22c55e;" : "color: #ef4444;";
            sb.AppendLine($@"<div class=""comparison-cell""><span class=""result-icon"" style=""{color}"">{icon}</span></div>");
        }

        // End row
        sb.AppendLine(@"<div class=""comparison-cell row-header"">End</div>");
        foreach (var provider in providers)
        {
            var found = data.Comparison.PositionComparison.EndFound.TryGetValue(provider.Key, out var endFound) && endFound;
            var icon = found ? "&#10003;" : "&#10007;";
            var color = found ? "color: #22c55e;" : "color: #ef4444;";
            sb.AppendLine($@"<div class=""comparison-cell""><span class=""result-icon"" style=""{color}"">{icon}</span></div>");
        }

        sb.AppendLine(@"</div></div></div>");

        // Insight callout
        sb.AppendLine(@"
    <div class=""insight-callout"">
        <strong>Research Insight:</strong> The AI may miss facts when they're buried in the middle of context.
        This is the <strong>""U-Curve of Attention""</strong> - LLMs focus heavily on the start and end of context,
        but the middle becomes a blind spot.
    </div>
</div>");

        return sb.ToString();
    }

    private string GenerateCostComparisonHtml(ReportDataDto data)
    {
        if (data.Comparison == null)
        {
            return string.Empty;
        }

        var maxCost = data.Comparison.CostByProvider.Values.DefaultIfEmpty(0.0001m).Max();
        var maxTokens = data.Comparison.TokensByProvider.Values.DefaultIfEmpty(1).Max();

        var sb = new StringBuilder();
        sb.AppendLine(@"
<div class=""mb-5"">
    <div class=""experiment-section-header"">
        <h2 class=""mb-2"">Cost &amp; Token Comparison</h2>
        <p class=""text-muted mb-0"">Compare costs and token usage across providers.</p>
    </div>

    <div class=""row g-4"">");

        // Token usage column
        sb.AppendLine(@"
        <div class=""col-md-6"">
            <div class=""card experiment-card"">
                <div class=""card-header bg-light fw-bold"">Token Usage by Provider</div>
                <div class=""card-body"">");

        foreach (var (key, tokens) in data.Comparison.TokensByProvider)
        {
            var provider = key.Split('/')[0].ToLower();
            var pct = maxTokens > 0 ? (tokens * 100.0 / maxTokens) : 0;
            sb.AppendLine($@"
                    <div class=""cost-bar-container"">
                        <div class=""cost-bar-label"">
                            <span class=""provider-name"">{Encode(key)}</span>
                            <span class=""cost-value"">{tokens:N0} tokens</span>
                        </div>
                        <div class=""cost-bar"">
                            <div class=""cost-bar-fill {provider}"" style=""width: {pct:F0}%;""></div>
                        </div>
                    </div>");
        }

        sb.AppendLine(@"
                </div>
            </div>
        </div>");

        // Cost column
        sb.AppendLine(@"
        <div class=""col-md-6"">
            <div class=""card experiment-card"">
                <div class=""card-header bg-light fw-bold"">Estimated Cost by Provider</div>
                <div class=""card-body"">");

        foreach (var (key, cost) in data.Comparison.CostByProvider)
        {
            var provider = key.Split('/')[0].ToLower();
            var pct = maxCost > 0 ? ((double)cost * 100.0 / (double)maxCost) : 0;
            var isCheapest = key == data.Comparison.CheapestProvider;
            var badge = isCheapest ? @"<span class=""badge bg-success ms-2"">Cheapest</span>" : "";
            sb.AppendLine($@"
                    <div class=""cost-bar-container"">
                        <div class=""cost-bar-label"">
                            <span class=""provider-name"">{Encode(key)}{badge}</span>
                            <span class=""cost-value"">${cost:F6}</span>
                        </div>
                        <div class=""cost-bar"">
                            <div class=""cost-bar-fill {provider}"" style=""width: {pct:F0}%;""></div>
                        </div>
                    </div>");
        }

        sb.AppendLine(@"
                </div>
            </div>
        </div>
    </div>
</div>");

        return sb.ToString();
    }

    private string GeneratePersonaOutputsHtml(ReportDataDto data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
<div class=""mb-5"">
    <div class=""experiment-section-header"">
        <h2 class=""mb-2"">Persona Outputs</h2>
        <p class=""text-muted mb-0"">Side-by-side comparison of different coaching personas.</p>
    </div>");

        foreach (var result in data.ProviderResults.Where(r => r.PersonaClaims?.Any() == true))
        {
            sb.AppendLine($@"
    <h5 class=""mt-4 mb-3"">{Encode(result.Provider)} / {Encode(result.Model)}</h5>
    <div class=""row g-4 position-relative"">");

            // Goggins
            if (result.PersonaClaims!.TryGetValue("goggins", out var gogginsClaims) && gogginsClaims.Any())
            {
                sb.AppendLine(@"
        <div class=""col-md-6"">
            <div class=""card experiment-card persona-goggins h-100"">
                <div class=""card-body p-4"">
                    <div class=""d-flex align-items-center mb-3"">
                        <span style=""font-size: 2rem"" class=""me-2"">&#128293;</span>
                        <div>
                            <h4 class=""mb-0 fw-bold"">Goggins Mode</h4>
                            <small class=""opacity-75"">No excuses. Stay hard.</small>
                        </div>
                    </div>
                    <div class=""summary-text"">");

                foreach (var claim in gogginsClaims)
                {
                    var statusIcon = claim.IsSupported ? "&#10003;" : "&#10007;";
                    var statusColor = claim.IsSupported ? "#22c55e" : "#ff6b6b";
                    sb.AppendLine($@"<p><span style=""color: {statusColor};"">{statusIcon}</span> {Encode(claim.Claim)}</p>");
                }

                sb.AppendLine(@"</div></div></div></div>");
            }

            // Lasso
            if (result.PersonaClaims!.TryGetValue("lasso", out var lassoClaims) && lassoClaims.Any())
            {
                sb.AppendLine(@"
        <div class=""col-md-6"">
            <div class=""card experiment-card persona-lasso h-100"">
                <div class=""card-body p-4"">
                    <div class=""d-flex align-items-center mb-3"">
                        <span style=""font-size: 2rem"" class=""me-2"">&#128147;</span>
                        <div>
                            <h4 class=""mb-0 fw-bold"">Ted Lasso Mode</h4>
                            <small class=""opacity-75"">Believe. Be a goldfish.</small>
                        </div>
                    </div>
                    <div class=""summary-text"">");

                foreach (var claim in lassoClaims)
                {
                    var statusIcon = claim.IsSupported ? "&#10003;" : "&#10007;";
                    var statusColor = claim.IsSupported ? "#22c55e" : "#92400e";
                    sb.AppendLine($@"<p><span style=""color: {statusColor};"">{statusIcon}</span> {Encode(claim.Claim)}</p>");
                }

                sb.AppendLine(@"</div></div></div></div>");
            }

            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private string GenerateClaimReceiptsHtml(ReportDataDto data)
    {
        var allClaims = data.ProviderResults
            .Where(r => r.PersonaClaims != null)
            .SelectMany(r => r.PersonaClaims!.Values.SelectMany(c => c))
            .Where(c => c.Receipts.Any())
            .ToList();

        if (!allClaims.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine(@"
<div class=""mb-5"">
    <div class=""experiment-section-header"">
        <h2 class=""mb-2"">Claim Verification Receipts</h2>
        <p class=""text-muted mb-0"">Evidence linking AI claims to source journal entries.</p>
    </div>

    <div class=""card experiment-card"">
        <div class=""card-body"">");

        foreach (var claim in allClaims.Take(20)) // Limit to 20 for readability
        {
            var statusClass = claim.IsSupported ? "supported" : "unsupported";
            var statusLabel = claim.IsSupported ? "Supported" : "Unsupported";
            var statusBadgeClass = claim.IsSupported ? "bg-success" : "bg-danger";

            sb.AppendLine($@"
            <div class=""claim-item {statusClass}"">
                <div class=""d-flex justify-content-between align-items-start"">
                    <div class=""fw-bold"">{Encode(claim.Claim)}</div>
                    <span class=""badge {statusBadgeClass}"">{statusLabel}</span>
                </div>");

            if (claim.Receipts.Any())
            {
                sb.AppendLine(@"<div class=""mt-2"">");
                foreach (var receipt in claim.Receipts)
                {
                    sb.AppendLine($@"
                    <div class=""receipt-card"">
                        <div><mark>{Encode(receipt.Snippet)}</mark></div>
                        <div class=""receipt-meta"">
                            <span>Entry #{receipt.EntryId}</span>
                            <span>{receipt.Date:MMM dd, yyyy}</span>
                            <span>Confidence: {receipt.Confidence:P0}</span>
                        </div>
                    </div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>");
        }

        sb.AppendLine(@"
        </div>
    </div>
</div>");

        return sb.ToString();
    }

    private string GenerateFooterHtml(ReportDataDto data)
    {
        return $@"
<div class=""report-footer"">
    <p class=""mb-1""><strong>MindSetCoach Context Engineering Lab</strong></p>
    <p class=""mb-1"">Report generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
    <p class=""mb-1"">Batch ID: {data.BatchId}</p>
    <p class=""mb-0"">This is a self-contained report - no API connection required to view.</p>
</div>";
    }

    private static string Encode(string? text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
}

#region Report DTOs

/// <summary>
/// Complete report data for JSON export or HTML generation.
/// </summary>
public class ReportDataDto
{
    public string BatchId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string ExperimentType { get; set; } = string.Empty;
    public int AthleteId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProviders { get; set; }
    public int CompletedProviders { get; set; }
    public decimal TotalCost { get; set; }
    public int TotalTokens { get; set; }
    public ExperimentConfigDto Configuration { get; set; } = new();
    public List<ReportProviderResultDto> ProviderResults { get; set; } = new();
    public ReportComparisonDto? Comparison { get; set; }
}

public class ExperimentConfigDto
{
    public string Persona { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string EntryOrder { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
}

public class ReportProviderResultDto
{
    public int RunId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public double? DurationSeconds { get; set; }
    public PositionResultsDto? PositionResults { get; set; }
    public Dictionary<string, List<ReportClaimDto>>? PersonaClaims { get; set; }
}

public class ReportClaimDto
{
    public int Id { get; set; }
    public string Claim { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public List<ReportReceiptDto> Receipts { get; set; } = new();
}

public class ReportReceiptDto
{
    public int EntryId { get; set; }
    public DateTime Date { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class ReportComparisonDto
{
    public ReportPositionComparisonDto? PositionComparison { get; set; }
    public string CheapestProvider { get; set; } = string.Empty;
    public string FastestProvider { get; set; } = string.Empty;
    public Dictionary<string, decimal> CostByProvider { get; set; } = new();
    public Dictionary<string, int> TokensByProvider { get; set; } = new();
    public Dictionary<string, double> DurationByProvider { get; set; } = new();
}

public class ReportPositionComparisonDto
{
    public Dictionary<string, bool> StartFound { get; set; } = new();
    public Dictionary<string, bool> MiddleFound { get; set; } = new();
    public Dictionary<string, bool> EndFound { get; set; } = new();
}

#endregion
