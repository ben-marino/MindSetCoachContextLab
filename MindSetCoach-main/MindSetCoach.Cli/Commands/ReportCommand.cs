using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for generating HTML or JSON reports from experiment results.
/// Usage: mindsetcoach report --batch <batchId> --output report.html
/// </summary>
public static class ReportCommand
{
    public static Command Create()
    {
        var batchOption = new Option<string?>(
            aliases: new[] { "--batch", "-b" },
            description: "Batch ID to generate report for");

        var runIdsOption = new Option<int[]?>(
            aliases: new[] { "--runs", "-r" },
            description: "Comma-separated list of run IDs to include in the report");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "report.html",
            description: "Output file path (.html or .json)");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "html",
            description: "Output format: html or json");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Show detailed output");

        var command = new Command("report", "Generate HTML or JSON report from experiment results")
        {
            batchOption,
            runIdsOption,
            outputOption,
            formatOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var batchId = context.ParseResult.GetValueForOption(batchOption);
            var runIds = context.ParseResult.GetValueForOption(runIdsOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            await ExecuteAsync(batchId, runIds, output, format, verbose);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string? batchId,
        int[]? runIds,
        string output,
        string format,
        bool verbose)
    {
        if (string.IsNullOrEmpty(batchId) && (runIds == null || runIds.Length == 0))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Must specify either --batch or --runs");
            Console.ResetColor();
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportGeneratorService>();

            // Determine format from file extension if not explicitly specified
            var isJson = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                || output.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"Generating {(isJson ? "JSON" : "HTML")} report...");

            string content;
            if (!string.IsNullOrEmpty(batchId))
            {
                Console.WriteLine($"Batch ID: {batchId}");

                if (isJson)
                {
                    var data = await reportService.GenerateJsonReportAsync(batchId);
                    if (data == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"No experiment runs found for batch: {batchId}");
                        Console.ResetColor();
                        Environment.ExitCode = 1;
                        return;
                    }
                    content = JsonSerializer.Serialize(data, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });
                }
                else
                {
                    content = await reportService.GenerateHtmlReportAsync(batchId);
                }
            }
            else
            {
                Console.WriteLine($"Run IDs: {string.Join(", ", runIds!)}");

                if (isJson)
                {
                    var data = await reportService.GenerateJsonReportAsync(runIds!.ToList());
                    if (data == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No experiment runs found for the specified IDs");
                        Console.ResetColor();
                        Environment.ExitCode = 1;
                        return;
                    }
                    content = JsonSerializer.Serialize(data, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });
                }
                else
                {
                    content = await reportService.GenerateHtmlReportAsync(runIds!.ToList());
                }
            }

            // Ensure output path has correct extension
            if (isJson && !output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                output = Path.ChangeExtension(output, ".json");
            }
            else if (!isJson && !output.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                output = Path.ChangeExtension(output, ".html");
            }

            // Create directory if needed
            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(output, content);

            var fullPath = Path.GetFullPath(output);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Report saved: {fullPath}");
            Console.ResetColor();

            if (verbose)
            {
                var fileInfo = new FileInfo(fullPath);
                Console.WriteLine($"Size: {fileInfo.Length:N0} bytes");
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
