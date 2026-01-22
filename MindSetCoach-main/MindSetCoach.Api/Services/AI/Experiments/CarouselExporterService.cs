using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models.Experiments;
using SkiaSharp;

namespace MindSetCoach.Api.Services.AI.Experiments;

/// <summary>
/// Type of carousel to generate from experiment results.
/// </summary>
public enum CarouselType
{
    /// <summary>Position test results (U-curve experiment)</summary>
    Position,
    /// <summary>Persona comparison results</summary>
    Persona,
    /// <summary>Cost comparison across providers</summary>
    Cost,
    /// <summary>Summary overview of all experiment types</summary>
    Summary
}

/// <summary>
/// Service for generating LinkedIn carousel images from experiment results.
/// </summary>
public interface ICarouselExporterService
{
    /// <summary>
    /// Generate carousel slides for position test results.
    /// </summary>
    /// <param name="batchId">The batch ID to generate carousel for</param>
    /// <returns>List of PNG images as byte arrays</returns>
    Task<List<byte[]>> GeneratePositionCarouselAsync(string batchId);

    /// <summary>
    /// Generate carousel slides for persona comparison results.
    /// </summary>
    /// <param name="batchId">The batch ID to generate carousel for</param>
    /// <returns>List of PNG images as byte arrays</returns>
    Task<List<byte[]>> GeneratePersonaCarouselAsync(string batchId);

    /// <summary>
    /// Generate carousel slides for cost comparison results.
    /// </summary>
    /// <param name="batchId">The batch ID to generate carousel for</param>
    /// <returns>List of PNG images as byte arrays</returns>
    Task<List<byte[]>> GenerateCostCarouselAsync(string batchId);

    /// <summary>
    /// Save carousel slides to disk.
    /// </summary>
    /// <param name="batchId">The batch ID to generate carousel for</param>
    /// <param name="outputDirectory">Directory to save slides</param>
    /// <param name="type">Type of carousel to generate</param>
    /// <returns>List of saved file paths</returns>
    Task<List<string>> SaveCarouselAsync(string batchId, string outputDirectory, CarouselType type);
}

/// <summary>
/// Implementation of carousel image generation using SkiaSharp.
/// Generates LinkedIn-optimized carousel slides (1080x1350px).
/// </summary>
public class CarouselExporterService : ICarouselExporterService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CarouselExporterService> _logger;

    // Slide dimensions (LinkedIn carousel optimal size)
    private const int SlideWidth = 1080;
    private const int SlideHeight = 1350;

    // Color palette
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor PrimaryColor = SKColor.Parse("#4f46e5");
    private static readonly SKColor SuccessColor = SKColor.Parse("#22c55e");
    private static readonly SKColor ErrorColor = SKColor.Parse("#ef4444");
    private static readonly SKColor TextWhite = SKColor.Parse("#ffffff");
    private static readonly SKColor TextGray = SKColor.Parse("#9ca3af");
    private static readonly SKColor AccentPurple = SKColor.Parse("#7c3aed");
    private static readonly SKColor AccentBlue = SKColor.Parse("#3b82f6");

    // Font settings
    private const string FontFamily = "Liberation Sans";

    public CarouselExporterService(
        IServiceScopeFactory scopeFactory,
        ILogger<CarouselExporterService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<List<byte[]>> GeneratePositionCarouselAsync(string batchId)
    {
        var runs = await GetBatchRunsAsync(batchId);
        if (!runs.Any())
        {
            _logger.LogWarning("No experiment runs found for batch {BatchId}", batchId);
            return new List<byte[]>();
        }

        var slides = new List<byte[]>();

        // Slide 1: Title slide with hook
        slides.Add(GenerateTitleSlide(
            "Lost in the Middle?",
            "How AI Models Handle Context Position",
            $"Batch: {batchId[..8]}...",
            runs.Count));

        // Slide 2: Position test diagram
        slides.Add(GeneratePositionDiagramSlide());

        // Slide 3: Results grid
        slides.Add(GeneratePositionResultsGridSlide(runs));

        // Slide 4: Winner announcement
        slides.Add(GenerateWinnerSlide(runs));

        // Slide 5: CTA with GitHub link
        slides.Add(GenerateCtaSlide());

        return slides;
    }

    public async Task<List<byte[]>> GeneratePersonaCarouselAsync(string batchId)
    {
        var runs = await GetBatchRunsAsync(batchId);
        if (!runs.Any())
        {
            _logger.LogWarning("No experiment runs found for batch {BatchId}", batchId);
            return new List<byte[]>();
        }

        var slides = new List<byte[]>();

        // Slide 1: Title
        slides.Add(GenerateTitleSlide(
            "AI Coaching Showdown",
            "Goggins vs Lasso Persona Test",
            $"Batch: {batchId[..8]}...",
            runs.Count));

        // Slide 2: Persona comparison overview
        slides.Add(GeneratePersonaComparisonSlide(runs));

        // Slide 3: CTA
        slides.Add(GenerateCtaSlide());

        return slides;
    }

    public async Task<List<byte[]>> GenerateCostCarouselAsync(string batchId)
    {
        var runs = await GetBatchRunsAsync(batchId);
        if (!runs.Any())
        {
            _logger.LogWarning("No experiment runs found for batch {BatchId}", batchId);
            return new List<byte[]>();
        }

        var slides = new List<byte[]>();

        // Slide 1: Title
        slides.Add(GenerateTitleSlide(
            "AI Cost Comparison",
            "Budget vs Premium Providers",
            $"Batch: {batchId[..8]}...",
            runs.Count));

        // Slide 2: Cost comparison
        slides.Add(GenerateCostComparisonSlide(runs));

        // Slide 3: CTA
        slides.Add(GenerateCtaSlide());

        return slides;
    }

    public async Task<List<string>> SaveCarouselAsync(string batchId, string outputDirectory, CarouselType type)
    {
        var slides = type switch
        {
            CarouselType.Position => await GeneratePositionCarouselAsync(batchId),
            CarouselType.Persona => await GeneratePersonaCarouselAsync(batchId),
            CarouselType.Cost => await GenerateCostCarouselAsync(batchId),
            CarouselType.Summary => await GeneratePositionCarouselAsync(batchId), // Default to position for summary
            _ => throw new ArgumentException($"Unknown carousel type: {type}")
        };

        if (!slides.Any())
        {
            return new List<string>();
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        var savedPaths = new List<string>();
        var prefix = $"{type.ToString().ToLower()}_{batchId[..8]}";

        for (int i = 0; i < slides.Count; i++)
        {
            var fileName = $"{prefix}_slide_{i + 1:D2}.png";
            var filePath = Path.Combine(outputDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, slides[i]);
            savedPaths.Add(filePath);
            _logger.LogInformation("Saved carousel slide: {FilePath}", filePath);
        }

        return savedPaths;
    }

    #region Slide Generation Methods

    private byte[] GenerateTitleSlide(string headline, string subheadline, string batchInfo, int providerCount)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        // Background
        canvas.Clear(BackgroundColor);

        // Draw gradient accent at top
        using var gradientPaint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(SlideWidth, 300),
            new[] { PrimaryColor, AccentPurple },
            null,
            SKShaderTileMode.Clamp);
        gradientPaint.Shader = shader;
        canvas.DrawRect(0, 0, SlideWidth, 300, gradientPaint);

        // Logo/Brand area
        using var brandPaint = CreateTextPaint(TextWhite, 32, SKFontStyleWeight.Bold);
        canvas.DrawText("MindSetCoach", SlideWidth / 2, 80, brandPaint);

        using var labPaint = CreateTextPaint(TextGray, 20);
        canvas.DrawText("Context Engineering Lab", SlideWidth / 2, 115, labPaint);

        // Main headline
        using var headlinePaint = CreateTextPaint(TextWhite, 72, SKFontStyleWeight.Bold);
        DrawWrappedText(canvas, headline, SlideWidth / 2, 500, SlideWidth - 120, headlinePaint);

        // Subheadline
        using var subPaint = CreateTextPaint(TextGray, 36);
        DrawWrappedText(canvas, subheadline, SlideWidth / 2, 650, SlideWidth - 120, subPaint);

        // Stats box
        DrawStatsBox(canvas, 140, 850, $"{providerCount}", "Providers Tested");

        // Batch info
        using var infoPaint = CreateTextPaint(TextGray, 24);
        canvas.DrawText(batchInfo, SlideWidth / 2, SlideHeight - 100, infoPaint);

        // Swipe indicator
        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    private byte[] GeneratePositionDiagramSlide()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        // Title
        using var titlePaint = CreateTextPaint(TextWhite, 48, SKFontStyleWeight.Bold);
        canvas.DrawText("The U-Curve of Attention", SlideWidth / 2, 120, titlePaint);

        using var subtitlePaint = CreateTextPaint(TextGray, 28);
        canvas.DrawText("Where does AI focus drop?", SlideWidth / 2, 170, subtitlePaint);

        // Draw context window visualization
        var contextTop = 280;
        var contextHeight = 600;
        var boxWidth = 280;
        var boxHeight = 160;
        var gap = 40;
        var startX = (SlideWidth - (3 * boxWidth + 2 * gap)) / 2;

        // Start position box
        DrawPositionBox(canvas, startX, contextTop, boxWidth, boxHeight,
            "START", "High Attention", SuccessColor, true);

        // Middle position box
        DrawPositionBox(canvas, startX + boxWidth + gap, contextTop + 200, boxWidth, boxHeight,
            "MIDDLE", "Attention Drops!", ErrorColor, false);

        // End position box
        DrawPositionBox(canvas, startX + 2 * (boxWidth + gap), contextTop, boxWidth, boxHeight,
            "END", "High Attention", SuccessColor, true);

        // Draw U-curve arrow
        DrawUCurve(canvas, startX + boxWidth / 2, startX + 2 * boxWidth + 2 * gap + boxWidth / 2,
            contextTop + boxHeight + 40, contextTop + 200 + boxHeight / 2);

        // Explanation text
        using var explainPaint = CreateTextPaint(TextGray, 26);
        DrawWrappedText(canvas,
            "LLMs focus heavily on the start and end of context. Information in the middle can get lost - this is the 'Lost in the Middle' phenomenon.",
            SlideWidth / 2, contextTop + contextHeight + 60, SlideWidth - 120, explainPaint);

        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    private byte[] GeneratePositionResultsGridSlide(List<ExperimentRun> runs)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        // Title
        using var titlePaint = CreateTextPaint(TextWhite, 48, SKFontStyleWeight.Bold);
        canvas.DrawText("Position Retrieval Results", SlideWidth / 2, 100, titlePaint);

        // Grid setup
        var gridTop = 200;
        var rowHeight = 120;
        var colWidth = 200;
        var labelWidth = 280;
        var startX = 60;

        // Header row
        using var headerPaint = CreateTextPaint(TextGray, 24, SKFontStyleWeight.Bold);
        headerPaint.TextAlign = SKTextAlign.Left;
        canvas.DrawText("Provider", startX, gridTop, headerPaint);

        headerPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText("Start", startX + labelWidth + colWidth / 2, gridTop, headerPaint);
        canvas.DrawText("Middle", startX + labelWidth + colWidth + colWidth / 2, gridTop, headerPaint);
        canvas.DrawText("End", startX + labelWidth + 2 * colWidth + colWidth / 2, gridTop, headerPaint);

        // Draw header separator
        using var linePaint = new SKPaint { Color = TextGray.WithAlpha(50), StrokeWidth = 2 };
        canvas.DrawLine(startX, gridTop + 30, SlideWidth - startX, gridTop + 30, linePaint);

        // Data rows
        var currentY = gridTop + rowHeight;
        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed && r.PositionTests?.Any() == true).ToList();

        foreach (var run in completedRuns.Take(7)) // Limit to 7 rows to fit
        {
            // Provider name
            using var providerPaint = CreateTextPaint(TextWhite, 22);
            providerPaint.TextAlign = SKTextAlign.Left;
            var providerName = $"{run.Provider}/{TruncateModel(run.Model)}";
            canvas.DrawText(providerName, startX, currentY, providerPaint);

            // Position results
            var positionTests = run.PositionTests?.ToList() ?? new List<PositionTest>();
            var startTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.Start);
            var middleTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.Middle);
            var endTest = positionTests.FirstOrDefault(t => t.Position == NeedlePosition.End);

            DrawCheckOrX(canvas, startX + labelWidth + colWidth / 2, currentY - 10, startTest?.FactRetrieved ?? false);
            DrawCheckOrX(canvas, startX + labelWidth + colWidth + colWidth / 2, currentY - 10, middleTest?.FactRetrieved ?? false);
            DrawCheckOrX(canvas, startX + labelWidth + 2 * colWidth + colWidth / 2, currentY - 10, endTest?.FactRetrieved ?? false);

            currentY += rowHeight;
        }

        // Legend
        var legendY = SlideHeight - 180;
        DrawCheckOrX(canvas, startX + 20, legendY, true);
        using var legendPaint = CreateTextPaint(TextGray, 20);
        legendPaint.TextAlign = SKTextAlign.Left;
        canvas.DrawText("= Fact Retrieved", startX + 60, legendY + 8, legendPaint);

        DrawCheckOrX(canvas, startX + 280, legendY, false);
        canvas.DrawText("= Fact Missed", startX + 320, legendY + 8, legendPaint);

        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    private byte[] GenerateWinnerSlide(List<ExperimentRun> runs)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        // Find winner (provider that retrieved most facts)
        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed && r.PositionTests?.Any() == true).ToList();
        var scores = completedRuns.Select(r => new
        {
            Provider = $"{r.Provider}/{r.Model}",
            Score = r.PositionTests?.Count(pt => pt.FactRetrieved) ?? 0,
            Total = r.PositionTests?.Count ?? 0
        }).OrderByDescending(x => x.Score).ToList();

        var winner = scores.FirstOrDefault();

        // Trophy icon area
        using var trophyPaint = CreateTextPaint(SKColor.Parse("#fbbf24"), 120);
        canvas.DrawText("\u2B50", SlideWidth / 2, 250, trophyPaint); // Star emoji

        // Winner announcement
        using var announcePaint = CreateTextPaint(TextGray, 32);
        canvas.DrawText("Best Context Handler", SlideWidth / 2, 350, announcePaint);

        if (winner != null)
        {
            using var winnerPaint = CreateTextPaint(SuccessColor, 56, SKFontStyleWeight.Bold);
            DrawWrappedText(canvas, winner.Provider, SlideWidth / 2, 450, SlideWidth - 120, winnerPaint);

            using var scorePaint = CreateTextPaint(TextWhite, 40);
            canvas.DrawText($"{winner.Score}/{winner.Total} positions retrieved", SlideWidth / 2, 540, scorePaint);
        }

        // Runner-ups
        if (scores.Count > 1)
        {
            using var runnerPaint = CreateTextPaint(TextGray, 24);
            canvas.DrawText("Runner-ups:", SlideWidth / 2, 680, runnerPaint);

            var runnerY = 730;
            foreach (var runner in scores.Skip(1).Take(3))
            {
                using var runnerNamePaint = CreateTextPaint(TextWhite, 22);
                canvas.DrawText($"{runner.Provider}: {runner.Score}/{runner.Total}", SlideWidth / 2, runnerY, runnerNamePaint);
                runnerY += 40;
            }
        }

        // Insight
        using var insightPaint = CreateTextPaint(AccentBlue, 26);
        DrawWrappedText(canvas,
            "Different models handle long context differently. Test your specific use case!",
            SlideWidth / 2, SlideHeight - 200, SlideWidth - 120, insightPaint);

        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    private byte[] GenerateCtaSlide()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        // Gradient background
        using var gradientPaint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(SlideWidth, SlideHeight),
            new[] { BackgroundColor, SKColor.Parse("#16213e") },
            null,
            SKShaderTileMode.Clamp);
        gradientPaint.Shader = shader;
        canvas.DrawRect(0, 0, SlideWidth, SlideHeight, gradientPaint);

        // Main CTA text
        using var ctaPaint = CreateTextPaint(TextWhite, 56, SKFontStyleWeight.Bold);
        DrawWrappedText(canvas, "Run Your Own Experiments", SlideWidth / 2, 350, SlideWidth - 120, ctaPaint);

        using var subCtaPaint = CreateTextPaint(TextGray, 32);
        DrawWrappedText(canvas, "MindSetCoach Context Engineering Lab is open source", SlideWidth / 2, 480, SlideWidth - 120, subCtaPaint);

        // GitHub button
        var buttonX = SlideWidth / 2 - 200;
        var buttonY = 600;
        var buttonWidth = 400;
        var buttonHeight = 70;

        using var buttonPaint = new SKPaint { Color = PrimaryColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(buttonX, buttonY, buttonX + buttonWidth, buttonY + buttonHeight), 12), buttonPaint);

        using var buttonTextPaint = CreateTextPaint(TextWhite, 28, SKFontStyleWeight.Bold);
        canvas.DrawText("View on GitHub", SlideWidth / 2, buttonY + 46, buttonTextPaint);

        // GitHub URL
        using var urlPaint = CreateTextPaint(TextGray, 22);
        canvas.DrawText("github.com/jonfairbanks/MindSetCoach", SlideWidth / 2, buttonY + buttonHeight + 50, urlPaint);

        // Social handles placeholder
        using var socialPaint = CreateTextPaint(TextGray, 24);
        canvas.DrawText("Like & Follow for more AI experiments", SlideWidth / 2, SlideHeight - 200, socialPaint);

        // Brand footer
        using var footerPaint = CreateTextPaint(PrimaryColor, 28, SKFontStyleWeight.Bold);
        canvas.DrawText("MindSetCoach", SlideWidth / 2, SlideHeight - 100, footerPaint);

        return EncodeToBytes(surface);
    }

    private byte[] GeneratePersonaComparisonSlide(List<ExperimentRun> runs)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        // Title
        using var titlePaint = CreateTextPaint(TextWhite, 48, SKFontStyleWeight.Bold);
        canvas.DrawText("Persona Comparison", SlideWidth / 2, 100, titlePaint);

        // Group by persona
        var gogginsClaims = runs
            .SelectMany(r => r.Claims ?? Enumerable.Empty<ExperimentClaim>())
            .Where(c => c.Persona.ToLower() == "goggins")
            .ToList();

        var lassoClaims = runs
            .SelectMany(r => r.Claims ?? Enumerable.Empty<ExperimentClaim>())
            .Where(c => c.Persona.ToLower() == "lasso")
            .ToList();

        // Goggins box
        var boxWidth = 450;
        var boxHeight = 500;
        var gap = 40;
        var startX = (SlideWidth - (2 * boxWidth + gap)) / 2;
        var boxY = 200;

        DrawPersonaBox(canvas, startX, boxY, boxWidth, boxHeight,
            "GOGGINS", "Stay Hard!", SKColor.Parse("#ef4444"), gogginsClaims.Count);

        // Lasso box
        DrawPersonaBox(canvas, startX + boxWidth + gap, boxY, boxWidth, boxHeight,
            "TED LASSO", "Be a Goldfish!", SKColor.Parse("#fbbf24"), lassoClaims.Count);

        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    private byte[] GenerateCostComparisonSlide(List<ExperimentRun> runs)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SlideWidth, SlideHeight));
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        // Title
        using var titlePaint = CreateTextPaint(TextWhite, 48, SKFontStyleWeight.Bold);
        canvas.DrawText("Cost Comparison", SlideWidth / 2, 100, titlePaint);

        var completedRuns = runs.Where(r => r.Status == ExperimentStatus.Completed).ToList();
        var maxCost = completedRuns.Max(r => r.EstimatedCost);
        if (maxCost == 0) maxCost = 0.01m;

        var barHeight = 60;
        var barGap = 30;
        var startY = 220;
        var barMaxWidth = SlideWidth - 300;
        var barStartX = 240;

        foreach (var run in completedRuns.OrderBy(r => r.EstimatedCost).Take(8))
        {
            var barWidth = (float)(run.EstimatedCost / maxCost) * barMaxWidth;
            barWidth = Math.Max(barWidth, 50); // Minimum width

            // Provider label
            using var labelPaint = CreateTextPaint(TextWhite, 20);
            labelPaint.TextAlign = SKTextAlign.Right;
            var label = TruncateModel($"{run.Provider}");
            canvas.DrawText(label, barStartX - 20, startY + barHeight / 2 + 8, labelPaint);

            // Cost bar
            var barColor = GetProviderColor(run.Provider);
            using var barPaint = new SKPaint { Color = barColor, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(barStartX, startY, barStartX + barWidth, startY + barHeight), 8), barPaint);

            // Cost value
            using var valuePaint = CreateTextPaint(TextWhite, 18, SKFontStyleWeight.Bold);
            valuePaint.TextAlign = SKTextAlign.Left;
            canvas.DrawText($"${run.EstimatedCost:F6}", barStartX + barWidth + 15, startY + barHeight / 2 + 6, valuePaint);

            startY += barHeight + barGap;
        }

        // Cheapest highlight
        var cheapest = completedRuns.OrderBy(r => r.EstimatedCost).FirstOrDefault();
        if (cheapest != null)
        {
            using var cheapestPaint = CreateTextPaint(SuccessColor, 28, SKFontStyleWeight.Bold);
            canvas.DrawText($"Cheapest: {cheapest.Provider}/{cheapest.Model}", SlideWidth / 2, SlideHeight - 180, cheapestPaint);
        }

        DrawSwipeIndicator(canvas);

        return EncodeToBytes(surface);
    }

    #endregion

    #region Helper Methods

    private async Task<List<ExperimentRun>> GetBatchRunsAsync(string batchId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

        return await dbContext.ExperimentRuns
            .Include(r => r.Claims)
                .ThenInclude(c => c.Receipts)
            .Include(r => r.PositionTests)
            .Where(r => r.BatchId == batchId && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    private static SKPaint CreateTextPaint(SKColor color, float textSize, SKFontStyleWeight weight = SKFontStyleWeight.Normal)
    {
        var typeface = SKTypeface.FromFamilyName(FontFamily, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        return new SKPaint
        {
            Color = color,
            TextSize = textSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = typeface
        };
    }

    private static void DrawWrappedText(SKCanvas canvas, string text, float x, float y, float maxWidth, SKPaint paint)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var bounds = new SKRect();
            paint.MeasureText(testLine, ref bounds);

            if (bounds.Width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        var lineHeight = paint.TextSize * 1.3f;
        var startY = y - ((lines.Count - 1) * lineHeight / 2);

        foreach (var line in lines)
        {
            canvas.DrawText(line, x, startY, paint);
            startY += lineHeight;
        }
    }

    private static void DrawStatsBox(SKCanvas canvas, float x, float y, string value, string label)
    {
        var boxWidth = 200;
        var boxHeight = 120;

        using var boxPaint = new SKPaint { Color = PrimaryColor.WithAlpha(100), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + boxWidth, y + boxHeight), 16), boxPaint);

        using var valuePaint = CreateTextPaint(TextWhite, 48, SKFontStyleWeight.Bold);
        valuePaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(value, x + boxWidth / 2, y + 55, valuePaint);

        using var labelPaint = CreateTextPaint(TextGray, 18);
        labelPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(label, x + boxWidth / 2, y + 95, labelPaint);
    }

    private static void DrawPositionBox(SKCanvas canvas, float x, float y, float width, float height,
        string position, string description, SKColor borderColor, bool isHighAttention)
    {
        // Box background
        var bgColor = isHighAttention ? borderColor.WithAlpha(30) : borderColor.WithAlpha(20);
        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 16), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = borderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 16), borderPaint);

        // Position label
        using var posPaint = CreateTextPaint(borderColor, 28, SKFontStyleWeight.Bold);
        canvas.DrawText(position, x + width / 2, y + 60, posPaint);

        // Description
        using var descPaint = CreateTextPaint(TextWhite, 20);
        canvas.DrawText(description, x + width / 2, y + 100, descPaint);

        // Icon
        var icon = isHighAttention ? "\u2714" : "\u2717"; // Check or X
        using var iconPaint = CreateTextPaint(borderColor, 36);
        canvas.DrawText(icon, x + width / 2, y + 140, iconPaint);
    }

    private static void DrawUCurve(SKCanvas canvas, float startX, float endX, float topY, float bottomY)
    {
        using var path = new SKPath();
        path.MoveTo(startX, topY);
        path.QuadTo((startX + endX) / 2, bottomY + 80, endX, topY);

        using var paint = new SKPaint
        {
            Color = AccentPurple,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0)
        };
        canvas.DrawPath(path, paint);

        // Label
        using var labelPaint = CreateTextPaint(AccentPurple, 20, SKFontStyleWeight.Bold);
        canvas.DrawText("U-Curve", (startX + endX) / 2, bottomY + 120, labelPaint);
    }

    private static void DrawCheckOrX(SKCanvas canvas, float x, float y, bool isCheck)
    {
        var color = isCheck ? SuccessColor : ErrorColor;
        var symbol = isCheck ? "\u2714" : "\u2717";

        using var circlePaint = new SKPaint { Color = color.WithAlpha(50), IsAntialias = true };
        canvas.DrawCircle(x, y, 25, circlePaint);

        using var symbolPaint = CreateTextPaint(color, 32, SKFontStyleWeight.Bold);
        canvas.DrawText(symbol, x, y + 12, symbolPaint);
    }

    private static void DrawPersonaBox(SKCanvas canvas, float x, float y, float width, float height,
        string name, string tagline, SKColor accentColor, int claimCount)
    {
        // Background
        using var bgPaint = new SKPaint { Color = accentColor.WithAlpha(20), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 20), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = accentColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 20), borderPaint);

        // Name
        using var namePaint = CreateTextPaint(accentColor, 36, SKFontStyleWeight.Bold);
        canvas.DrawText(name, x + width / 2, y + 80, namePaint);

        // Tagline
        using var tagPaint = CreateTextPaint(TextGray, 22);
        canvas.DrawText(tagline, x + width / 2, y + 130, tagPaint);

        // Stats
        using var statPaint = CreateTextPaint(TextWhite, 64, SKFontStyleWeight.Bold);
        canvas.DrawText(claimCount.ToString(), x + width / 2, y + 280, statPaint);

        using var statLabelPaint = CreateTextPaint(TextGray, 20);
        canvas.DrawText("Claims Generated", x + width / 2, y + 320, statLabelPaint);
    }

    private static void DrawSwipeIndicator(SKCanvas canvas)
    {
        using var paint = CreateTextPaint(TextGray.WithAlpha(150), 18);
        canvas.DrawText("Swipe \u2192", SlideWidth - 80, SlideHeight - 40, paint);
    }

    private static SKColor GetProviderColor(string provider)
    {
        return provider.ToLower() switch
        {
            "openai" => SKColor.Parse("#10b981"),
            "anthropic" => SKColor.Parse("#f59e0b"),
            "google" => SKColor.Parse("#4285f4"),
            "deepseek" => SKColor.Parse("#6366f1"),
            "ollama" => SKColor.Parse("#8b5cf6"),
            _ => SKColor.Parse("#6b7280")
        };
    }

    private static string TruncateModel(string model)
    {
        if (model.Length <= 20) return model;
        return model[..17] + "...";
    }

    private static byte[] EncodeToBytes(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    #endregion
}

/// <summary>
/// Extension methods for carousel ZIP generation.
/// </summary>
public static class CarouselZipExtensions
{
    /// <summary>
    /// Create a ZIP archive containing all carousel slides.
    /// </summary>
    public static byte[] CreateCarouselZip(this List<byte[]> slides, string prefix)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            for (int i = 0; i < slides.Count; i++)
            {
                var entry = archive.CreateEntry($"{prefix}_slide_{i + 1:D2}.png", CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(slides[i], 0, slides[i].Length);
            }
        }

        return memoryStream.ToArray();
    }
}
