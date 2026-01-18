using System.Text.Json;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MindSetCoach.Api.Configuration;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Background service that handles automated experiment scheduling.
/// Supports running baseline experiments on startup and scheduled runs via cron expressions.
/// </summary>
public class ExperimentSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentSchedulerService> _logger;
    private readonly ExperimentSchedulerOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ExperimentSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentSchedulerService> logger,
        IOptions<ExperimentSchedulerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExperimentSchedulerService starting...");

        // Handle auto-run on startup
        if (_options.AutoRunOnStartup)
        {
            await HandleStartupExperimentAsync(stoppingToken);
        }

        // Handle scheduled runs if configured
        if (!string.IsNullOrWhiteSpace(_options.Schedule))
        {
            await RunScheduledLoopAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation("No schedule configured. Scheduler will not run periodic experiments.");
        }
    }

    private async Task HandleStartupExperimentAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Checking if baseline experiment should run on startup...");

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

            // Check if any experiments exist
            var hasExperiments = await dbContext.ExperimentRuns
                .AnyAsync(r => !r.IsDeleted, stoppingToken);

            if (hasExperiments)
            {
                _logger.LogInformation("Experiments already exist. Skipping startup baseline experiment.");
                return;
            }

            _logger.LogInformation(
                "No experiments found. Running baseline experiment with preset '{Preset}' for athlete {AthleteId}",
                _options.DefaultPreset,
                _options.DefaultAthleteId);

            await RunPresetExperimentAsync(_options.DefaultPreset, _options.DefaultAthleteId, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running startup baseline experiment");
        }
    }

    private async Task RunScheduledLoopAsync(CancellationToken stoppingToken)
    {
        CronExpression? cronExpression;
        try
        {
            cronExpression = CronExpression.Parse(_options.Schedule);
            _logger.LogInformation(
                "Scheduled experiments enabled with cron expression: {Schedule} (preset: {Preset})",
                _options.Schedule,
                _options.ScheduledPreset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression: {Schedule}. Scheduled experiments disabled.", _options.Schedule);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextOccurrence = cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);

                if (nextOccurrence == null)
                {
                    _logger.LogWarning("No next occurrence found for cron expression. Stopping scheduler.");
                    break;
                }

                var delay = nextOccurrence.Value - now;
                _logger.LogInformation(
                    "Next scheduled experiment at {NextRun} UTC (in {Delay})",
                    nextOccurrence.Value,
                    delay);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                _logger.LogInformation(
                    "Running scheduled experiment with preset '{Preset}' for athlete {AthleteId}",
                    _options.ScheduledPreset,
                    _options.DefaultAthleteId);

                await RunPresetExperimentAsync(_options.ScheduledPreset, _options.DefaultAthleteId, stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled experiment loop. Will retry at next scheduled time.");
                // Wait a bit before checking next occurrence to avoid tight loop on errors
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("ExperimentSchedulerService stopping...");
    }

    private async Task RunPresetExperimentAsync(string presetName, int athleteId, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var experimentRunner = scope.ServiceProvider.GetRequiredService<IExperimentRunnerService>();

        // Find the preset
        var preset = await dbContext.ExperimentPresets
            .FirstOrDefaultAsync(p => p.Name == presetName, stoppingToken);

        if (preset == null)
        {
            _logger.LogWarning("Preset '{PresetName}' not found. Skipping scheduled experiment.", presetName);
            return;
        }

        // Parse the preset config
        PresetConfigDto? config;
        try
        {
            config = JsonSerializer.Deserialize<PresetConfigDto>(preset.Config, JsonOptions);
            if (config == null)
            {
                _logger.LogWarning("Failed to parse preset config for '{PresetName}'.", presetName);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing preset config for '{PresetName}'", presetName);
            return;
        }

        // Check if this is a provider sweep (batch experiment)
        if (config.ProviderSweep != null && config.ProviderSweep.Any())
        {
            await RunBatchExperimentFromPresetAsync(config, athleteId, presetName, stoppingToken);
        }
        else
        {
            await RunSingleExperimentFromPresetAsync(config, athleteId, presetName, experimentRunner, stoppingToken);
        }
    }

    private async Task RunSingleExperimentFromPresetAsync(
        PresetConfigDto config,
        int athleteId,
        string presetName,
        IExperimentRunnerService experimentRunner,
        CancellationToken stoppingToken)
    {
        var request = new RunExperimentRequest
        {
            AthleteId = athleteId,
            ExperimentType = config.ExperimentType,
            Provider = config.Provider,
            Model = config.Model,
            Persona = config.Persona ?? "lasso",
            Temperature = config.Temperature,
            MaxEntries = config.MaxEntries,
            EntryOrder = config.EntryOrder,
            NeedleFact = config.NeedleFact
        };

        try
        {
            var runId = await experimentRunner.StartExperimentAsync(request, stoppingToken);
            _logger.LogInformation(
                "[Scheduler] Started experiment RunId={RunId} from preset '{Preset}' - Type={Type}, Provider={Provider}, Model={Model}",
                runId, presetName, request.ExperimentType, request.Provider, request.Model);

            // Wait for completion and log result
            await WaitForExperimentCompletionAsync(runId, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Failed to run experiment from preset '{Preset}'", presetName);
        }
    }

    private async Task RunBatchExperimentFromPresetAsync(
        PresetConfigDto config,
        int athleteId,
        string presetName,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchService = scope.ServiceProvider.GetRequiredService<IBatchExperimentService>();

        var request = new BatchExperimentRequest
        {
            AthleteId = athleteId,
            ExperimentType = config.ExperimentType,
            Providers = config.ProviderSweep!,
            Persona = config.Persona ?? "lasso",
            Temperature = config.Temperature,
            MaxEntries = config.MaxEntries,
            EntryOrder = config.EntryOrder,
            NeedleFact = config.NeedleFact
        };

        try
        {
            var response = await batchService.StartBatchAsync(request);
            _logger.LogInformation(
                "[Scheduler] Started batch experiment BatchId={BatchId} from preset '{Preset}' - Type={Type}, Providers={ProviderCount}",
                response.BatchId, presetName, request.ExperimentType, request.Providers.Count);

            // Wait for all experiments in batch to complete
            foreach (var runId in response.RunIds)
            {
                await WaitForExperimentCompletionAsync(runId, stoppingToken);
            }

            _logger.LogInformation("[Scheduler] Batch experiment {BatchId} completed", response.BatchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Failed to run batch experiment from preset '{Preset}'", presetName);
        }
    }

    private async Task WaitForExperimentCompletionAsync(int runId, CancellationToken stoppingToken)
    {
        const int maxWaitMinutes = 10;
        const int checkIntervalSeconds = 5;
        var startTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

            var run = await dbContext.ExperimentRuns
                .AsNoTracking()
                .Include(r => r.PositionTests)
                .Include(r => r.Claims)
                .FirstOrDefaultAsync(r => r.Id == runId, stoppingToken);

            if (run == null)
            {
                _logger.LogWarning("[Scheduler] Experiment RunId={RunId} not found", runId);
                return;
            }

            if (run.Status == Models.Experiments.ExperimentStatus.Completed)
            {
                LogExperimentResults(run);
                return;
            }

            if (run.Status == Models.Experiments.ExperimentStatus.Failed)
            {
                _logger.LogWarning("[Scheduler] Experiment RunId={RunId} failed", runId);
                return;
            }

            // Check timeout
            if ((DateTime.UtcNow - startTime).TotalMinutes > maxWaitMinutes)
            {
                _logger.LogWarning("[Scheduler] Experiment RunId={RunId} timed out waiting for completion", runId);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
        }
    }

    private void LogExperimentResults(Models.Experiments.ExperimentRun run)
    {
        _logger.LogInformation(
            "[Scheduler] Experiment RunId={RunId} completed - Provider={Provider}, Model={Model}, Tokens={Tokens}, Cost=${Cost:F4}",
            run.Id, run.Provider, run.Model, run.TokensUsed, run.EstimatedCost);

        // Log position test results if applicable
        if (run.PositionTests != null && run.PositionTests.Any())
        {
            var positionResults = run.PositionTests
                .Select(pt => $"{pt.Position}: {(pt.FactRetrieved ? "Found" : "NotFound")}")
                .ToList();

            _logger.LogInformation(
                "[Scheduler] Position test results for RunId={RunId}: {Results}",
                run.Id,
                string.Join(", ", positionResults));
        }

        // Log claim statistics if applicable
        if (run.Claims != null && run.Claims.Any())
        {
            var totalClaims = run.Claims.Count;
            var supportedClaims = run.Claims.Count(c => c.IsSupported);
            var accuracy = totalClaims > 0 ? (supportedClaims * 100.0 / totalClaims) : 0;

            _logger.LogInformation(
                "[Scheduler] Claim results for RunId={RunId}: {Supported}/{Total} supported ({Accuracy:F1}% accuracy)",
                run.Id, supportedClaims, totalClaims, accuracy);
        }
    }
}
