using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for generating LinkedIn carousel images from experiment results.
/// Usage: mindsetcoach carousel --batch [ID] --type position --output ./slides/
/// </summary>
public static class CarouselCommand
{
    public static Command Create()
    {
        var batchOption = new Option<string>(
            aliases: new[] { "--batch", "-b" },
            description: "Batch ID to generate carousel for")
        {
            IsRequired = true
        };

        var typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            getDefaultValue: () => "position",
            description: "Carousel type: position, persona, cost, or summary");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "./slides/",
            description: "Output directory for carousel slides");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Show detailed output");

        var command = new Command("carousel", "Generate LinkedIn carousel images from experiment results")
        {
            batchOption,
            typeOption,
            outputOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var batchId = context.ParseResult.GetValueForOption(batchOption)!;
            var type = context.ParseResult.GetValueForOption(typeOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            await ExecuteAsync(batchId, type, output, verbose);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string batchId,
        string type,
        string output,
        bool verbose)
    {
        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var carouselService = scope.ServiceProvider.GetRequiredService<ICarouselExporterService>();

            // Parse carousel type
            if (!Enum.TryParse<CarouselType>(type, ignoreCase: true, out var carouselType))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Invalid carousel type '{type}'. Valid types: position, persona, cost, summary");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"Generating {carouselType} carousel for batch {batchId}...");
            Console.WriteLine($"Output directory: {Path.GetFullPath(output)}");

            var savedPaths = await carouselService.SaveCarouselAsync(batchId, output, carouselType);

            if (!savedPaths.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: No slides generated. Check if the batch has completed experiment runs.");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nCarousel generated successfully!");
            Console.WriteLine($"Slides saved: {savedPaths.Count}");
            Console.ResetColor();

            if (verbose)
            {
                Console.WriteLine("\nGenerated files:");
                foreach (var path in savedPaths)
                {
                    var fileInfo = new FileInfo(path);
                    Console.WriteLine($"  - {Path.GetFileName(path)} ({fileInfo.Length:N0} bytes)");
                }
            }

            Console.WriteLine($"\nView slides at: {Path.GetFullPath(output)}");
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
