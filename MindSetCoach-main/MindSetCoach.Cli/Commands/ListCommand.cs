using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for listing recent experiment runs.
/// Usage: mindsetcoach list --limit 10
/// </summary>
public static class ListCommand
{
    public static Command Create()
    {
        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-n" },
            getDefaultValue: () => 10,
            description: "Maximum number of runs to display");

        var athleteOption = new Option<int?>(
            aliases: new[] { "--athlete", "-a" },
            description: "Filter by athlete ID");

        var typeOption = new Option<string?>(
            aliases: new[] { "--type", "-t" },
            description: "Filter by experiment type: position, persona, or compression");

        var statusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status: running, completed, failed, or pending");

        var batchOption = new Option<string?>(
            aliases: new[] { "--batch", "-b" },
            description: "Filter by batch ID");

        var presetsOption = new Option<bool>(
            aliases: new[] { "--presets" },
            getDefaultValue: () => false,
            description: "List available presets instead of runs");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Show detailed output");

        var command = new Command("list", "List recent experiment runs or presets")
        {
            limitOption,
            athleteOption,
            typeOption,
            statusOption,
            batchOption,
            presetsOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var athleteId = context.ParseResult.GetValueForOption(athleteOption);
            var experimentType = context.ParseResult.GetValueForOption(typeOption);
            var status = context.ParseResult.GetValueForOption(statusOption);
            var batchId = context.ParseResult.GetValueForOption(batchOption);
            var showPresets = context.ParseResult.GetValueForOption(presetsOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            await ExecuteAsync(limit, athleteId, experimentType, status, batchId, showPresets, verbose);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        int limit,
        int? athleteId,
        string? experimentType,
        string? status,
        string? batchId,
        bool showPresets,
        bool verbose)
    {
        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

            if (showPresets)
            {
                await ListPresetsAsync(dbContext, verbose);
                return;
            }

            await ListRunsAsync(dbContext, limit, athleteId, experimentType, status, batchId, verbose);

            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();

            if (verbose)
            {
                Console.WriteLine(ex.StackTrace);
            }

            Environment.ExitCode = 1;
        }
    }

    private static async Task ListPresetsAsync(ExperimentsDbContext dbContext, bool verbose)
    {
        var presets = await dbContext.ExperimentPresets
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (!presets.Any())
        {
            Console.WriteLine("No presets found.");
            return;
        }

        Console.WriteLine($"{"Name",-30} {"Type",-12} {"Default",-8} {"Created",-20}");
        Console.WriteLine(new string('-', 72));

        foreach (var preset in presets)
        {
            var dto = preset.ToDto();
            var defaultLabel = preset.IsDefault ? "Yes" : "";
            Console.WriteLine($"{dto.Name,-30} {dto.Config.ExperimentType,-12} {defaultLabel,-8} {preset.CreatedAt:yyyy-MM-dd HH:mm}");

            if (verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Description: {dto.Description}");
                if (dto.Config.ProviderSweep != null && dto.Config.ProviderSweep.Any())
                {
                    Console.WriteLine($"  Providers: {string.Join(", ", dto.Config.ProviderSweep.Select(p => $"{p.Provider}/{p.Model}"))}");
                }
                else
                {
                    Console.WriteLine($"  Provider: {dto.Config.Provider}/{dto.Config.Model}");
                }
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {presets.Count} presets");
    }

    private static async Task ListRunsAsync(
        ExperimentsDbContext dbContext,
        int limit,
        int? athleteId,
        string? experimentType,
        string? status,
        string? batchId,
        bool verbose)
    {
        var query = dbContext.ExperimentRuns
            .Include(r => r.Claims)
            .Include(r => r.PositionTests)
            .Where(r => !r.IsDeleted)
            .AsQueryable();

        // Apply filters
        if (athleteId.HasValue)
        {
            query = query.Where(r => r.AthleteId == athleteId.Value);
        }

        if (!string.IsNullOrEmpty(experimentType))
        {
            var type = experimentType.ToLower() switch
            {
                "position" => ExperimentType.Position,
                "persona" => ExperimentType.Persona,
                "compression" => ExperimentType.Compression,
                _ => (ExperimentType?)null
            };

            if (type.HasValue)
            {
                query = query.Where(r => r.ExperimentType == type.Value);
            }
        }

        if (!string.IsNullOrEmpty(status))
        {
            var statusEnum = status.ToLower() switch
            {
                "running" => ExperimentStatus.Running,
                "completed" => ExperimentStatus.Completed,
                "failed" => ExperimentStatus.Failed,
                "pending" => ExperimentStatus.Pending,
                _ => (ExperimentStatus?)null
            };

            if (statusEnum.HasValue)
            {
                query = query.Where(r => r.Status == statusEnum.Value);
            }
        }

        if (!string.IsNullOrEmpty(batchId))
        {
            query = query.Where(r => r.BatchId == batchId);
        }

        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync();

        if (!runs.Any())
        {
            Console.WriteLine("No experiment runs found.");
            return;
        }

        // Table header
        Console.WriteLine($"{"ID",-6} {"Type",-12} {"Provider",-20} {"Status",-10} {"Athlete",-8} {"Started",-18} {"Cost",-10}");
        Console.WriteLine(new string('-', 90));

        foreach (var run in runs)
        {
            var statusColor = run.Status switch
            {
                ExperimentStatus.Completed => ConsoleColor.Green,
                ExperimentStatus.Failed => ConsoleColor.Red,
                ExperimentStatus.Running => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray
            };

            var providerModel = $"{run.Provider}/{run.Model}";
            if (providerModel.Length > 20)
            {
                providerModel = providerModel.Substring(0, 17) + "...";
            }

            var costStr = run.EstimatedCost > 0 ? $"${run.EstimatedCost:F6}" : "-";

            Console.Write($"{run.Id,-6} {run.ExperimentType.ToString().ToLower(),-12} {providerModel,-20} ");

            Console.ForegroundColor = statusColor;
            Console.Write($"{run.Status.ToString().ToLower(),-10} ");
            Console.ResetColor();

            Console.WriteLine($"{run.AthleteId,-8} {run.StartedAt:MM/dd HH:mm:ss}  {costStr,-10}");

            if (verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                if (!string.IsNullOrEmpty(run.BatchId))
                {
                    Console.WriteLine($"  Batch: {run.BatchId}");
                }
                if (run.Claims != null && run.Claims.Any())
                {
                    var supported = run.Claims.Count(c => c.IsSupported);
                    Console.WriteLine($"  Claims: {run.Claims.Count} total, {supported} supported");
                }
                if (run.PositionTests != null && run.PositionTests.Any())
                {
                    var found = run.PositionTests.Count(t => t.FactRetrieved);
                    Console.WriteLine($"  Position tests: {run.PositionTests.Count} total, {found} found needle");
                }
                if (run.TokensUsed > 0)
                {
                    Console.WriteLine($"  Tokens: {run.TokensUsed:N0}");
                }
                Console.ResetColor();
            }
        }

        Console.WriteLine();

        // Summary
        var totalRuns = await query.CountAsync();
        if (totalRuns > limit)
        {
            Console.WriteLine($"Showing {runs.Count} of {totalRuns} runs (use --limit to see more)");
        }
        else
        {
            Console.WriteLine($"Total: {runs.Count} runs");
        }

        // Group by batch if there are batches
        var batches = runs.Where(r => !string.IsNullOrEmpty(r.BatchId))
            .GroupBy(r => r.BatchId)
            .ToList();

        if (batches.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Recent batches:");
            foreach (var batch in batches.Take(5))
            {
                var batchRuns = batch.ToList();
                var completed = batchRuns.Count(r => r.Status == ExperimentStatus.Completed);
                Console.WriteLine($"  {batch.Key}: {completed}/{batchRuns.Count} completed");
            }
        }
    }
}
