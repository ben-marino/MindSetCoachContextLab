using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MindSetCoach.Api.Models;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli.Commands;

/// <summary>
/// Command for generating LinkedIn post content from experiment insights.
/// Usage: mindsetcoach insights --batch [ID] --tone professional --output post.md
/// </summary>
public static class InsightsCommand
{
    public static Command Create()
    {
        var batchOption = new Option<string>(
            aliases: new[] { "--batch", "-b" },
            description: "Batch ID to generate insights for")
        {
            IsRequired = true
        };

        var toneOption = new Option<string>(
            aliases: new[] { "--tone", "-t" },
            getDefaultValue: () => "professional",
            description: "Post tone: professional, conversational, or technical");

        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path (e.g., post.md). If not specified, outputs to console");

        var carouselOption = new Option<int?>(
            aliases: new[] { "--carousel", "-c" },
            description: "Generate carousel captions with specified slide count instead of post");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Show detailed output");

        var command = new Command("insights", "Generate LinkedIn post content from experiment insights")
        {
            batchOption,
            toneOption,
            outputOption,
            carouselOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var batchId = context.ParseResult.GetValueForOption(batchOption)!;
            var tone = context.ParseResult.GetValueForOption(toneOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption);
            var carousel = context.ParseResult.GetValueForOption(carouselOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            await ExecuteAsync(batchId, tone, output, carousel, verbose);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string batchId,
        string tone,
        string? output,
        int? carouselSlideCount,
        bool verbose)
    {
        try
        {
            using var serviceProvider = ServiceFactory.CreateServiceProvider(verbose);
            await ServiceFactory.EnsureDatabasesAsync(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var insightService = scope.ServiceProvider.GetRequiredService<IInsightSummarizerService>();

            // Parse tone
            if (!Enum.TryParse<PostTone>(tone, ignoreCase: true, out var postTone))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Invalid tone '{tone}'. Valid tones: professional, conversational, technical");
                Console.ResetColor();
                Environment.ExitCode = 1;
                return;
            }

            if (carouselSlideCount.HasValue)
            {
                // Generate carousel captions
                await GenerateCarouselAsync(insightService, batchId, carouselSlideCount.Value, output, verbose);
            }
            else
            {
                // Generate LinkedIn post
                await GeneratePostAsync(insightService, batchId, postTone, output, verbose);
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

    private static async Task GeneratePostAsync(
        IInsightSummarizerService service,
        string batchId,
        PostTone tone,
        string? output,
        bool verbose)
    {
        Console.WriteLine($"Generating {tone} LinkedIn post for batch {batchId}...");

        var content = await service.GeneratePostAsync(batchId, tone);

        if (verbose)
        {
            Console.WriteLine($"\nGeneration complete:");
            Console.WriteLine($"  - Hook: {content.Hook.Length} chars");
            Console.WriteLine($"  - Body: {content.Body.Length} chars");
            Console.WriteLine($"  - CTA: {content.CallToAction.Length} chars");
            Console.WriteLine($"  - Hashtags: {content.Hashtags.Count}");
            Console.WriteLine($"  - Total: {content.CharacterCount} chars");
            Console.WriteLine($"  - Within limit: {content.IsWithinLimit}");
        }

        var outputContent = FormatOutput(content);

        if (!string.IsNullOrEmpty(output))
        {
            // Write to file
            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(output, outputContent);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nPost saved to: {Path.GetFullPath(output)}");
            Console.ResetColor();
        }
        else
        {
            // Output to console
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("LINKEDIN POST");
            Console.WriteLine(new string('=', 60) + "\n");
            Console.WriteLine(content.FullPost);
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine($"Character count: {content.CharacterCount}/3000");

            if (!content.IsWithinLimit)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: Post exceeds LinkedIn's 3000 character limit!");
                Console.ResetColor();
            }
        }
    }

    private static async Task GenerateCarouselAsync(
        IInsightSummarizerService service,
        string batchId,
        int slideCount,
        string? output,
        bool verbose)
    {
        Console.WriteLine($"Generating {slideCount} carousel captions for batch {batchId}...");

        var captions = await service.GenerateCarouselCaptionsAsync(batchId, slideCount);

        if (verbose)
        {
            Console.WriteLine($"\nGenerated {captions.Count} captions");
        }

        var outputContent = FormatCarouselOutput(captions);

        if (!string.IsNullOrEmpty(output))
        {
            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(output, outputContent);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nCarousel captions saved to: {Path.GetFullPath(output)}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("CAROUSEL CAPTIONS");
            Console.WriteLine(new string('=', 60) + "\n");

            for (int i = 0; i < captions.Count; i++)
            {
                Console.WriteLine($"Slide {i + 1}: {captions[i]}");
                Console.WriteLine();
            }
        }
    }

    private static string FormatOutput(LinkedInPostContent content)
    {
        return $@"# LinkedIn Post
**Batch:** {content.BatchId}
**Tone:** {content.Tone}
**Characters:** {content.CharacterCount}/3000

---

## Hook
{content.Hook}

## Body
{content.Body}

## Call to Action
{content.CallToAction}

## Hashtags
{string.Join(" ", content.Hashtags)}

---

## Ready to Copy

```
{content.FullPost}
```
";
    }

    private static string FormatCarouselOutput(List<string> captions)
    {
        var lines = new List<string>
        {
            "# Carousel Captions",
            ""
        };

        for (int i = 0; i < captions.Count; i++)
        {
            lines.Add($"## Slide {i + 1}");
            lines.Add(captions[i]);
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}
