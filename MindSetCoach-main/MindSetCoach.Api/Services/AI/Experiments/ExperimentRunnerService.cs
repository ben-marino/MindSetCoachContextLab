using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Configuration;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Service for running experiments in the background with progress streaming.
/// Supports position, persona, and compression experiment types.
/// </summary>
public interface IExperimentRunnerService
{
    /// <summary>
    /// Start an experiment run asynchronously. Returns immediately with run ID.
    /// </summary>
    Task<int> StartExperimentAsync(RunExperimentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a channel for streaming progress events for a specific run.
    /// </summary>
    Channel<ExperimentProgressEvent>? GetProgressChannel(int runId);

    /// <summary>
    /// Check if an experiment is currently running.
    /// </summary>
    bool IsRunning(int runId);
}

public class ExperimentRunnerService : IExperimentRunnerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentRunnerService> _logger;
    private readonly AIProviderInfo _providerInfo;

    // Track running experiments and their progress channels
    private readonly ConcurrentDictionary<int, Channel<ExperimentProgressEvent>> _progressChannels = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningExperiments = new();

    public ExperimentRunnerService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentRunnerService> logger,
        AIProviderInfo providerInfo)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _providerInfo = providerInfo;
    }

    public async Task<int> StartExperimentAsync(RunExperimentRequest request, CancellationToken cancellationToken = default)
    {
        // Create the experiment run record
        int runId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

            var experimentType = request.ExperimentType.ToLower() switch
            {
                "position" => ExperimentType.Position,
                "compression" => ExperimentType.Compression,
                _ => ExperimentType.Persona
            };

            var run = new ExperimentRun
            {
                Provider = request.Provider,
                Model = request.Model,
                Temperature = request.Temperature,
                PromptVersion = "v1",
                AthleteId = request.AthleteId,
                Persona = request.Persona,
                ExperimentType = experimentType,
                EntryOrder = request.EntryOrder,
                StartedAt = DateTime.UtcNow,
                Status = ExperimentStatus.Running
            };

            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);
            runId = run.Id;
        }

        // Create progress channel for SSE streaming
        var channel = Channel.CreateUnbounded<ExperimentProgressEvent>();
        _progressChannels[runId] = channel;

        // Create cancellation token for this run
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningExperiments[runId] = cts;

        // Start the experiment in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await RunExperimentInternalAsync(runId, request, channel.Writer, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running experiment {RunId}", runId);
                await channel.Writer.WriteAsync(new ExperimentProgressEvent
                {
                    Type = "error",
                    Message = $"Experiment failed: {ex.Message}"
                });
            }
            finally
            {
                channel.Writer.Complete();
                _progressChannels.TryRemove(runId, out _);
                _runningExperiments.TryRemove(runId, out _);
            }
        }, cts.Token);

        _logger.LogInformation("Started experiment {RunId} of type {Type} for athlete {AthleteId}",
            runId, request.ExperimentType, request.AthleteId);

        return runId;
    }

    public Channel<ExperimentProgressEvent>? GetProgressChannel(int runId)
    {
        _progressChannels.TryGetValue(runId, out var channel);
        return channel;
    }

    public bool IsRunning(int runId)
    {
        return _runningExperiments.ContainsKey(runId);
    }

    private async Task RunExperimentInternalAsync(
        int runId,
        RunExperimentRequest request,
        ChannelWriter<ExperimentProgressEvent> progress,
        CancellationToken cancellationToken)
    {
        await progress.WriteAsync(new ExperimentProgressEvent
        {
            Type = "progress",
            Message = "Starting experiment..."
        }, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IMentalCoachAIService>();
        var experimentLogger = scope.ServiceProvider.GetRequiredService<ContextExperimentLogger>();
        var claimExtractor = scope.ServiceProvider.GetRequiredService<IClaimExtractorService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var mainDbContext = scope.ServiceProvider.GetRequiredService<MindSetCoachDbContext>();

        var experimentType = request.ExperimentType.ToLower();
        var totalTokens = 0;
        var estimatedCost = 0m;
        var entriesUsed = 0;

        try
        {
            // Get entry count for this athlete
            entriesUsed = await mainDbContext.JournalEntries
                .CountAsync(e => e.AthleteId == request.AthleteId, cancellationToken);

            switch (experimentType)
            {
                case "position":
                    await RunPositionExperimentAsync(runId, request, progress, aiService, experimentLogger, cancellationToken);
                    break;

                case "compression":
                    await RunCompressionExperimentAsync(runId, request, progress, aiService, cancellationToken);
                    break;

                case "persona":
                default:
                    var (tokens, cost) = await RunPersonaExperimentAsync(runId, request, progress, aiService, claimExtractor, experimentLogger, cancellationToken);
                    totalTokens = tokens;
                    estimatedCost = cost;
                    break;
            }

            // Mark experiment as complete
            var run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run != null)
            {
                run.Status = ExperimentStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                run.TokensUsed = totalTokens;
                run.EstimatedCost = estimatedCost;
                run.EntriesUsed = entriesUsed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await progress.WriteAsync(new ExperimentProgressEvent
            {
                Type = "complete",
                Message = "Experiment completed successfully",
                Data = new { runId, tokens = totalTokens, cost = estimatedCost }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Mark experiment as failed
            var run = await dbContext.ExperimentRuns.FindAsync(new object[] { runId }, cancellationToken);
            if (run != null)
            {
                run.Status = ExperimentStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.EntriesUsed = entriesUsed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await progress.WriteAsync(new ExperimentProgressEvent
            {
                Type = "error",
                Message = $"Experiment failed: {ex.Message}"
            }, cancellationToken);

            throw;
        }
    }

    private async Task RunPositionExperimentAsync(
        int runId,
        RunExperimentRequest request,
        ChannelWriter<ExperimentProgressEvent> progress,
        IMentalCoachAIService aiService,
        ContextExperimentLogger experimentLogger,
        CancellationToken cancellationToken)
    {
        var needleFact = request.NeedleFact ?? "shin splints on Tuesday";

        await progress.WriteAsync(new ExperimentProgressEvent
        {
            Type = "progress",
            Message = $"Running position test with needle fact: {needleFact}"
        }, cancellationToken);

        var result = await aiService.RunPositionTestAsync(request.AthleteId, needleFact, request.Persona);

        foreach (var outcome in result.Results)
        {
            var position = outcome.Position.ToLower() switch
            {
                "start" => NeedlePosition.Start,
                "middle" => NeedlePosition.Middle,
                "end" => NeedlePosition.End,
                _ => NeedlePosition.Start
            };

            await experimentLogger.LogPositionTestAsync(runId, position, needleFact, outcome.FactRetrieved, outcome.GeneratedSummary);

            await progress.WriteAsync(new ExperimentProgressEvent
            {
                Type = "position",
                Message = $"Position {outcome.Position}: {(outcome.FactRetrieved ? "Found" : "Not found")}",
                Data = new { position = outcome.Position, found = outcome.FactRetrieved, snippet = outcome.GeneratedSummary }
            }, cancellationToken);
        }
    }

    private async Task RunCompressionExperimentAsync(
        int runId,
        RunExperimentRequest request,
        ChannelWriter<ExperimentProgressEvent> progress,
        IMentalCoachAIService aiService,
        CancellationToken cancellationToken)
    {
        await progress.WriteAsync(new ExperimentProgressEvent
        {
            Type = "progress",
            Message = "Running compression test with full, compressed, and limited contexts..."
        }, cancellationToken);

        var result = await aiService.RunCompressionTestAsync(request.AthleteId, request.Persona);

        await progress.WriteAsync(new ExperimentProgressEvent
        {
            Type = "compression",
            Message = "Compression test completed",
            Data = new
            {
                fullContext = new { entries = result.FullContext.EntriesUsed, tokens = result.FullContext.EstimatedTokens },
                compressedContext = new { entries = result.CompressedContext.EntriesUsed, tokens = result.CompressedContext.EstimatedTokens },
                limitedContext = new { entries = result.LimitedContext.EntriesUsed, tokens = result.LimitedContext.EstimatedTokens },
                conclusion = result.Conclusion
            }
        }, cancellationToken);
    }

    private async Task<(int tokens, decimal cost)> RunPersonaExperimentAsync(
        int runId,
        RunExperimentRequest request,
        ChannelWriter<ExperimentProgressEvent> progress,
        IMentalCoachAIService aiService,
        IClaimExtractorService claimExtractor,
        ContextExperimentLogger experimentLogger,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mainDbContext = scope.ServiceProvider.GetRequiredService<MindSetCoachDbContext>();

        // Get journal entries for claim extraction
        var journalEntries = await mainDbContext.JournalEntries
            .Where(e => e.AthleteId == request.AthleteId)
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync(cancellationToken);

        var personas = new[] { "goggins", "lasso" };
        var totalTokens = 0;
        var totalCost = 0m;

        foreach (var persona in personas)
        {
            await progress.WriteAsync(new ExperimentProgressEvent
            {
                Type = "progress",
                Message = $"Generating summary with {persona} persona..."
            }, cancellationToken);

            var options = new ContextOptions
            {
                MaxEntries = request.MaxEntries,
                EntryOrder = request.EntryOrder
            };

            var summary = await aiService.GenerateWeeklySummaryAsync(request.AthleteId, persona, options);
            totalTokens += summary.TokensUsed;

            // Extract and verify claims
            await progress.WriteAsync(new ExperimentProgressEvent
            {
                Type = "progress",
                Message = $"Extracting claims from {persona} response..."
            }, cancellationToken);

            var claims = claimExtractor.ExtractAndVerifyClaims(summary.Summary, journalEntries);

            foreach (var claim in claims)
            {
                var experimentClaim = await experimentLogger.LogClaimAsync(runId, claim.ClaimText, claim.IsSupported, persona);

                // Log receipt if there's a matched entry
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

                await progress.WriteAsync(new ExperimentProgressEvent
                {
                    Type = "claim",
                    Message = $"[{persona}] Claim: {claim.ClaimText.Substring(0, Math.Min(50, claim.ClaimText.Length))}...",
                    Data = new
                    {
                        persona,
                        claim = claim.ClaimText,
                        isSupported = claim.IsSupported,
                        hasEvidence = claim.MatchedEntryId.HasValue
                    }
                }, cancellationToken);
            }
        }

        // Calculate estimated cost based on provider rates
        totalCost = (totalTokens / 1000m) * (decimal)(_providerInfo.CostPer1KInputTokens + _providerInfo.CostPer1KOutputTokens) / 2;

        return (totalTokens, totalCost);
    }
}
