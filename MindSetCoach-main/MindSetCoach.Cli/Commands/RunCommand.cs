using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for running a single experiment.
/// Usage: mindsetcoach run --athlete 1 --type position --provider openai --model gpt-4o-mini
/// </summary>
public static class RunCommand
{
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

        var providerOption = new Option<string>(
            aliases: new[] { "--provider", "-p" },
            getDefaultValue: () => "openai",
            description: "AI provider: openai, anthropic, deepseek, google, or ollama");

        var modelOption = new Option<string>(
            aliases: new[] { "--model", "-m" },
            getDefaultValue: () => "gpt-4o-mini",
            description: "Model name (e.g., gpt-4o-mini, claude-sonnet-4-20250514, deepseek-chat)");

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

        var command = new Command("run", "Run a single experiment")
        {
            athleteOption,
            typeOption,
            providerOption,
            modelOption,
            personaOption,
            temperatureOption,
            needleOption,
            maxEntriesOption,
            orderOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var athleteId = context.ParseResult.GetValueForOption(athleteOption);
            var experimentType = context.ParseResult.GetValueForOption(typeOption)!;
            var provider = context.ParseResult.GetValueForOption(providerOption)!;
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var persona = context.ParseResult.GetValueForOption(personaOption)!;
            var temperature = context.ParseResult.GetValueForOption(temperatureOption);
            var needle = context.ParseResult.GetValueForOption(needleOption);
            var maxEntries = context.ParseResult.GetValueForOption(maxEntriesOption);
            var order = context.ParseResult.GetValueForOption(orderOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            await ExecuteAsync(athleteId, experimentType, provider, model, persona, temperature, needle, maxEntries, order, verbose);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        int athleteId,
        string experimentType,
        string provider,
        string model,
        string persona,
        double temperature,
        string? needle,
        int? maxEntries,
        string order,
        bool verbose)
    {
        Console.WriteLine($"Starting {experimentType} experiment for athlete {athleteId}...");
        Console.WriteLine($"Provider: {provider}/{model}");
        Console.WriteLine();

        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            var experimentRunner = serviceProvider.GetRequiredService<IExperimentRunnerService>();

            var request = new RunExperimentRequest
            {
                AthleteId = athleteId,
                ExperimentType = experimentType,
                Provider = provider,
                Model = model,
                Persona = persona,
                Temperature = temperature,
                NeedleFact = needle,
                MaxEntries = maxEntries,
                EntryOrder = order
            };

            var runId = await experimentRunner.StartExperimentAsync(request);
            Console.WriteLine($"Experiment started with run ID: {runId}");

            // Stream progress from the channel
            var channel = experimentRunner.GetProgressChannel(runId);
            if (channel != null)
            {
                await foreach (var progressEvent in channel.Reader.ReadAllAsync())
                {
                    var timestamp = progressEvent.Timestamp.ToString("HH:mm:ss");
                    var typeIndicator = progressEvent.Type switch
                    {
                        "progress" => "[INFO]",
                        "position" => "[POSITION]",
                        "claim" => "[CLAIM]",
                        "compression" => "[COMPRESSION]",
                        "complete" => "[DONE]",
                        "error" => "[ERROR]",
                        _ => $"[{progressEvent.Type.ToUpper()}]"
                    };

                    if (progressEvent.Type == "error")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (progressEvent.Type == "complete")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    else if (progressEvent.Type == "position")
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }

                    Console.WriteLine($"{timestamp} {typeIndicator} {progressEvent.Message}");
                    Console.ResetColor();

                    if (verbose && progressEvent.Data != null)
                    {
                        Console.WriteLine($"         Data: {System.Text.Json.JsonSerializer.Serialize(progressEvent.Data)}");
                    }
                }
            }
            else
            {
                // Wait for experiment to complete by polling
                while (experimentRunner.IsRunning(runId))
                {
                    Console.Write(".");
                    await Task.Delay(500);
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine($"Experiment completed. Run ID: {runId}");
            Console.WriteLine($"View results: mindsetcoach report --batch {runId} --output report.html");

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
