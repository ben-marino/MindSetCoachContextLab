using System.CommandLine;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for running batch experiments across multiple providers.
/// Usage: mindsetcoach batch --athlete 1 --type position --preset "Full Provider Sweep"
/// </summary>
public static class BatchCommand
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static Command Create()
    {
        var athleteOption = new Option<int>(
            aliases: new[] { "--athlete", "-a" },
            description: "The athlete ID to run the experiment for")
        {
            IsRequired = true
        };

        var typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            getDefaultValue: () => "position",
            description: "Experiment type: position, persona, or compression");

        var presetOption = new Option<string?>(
            aliases: new[] { "--preset", "-P" },
            description: "Name of a saved preset to use (overrides other options)");

        var providersOption = new Option<string[]?>(
            aliases: new[] { "--providers" },
            description: "Comma-separated list of provider:model pairs (e.g., openai:gpt-4o-mini,anthropic:claude-sonnet-4-20250514)");

        var personaOption = new Option<string>(
            aliases: new[] { "--persona" },
            getDefaultValue: () => "lasso",
            description: "Coaching persona: lasso or goggins");

        var temperatureOption = new Option<double>(
            aliases: new[] { "--temperature" },
            getDefaultValue: () => 0.7,
            description: "Model temperature (0.0 to 1.0)");

        var needleOption = new Option<string?>(
            aliases: new[] { "--needle" },
            description: "Needle fact for position tests");

        var maxEntriesOption = new Option<int?>(
            aliases: new[] { "--max-entries" },
            description: "Maximum journal entries to include");

        var orderOption = new Option<string>(
            aliases: new[] { "--order" },
            getDefaultValue: () => "reverse",
            description: "Entry order: reverse or chronological");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Show detailed progress output");

        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Generate HTML report to this file path when complete");

        var command = new Command("batch", "Run batch experiments across multiple providers")
        {
            athleteOption,
            typeOption,
            presetOption,
            providersOption,
            personaOption,
            temperatureOption,
            needleOption,
            maxEntriesOption,
            orderOption,
            verboseOption,
            outputOption
        };

        command.SetHandler(async context =>
        {
            var athleteId = context.ParseResult.GetValueForOption(athleteOption);
            var experimentType = context.ParseResult.GetValueForOption(typeOption)!;
            var presetName = context.ParseResult.GetValueForOption(presetOption);
            var providers = context.ParseResult.GetValueForOption(providersOption);
            var persona = context.ParseResult.GetValueForOption(personaOption)!;
            var temperature = context.ParseResult.GetValueForOption(temperatureOption);
            var needle = context.ParseResult.GetValueForOption(needleOption);
            var maxEntries = context.ParseResult.GetValueForOption(maxEntriesOption);
            var order = context.ParseResult.GetValueForOption(orderOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var outputPath = context.ParseResult.GetValueForOption(outputOption);

            await ExecuteAsync(athleteId, experimentType, presetName, providers, persona, temperature, needle, maxEntries, order, verbose, outputPath);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        int athleteId,
        string experimentType,
        string? presetName,
        string[]? providers,
        string persona,
        double temperature,
        string? needle,
        int? maxEntries,
        string order,
        bool verbose,
        string? outputPath)
    {
        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            List<ProviderModelPair> providerPairs;

            // Load preset if specified
            if (!string.IsNullOrEmpty(presetName))
            {
                Console.WriteLine($"Loading preset: {presetName}");

                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

                var preset = await dbContext.ExperimentPresets
                    .FirstOrDefaultAsync(p => p.Name == presetName);

                if (preset == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Preset not found: {presetName}");
                    Console.ResetColor();

                    // List available presets
                    var availablePresets = await dbContext.ExperimentPresets
                        .Select(p => p.Name)
                        .ToListAsync();

                    Console.WriteLine("Available presets:");
                    foreach (var p in availablePresets)
                    {
                        Console.WriteLine($"  - {p}");
                    }

                    Environment.ExitCode = 1;
                    return;
                }

                var config = JsonSerializer.Deserialize<PresetConfigDto>(preset.Config, _jsonOptions);
                if (config == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to parse preset configuration");
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }

                // Use preset values
                experimentType = config.ExperimentType;
                persona = config.Persona ?? persona;
                temperature = config.Temperature;
                needle = config.NeedleFact ?? needle;
                maxEntries = config.MaxEntries ?? maxEntries;
                order = config.EntryOrder;

                if (config.ProviderSweep != null && config.ProviderSweep.Any())
                {
                    providerPairs = config.ProviderSweep;
                }
                else
                {
                    providerPairs = new List<ProviderModelPair>
                    {
                        new() { Provider = config.Provider, Model = config.Model }
                    };
                }

                Console.WriteLine($"Preset loaded: {preset.Description}");
            }
            else if (providers != null && providers.Length > 0)
            {
                // Parse provider:model pairs from command line
                providerPairs = new List<ProviderModelPair>();
                foreach (var p in providers)
                {
                    // Handle comma-separated within a single argument
                    var pairs = p.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split(':');
                        if (parts.Length == 2)
                        {
                            providerPairs.Add(new ProviderModelPair
                            {
                                Provider = parts[0].Trim(),
                                Model = parts[1].Trim()
                            });
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Warning: Invalid provider format '{pair}'. Expected provider:model");
                            Console.ResetColor();
                        }
                    }
                }

                if (providerPairs.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No valid providers specified. Use --providers openai:gpt-4o-mini,anthropic:claude-sonnet-4-20250514");
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }
            }
            else
            {
                // Default provider set
                providerPairs = new List<ProviderModelPair>
                {
                    new() { Provider = "openai", Model = "gpt-4o-mini" }
                };
            }

            Console.WriteLine();
            Console.WriteLine($"Starting batch {experimentType} experiment for athlete {athleteId}");
            Console.WriteLine($"Providers: {string.Join(", ", providerPairs.Select(p => $"{p.Provider}/{p.Model}"))}");
            Console.WriteLine();

            var batchService = serviceProvider.GetRequiredService<IBatchExperimentService>();

            var request = new BatchExperimentRequest
            {
                AthleteId = athleteId,
                ExperimentType = experimentType,
                Providers = providerPairs,
                Persona = persona,
                Temperature = temperature,
                NeedleFact = needle,
                MaxEntries = maxEntries,
                EntryOrder = order
            };

            var response = await batchService.StartBatchAsync(request);
            Console.WriteLine($"Batch started with ID: {response.BatchId}");
            Console.WriteLine($"Run IDs: {string.Join(", ", response.RunIds)}");
            Console.WriteLine();

            // Stream progress from the channel
            var channel = batchService.GetBatchProgressChannel(response.BatchId);
            if (channel != null)
            {
                var completedProviders = 0;
                var totalProviders = providerPairs.Count;

                await foreach (var progressEvent in channel.Reader.ReadAllAsync())
                {
                    var timestamp = progressEvent.Timestamp.ToString("HH:mm:ss");
                    var typeIndicator = progressEvent.Type switch
                    {
                        "batch_started" => "[START]",
                        "provider_started" => "[PROVIDER]",
                        "provider_complete" => "[DONE]",
                        "provider_error" => "[ERROR]",
                        "batch_complete" => "[COMPLETE]",
                        "batch_error" => "[ERROR]",
                        _ => $"[{progressEvent.Type.ToUpper()}]"
                    };

                    if (progressEvent.Type.Contains("error"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (progressEvent.Type.Contains("complete"))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        if (progressEvent.Type == "provider_complete")
                        {
                            completedProviders++;
                        }
                    }
                    else if (progressEvent.Type == "provider_started")
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }

                    var providerInfo = !string.IsNullOrEmpty(progressEvent.Provider)
                        ? $"[{progressEvent.Provider}/{progressEvent.Model}] "
                        : "";

                    Console.WriteLine($"{timestamp} {typeIndicator} {providerInfo}{progressEvent.Message}");

                    if (progressEvent.Type == "provider_complete" || progressEvent.Type == "provider_error")
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"         Progress: {completedProviders}/{totalProviders} providers");
                    }

                    Console.ResetColor();

                    if (verbose && progressEvent.Data != null)
                    {
                        Console.WriteLine($"         Data: {JsonSerializer.Serialize(progressEvent.Data)}");
                    }
                }
            }
            else
            {
                // Wait for batch to complete by polling
                while (batchService.IsBatchRunning(response.BatchId))
                {
                    Console.Write(".");
                    await Task.Delay(1000);
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine($"Batch completed. Batch ID: {response.BatchId}");

            // Generate report if output path specified
            if (!string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine($"Generating report to: {outputPath}");

                using var scope = serviceProvider.CreateScope();
                var reportService = scope.ServiceProvider.GetRequiredService<IReportGeneratorService>();

                var html = await reportService.GenerateHtmlReportAsync(response.BatchId);
                await File.WriteAllTextAsync(outputPath, html);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Report saved: {Path.GetFullPath(outputPath)}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"Generate report: mindsetcoach report --batch {response.BatchId} --output report.html");
            }

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
}
