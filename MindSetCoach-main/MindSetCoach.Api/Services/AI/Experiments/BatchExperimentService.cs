using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;
using MindSetCoach.Api.Services.AI;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Service for running batch experiments across multiple providers in parallel.
/// </summary>
public interface IBatchExperimentService
{
    /// <summary>
    /// Start a batch experiment across multiple providers. Returns immediately with batch ID.
    /// </summary>
    Task<BatchExperimentResponse> StartBatchAsync(BatchExperimentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a channel for streaming batch progress events.
    /// </summary>
    Channel<BatchProgressEvent>? GetBatchProgressChannel(string batchId);

    /// <summary>
    /// Get aggregated results for a batch experiment.
    /// </summary>
    Task<BatchResultsDto?> GetBatchResultsAsync(string batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a batch experiment is still running.
    /// </summary>
    bool IsBatchRunning(string batchId);
}

public class BatchExperimentService : IBatchExperimentService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExperimentRunnerService _experimentRunner;
    private readonly ILogger<BatchExperimentService> _logger;
    private readonly ICostCalculatorService _costCalculator;

    // Track running batches and their progress channels
    private readonly ConcurrentDictionary<string, Channel<BatchProgressEvent>> _batchProgressChannels = new();
    private readonly ConcurrentDictionary<string, BatchState> _runningBatches = new();

    private class BatchState
    {
        public List<int> RunIds { get; set; } = new();
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }

    public BatchExperimentService(
        IServiceScopeFactory scopeFactory,
        IExperimentRunnerService experimentRunner,
        ILogger<BatchExperimentService> logger,
        ICostCalculatorService costCalculator)
    {
        _scopeFactory = scopeFactory;
        _experimentRunner = experimentRunner;
        _logger = logger;
        _costCalculator = costCalculator;
    }

    public async Task<BatchExperimentResponse> StartBatchAsync(BatchExperimentRequest request, CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid().ToString();
        var runIds = new List<int>();

        // Create progress channel for SSE streaming
        var channel = Channel.CreateUnbounded<BatchProgressEvent>();
        _batchProgressChannels[batchId] = channel;

        // Create batch state
        var batchState = new BatchState
        {
            TotalCount = request.Providers.Count,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };
        _runningBatches[batchId] = batchState;

        // Create experiment runs for each provider (sequentially to get IDs)
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

            var experimentType = request.ExperimentType.ToLower() switch
            {
                "position" => ExperimentType.Position,
                "compression" => ExperimentType.Compression,
                _ => ExperimentType.Persona
            };

            foreach (var provider in request.Providers)
            {
                var run = new ExperimentRun
                {
                    Provider = provider.Provider,
                    Model = provider.Model,
                    Temperature = request.Temperature,
                    PromptVersion = "v1",
                    AthleteId = request.AthleteId,
                    Persona = request.Persona,
                    ExperimentType = experimentType,
                    EntryOrder = request.EntryOrder,
                    StartedAt = DateTime.UtcNow,
                    Status = ExperimentStatus.Pending,
                    BatchId = batchId
                };

                dbContext.ExperimentRuns.Add(run);
                await dbContext.SaveChangesAsync(cancellationToken);
                runIds.Add(run.Id);
            }
        }

        batchState.RunIds = runIds;

        _logger.LogInformation(
            "Starting batch experiment {BatchId} with {ProviderCount} providers for athlete {AthleteId}",
            batchId, request.Providers.Count, request.AthleteId);

        // Start all experiments in parallel
        _ = Task.Run(async () =>
        {
            try
            {
                await RunBatchInternalAsync(batchId, request, runIds, channel.Writer, batchState.CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running batch experiment {BatchId}", batchId);
                await channel.Writer.WriteAsync(new BatchProgressEvent
                {
                    Type = "batch_error",
                    BatchId = batchId,
                    Message = $"Batch experiment failed: {ex.Message}"
                });
            }
            finally
            {
                channel.Writer.Complete();
                _batchProgressChannels.TryRemove(batchId, out _);
                _runningBatches.TryRemove(batchId, out _);
            }
        }, batchState.CancellationTokenSource.Token);

        return new BatchExperimentResponse
        {
            BatchId = batchId,
            RunIds = runIds,
            Status = "running",
            Message = $"Batch experiment started with {request.Providers.Count} providers. Use GET /api/experiments/batch/{batchId}/stream to monitor progress."
        };
    }

    public Channel<BatchProgressEvent>? GetBatchProgressChannel(string batchId)
    {
        _batchProgressChannels.TryGetValue(batchId, out var channel);
        return channel;
    }

    public async Task<BatchResultsDto?> GetBatchResultsAsync(string batchId, CancellationToken cancellationToken = default)
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

        var firstRun = runs.First();
        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed).ToList();
        var failedRuns = runs.Where(r => r.Status == ExperimentStatus.Failed).ToList();

        // Determine overall batch status
        var batchStatus = "running";
        if (runs.All(r => r.Status == ExperimentStatus.Completed))
        {
            batchStatus = "completed";
        }
        else if (runs.All(r => r.Status == ExperimentStatus.Failed))
        {
            batchStatus = "failed";
        }
        else if (runs.Any(r => r.Status == ExperimentStatus.Completed || r.Status == ExperimentStatus.Failed))
        {
            batchStatus = runs.All(r => r.Status != ExperimentStatus.Running && r.Status != ExperimentStatus.Pending)
                ? (completedRuns.Any() ? "partial" : "failed")
                : "running";
        }

        var result = new BatchResultsDto
        {
            BatchId = batchId,
            ExperimentType = firstRun.ExperimentType.ToString().ToLower(),
            AthleteId = firstRun.AthleteId,
            Status = batchStatus,
            StartedAt = runs.Min(r => r.StartedAt),
            CompletedAt = batchStatus is "completed" or "partial" or "failed"
                ? runs.Max(r => r.CompletedAt)
                : null,
            TotalProviders = runs.Count,
            CompletedProviders = completedRuns.Count,
            ProviderResults = new List<ProviderResultDto>()
        };

        // Build provider results
        foreach (var run in runs)
        {
            var duration = run.CompletedAt.HasValue
                ? (run.CompletedAt.Value - run.StartedAt).TotalSeconds
                : (double?)null;

            var providerResult = new ProviderResultDto
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
            }

            result.ProviderResults.Add(providerResult);
        }

        // Build comparison data if we have completed results
        if (completedRuns.Any())
        {
            result.Comparison = BuildComparison(completedRuns);
            result.CostSummary = BuildCostSummary(completedRuns);
        }

        return result;
    }

    private BatchCostSummaryDto BuildCostSummary(List<ExperimentRun> completedRuns)
    {
        var totalCost = completedRuns.Sum(r => r.EstimatedCost);
        var totalTokens = completedRuns.Sum(r => r.TokensUsed);
        var providerCount = completedRuns.Count;

        var cheapestRun = completedRuns.OrderBy(r => r.EstimatedCost).FirstOrDefault();
        var mostExpensiveRun = completedRuns.OrderByDescending(r => r.EstimatedCost).FirstOrDefault();

        return new BatchCostSummaryDto
        {
            TotalCost = totalCost,
            TotalTokens = totalTokens,
            AverageCostPerProvider = providerCount > 0 ? totalCost / providerCount : 0,
            AverageTokensPerProvider = providerCount > 0 ? totalTokens / providerCount : 0,
            Currency = "USD",
            CheapestProvider = cheapestRun != null ? $"{cheapestRun.Provider}/{cheapestRun.Model}" : string.Empty,
            CheapestProviderCost = cheapestRun?.EstimatedCost ?? 0,
            MostExpensiveProvider = mostExpensiveRun != null ? $"{mostExpensiveRun.Provider}/{mostExpensiveRun.Model}" : string.Empty,
            MostExpensiveProviderCost = mostExpensiveRun?.EstimatedCost ?? 0
        };
    }

    public bool IsBatchRunning(string batchId)
    {
        return _runningBatches.ContainsKey(batchId);
    }

    private BatchComparisonDto BuildComparison(List<ExperimentRun> completedRuns)
    {
        var comparison = new BatchComparisonDto
        {
            CostComparison = new CostComparisonDto()
        };

        // Build position comparison if applicable
        if (completedRuns.Any(r => r.PositionTests != null && r.PositionTests.Any()))
        {
            comparison.PositionComparison = new PositionComparisonDto();

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
            comparison.CostComparison.CostByProvider[key] = run.EstimatedCost;

            if (run.CompletedAt.HasValue)
            {
                comparison.CostComparison.DurationByProvider[key] = (run.CompletedAt.Value - run.StartedAt).TotalSeconds;
            }
        }

        // Find cheapest and fastest
        if (comparison.CostComparison.CostByProvider.Any())
        {
            comparison.CostComparison.CheapestProvider = comparison.CostComparison.CostByProvider
                .OrderBy(kvp => kvp.Value)
                .First().Key;
        }

        if (comparison.CostComparison.DurationByProvider.Any())
        {
            comparison.CostComparison.FastestProvider = comparison.CostComparison.DurationByProvider
                .OrderBy(kvp => kvp.Value)
                .First().Key;
        }

        return comparison;
    }

    private async Task RunBatchInternalAsync(
        string batchId,
        BatchExperimentRequest request,
        List<int> runIds,
        ChannelWriter<BatchProgressEvent> progress,
        CancellationToken cancellationToken)
    {
        await progress.WriteAsync(new BatchProgressEvent
        {
            Type = "batch_started",
            BatchId = batchId,
            Message = $"Starting batch experiment with {request.Providers.Count} providers"
        }, cancellationToken);

        // Create tasks for each provider
        var tasks = new List<Task>();
        var completedCount = 0;
        var lockObj = new object();

        for (int i = 0; i < request.Providers.Count; i++)
        {
            var provider = request.Providers[i];
            var runId = runIds[i];

            var task = RunSingleProviderAsync(
                batchId,
                runId,
                request,
                provider,
                progress,
                () =>
                {
                    lock (lockObj)
                    {
                        completedCount++;
                        if (_runningBatches.TryGetValue(batchId, out var state))
                        {
                            state.CompletedCount = completedCount;
                        }
                    }
                },
                cancellationToken);

            tasks.Add(task);
        }

        // Wait for all providers to complete
        await Task.WhenAll(tasks);

        // Get final results
        var results = await GetBatchResultsAsync(batchId, cancellationToken);

        await progress.WriteAsync(new BatchProgressEvent
        {
            Type = "batch_complete",
            BatchId = batchId,
            Message = "Batch experiment completed",
            Data = results
        }, cancellationToken);

        _logger.LogInformation("Batch experiment {BatchId} completed", batchId);
    }

    private async Task RunSingleProviderAsync(
        string batchId,
        int runId,
        BatchExperimentRequest request,
        ProviderModelPair provider,
        ChannelWriter<BatchProgressEvent> progress,
        Action onComplete,
        CancellationToken cancellationToken)
    {
        await progress.WriteAsync(new BatchProgressEvent
        {
            Type = "provider_started",
            BatchId = batchId,
            Provider = provider.Provider,
            Model = provider.Model,
            RunId = runId,
            Message = $"Starting {provider.Provider}/{provider.Model}"
        }, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IMentalCoachAIService>();
        var experimentLogger = scope.ServiceProvider.GetRequiredService<ContextExperimentLogger>();
        var mainDbContext = scope.ServiceProvider.GetRequiredService<MindSetCoachDbContext>();

        try
        {
            // Mark as running
            var run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run != null)
            {
                run.Status = ExperimentStatus.Running;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Get entry count
            var entriesUsed = await mainDbContext.JournalEntries
                .CountAsync(e => e.AthleteId == request.AthleteId, cancellationToken);

            // Run the experiment based on type
            var experimentType = request.ExperimentType.ToLower();
            var needleFact = request.NeedleFact ?? "shin splints on Tuesday";

            switch (experimentType)
            {
                case "position":
                    var positionResult = await aiService.RunPositionTestAsync(
                        request.AthleteId,
                        needleFact,
                        request.Persona,
                        provider.Provider,
                        provider.Model);

                    var positionTotalTokens = 0;
                    var positionTotalInputTokens = 0;
                    var positionTotalOutputTokens = 0;

                    foreach (var outcome in positionResult.Results)
                    {
                        var position = outcome.Position.ToLower() switch
                        {
                            "start" => NeedlePosition.Start,
                            "middle" => NeedlePosition.Middle,
                            "end" => NeedlePosition.End,
                            _ => NeedlePosition.Start
                        };

                        await experimentLogger.LogPositionTestAsync(runId, position, needleFact, outcome.FactRetrieved, outcome.GeneratedSummary);

                        // Estimate tokens for this position test
                        var inputTokens = outcome.TotalEntries * 100; // ~100 tokens per entry estimate
                        var outputTokens = outcome.GeneratedSummary.Length / 4;
                        positionTotalInputTokens += inputTokens;
                        positionTotalOutputTokens += outputTokens;
                        positionTotalTokens += inputTokens + outputTokens;
                    }

                    // Update run with position test tokens and cost
                    run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
                    if (run != null)
                    {
                        run.TokensUsed = positionTotalTokens;
                        run.EstimatedCost = _costCalculator.CalculateCost(provider.Provider, provider.Model, positionTotalInputTokens, positionTotalOutputTokens);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    break;

                case "compression":
                    var compressionResult = await aiService.RunCompressionTestAsync(
                        request.AthleteId,
                        request.Persona,
                        provider.Provider,
                        provider.Model);

                    // Calculate total tokens and cost across all compression test variants
                    var compressionTotalTokens = compressionResult.FullContext.EstimatedTokens +
                                                compressionResult.CompressedContext.EstimatedTokens +
                                                compressionResult.LimitedContext.EstimatedTokens;

                    var compressionInputTokens = (int)(compressionTotalTokens * 0.6);
                    var compressionOutputTokens = compressionTotalTokens - compressionInputTokens;

                    // Update run with compression test tokens and cost
                    run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
                    if (run != null)
                    {
                        run.TokensUsed = compressionTotalTokens;
                        run.EstimatedCost = _costCalculator.CalculateCost(provider.Provider, provider.Model, compressionInputTokens, compressionOutputTokens);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    break;

                case "persona":
                default:
                    var claimExtractor = scope.ServiceProvider.GetRequiredService<IClaimExtractorService>();
                    var journalEntries = await mainDbContext.JournalEntries
                        .Where(e => e.AthleteId == request.AthleteId)
                        .OrderByDescending(e => e.EntryDate)
                        .ToListAsync(cancellationToken);

                    var personas = new[] { "goggins", "lasso" };
                    var totalTokens = 0;
                    var totalInputTokens = 0;
                    var totalOutputTokens = 0;
                    var totalCost = 0m;

                    foreach (var persona in personas)
                    {
                        var options = new ContextOptions
                        {
                            MaxEntries = request.MaxEntries,
                            EntryOrder = request.EntryOrder
                        };

                        var summary = await aiService.GenerateWeeklySummaryAsync(
                            request.AthleteId,
                            persona,
                            options,
                            provider.Provider,
                            provider.Model);

                        totalTokens += summary.TokensUsed;
                        totalInputTokens += summary.InputTokens;
                        totalOutputTokens += summary.OutputTokens;
                        totalCost += summary.EstimatedCost;

                        var claims = claimExtractor.ExtractAndVerifyClaims(summary.Summary, journalEntries);

                        foreach (var claim in claims)
                        {
                            var experimentClaim = await experimentLogger.LogClaimAsync(runId, claim.ClaimText, claim.IsSupported, persona);

                            if (claim.MatchedEntryId.HasValue && !string.IsNullOrEmpty(claim.MatchedSnippet))
                            {
                                var matchedEntry = journalEntries.FirstOrDefault(e => e.Id == claim.MatchedEntryId.Value);
                                await experimentLogger.LogClaimReceiptAsync(
                                    experimentClaim.Id,
                                    claim.MatchedEntryId.Value,
                                    claim.MatchedSnippet,
                                    matchedEntry?.EntryDate ?? DateTime.UtcNow,
                                    claim.Confidence);
                            }
                        }
                    }

                    // Update run with tokens and cost
                    run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
                    if (run != null)
                    {
                        run.TokensUsed = totalTokens;
                        run.EstimatedCost = totalCost > 0 ? totalCost : _costCalculator.CalculateCost(provider.Provider, provider.Model, totalInputTokens, totalOutputTokens);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    break;
            }

            // Mark as complete with cost calculation
            run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run != null)
            {
                run.Status = ExperimentStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                run.EntriesUsed = entriesUsed;

                // Calculate cost if not already set
                if (run.EstimatedCost == 0 && run.TokensUsed > 0)
                {
                    // Estimate input/output split (roughly 60% input, 40% output)
                    var inputTokens = (int)(run.TokensUsed * 0.6);
                    var outputTokens = run.TokensUsed - inputTokens;
                    run.EstimatedCost = _costCalculator.CalculateCost(provider.Provider, provider.Model, inputTokens, outputTokens);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await progress.WriteAsync(new BatchProgressEvent
            {
                Type = "provider_complete",
                BatchId = batchId,
                Provider = provider.Provider,
                Model = provider.Model,
                RunId = runId,
                Message = $"Completed {provider.Provider}/{provider.Model}",
                Data = new { cost = run?.EstimatedCost ?? 0, tokens = run?.TokensUsed ?? 0 }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running experiment for {Provider}/{Model} in batch {BatchId}",
                provider.Provider, provider.Model, batchId);

            // Mark as failed
            var run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run != null)
            {
                run.Status = ExperimentStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await progress.WriteAsync(new BatchProgressEvent
            {
                Type = "provider_error",
                BatchId = batchId,
                Provider = provider.Provider,
                Model = provider.Model,
                RunId = runId,
                Message = $"Failed {provider.Provider}/{provider.Model}: {ex.Message}"
            }, cancellationToken);
        }
        finally
        {
            onComplete();
        }
    }
}
