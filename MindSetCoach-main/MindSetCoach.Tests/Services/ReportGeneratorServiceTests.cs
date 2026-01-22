using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models.Experiments;
using MindSetCoach.Api.Services.AI.Experiments;
using Moq;

namespace MindSetCoach.Tests.Services;

public class ReportGeneratorServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReportGeneratorService _service;
    private readonly string _databaseName;

    public ReportGeneratorServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<ExperimentsDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName));

        var loggerMock = new Mock<ILogger<ReportGeneratorService>>();
        serviceCollection.AddSingleton(loggerMock.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _service = new ReportGeneratorService(
            _scopeFactory,
            loggerMock.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    #region GenerateJsonReportAsync - Batch ID Tests

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_ForUnknownBatch_ReturnsNull()
    {
        // Arrange
        var unknownBatchId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.GenerateJsonReportAsync(unknownBatchId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_ReturnsReportData()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 2);

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.BatchId.Should().Be(batchId);
        result.ProviderResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_IncludesCorrectExperimentType()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 1, ExperimentType.Position);

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.ExperimentType.Should().Be("position");
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_IncludesAthleteId()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 1, athleteId: 42);

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.AthleteId.Should().Be(42);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_CalculatesTotalCost()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                CreateExperimentRun(batchId, "openai", "gpt-4o", estimatedCost: 0.01m),
                CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", estimatedCost: 0.02m)
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalCost.Should().Be(0.03m);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_CalculatesTotalTokens()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                CreateExperimentRun(batchId, "openai", "gpt-4o", tokensUsed: 1000),
                CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", tokensUsed: 1500)
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalTokens.Should().Be(2500);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_ExcludesDeletedRuns()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                CreateExperimentRun(batchId, "openai", "gpt-4o", isDeleted: false),
                CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", isDeleted: true)
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalProviders.Should().Be(1);
        result.ProviderResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithBatchId_IncludesConfiguration()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run = CreateExperimentRun(batchId, "openai", "gpt-4o");
            run.Persona = "goggins";
            run.Temperature = 0.5;
            run.EntryOrder = "chronological";
            run.PromptVersion = "v2";
            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.Configuration.Should().NotBeNull();
        result.Configuration.Persona.Should().Be("goggins");
        result.Configuration.Temperature.Should().Be(0.5);
        result.Configuration.EntryOrder.Should().Be("chronological");
        result.Configuration.PromptVersion.Should().Be("v2");
    }

    #endregion

    #region GenerateJsonReportAsync - Run IDs Tests

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_ForEmptyList_ReturnsNull()
    {
        // Act
        var result = await _service.GenerateJsonReportAsync(new List<int>());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_ForNullList_ReturnsNull()
    {
        // Act
        var result = await _service.GenerateJsonReportAsync((List<int>)null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_ForNonExistentIds_ReturnsNull()
    {
        // Act
        var result = await _service.GenerateJsonReportAsync(new List<int> { 999, 1000, 1001 });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_ReturnsReportData()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        List<int> runIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var runs = new[]
            {
                CreateExperimentRun(batchId, "openai", "gpt-4o"),
                CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet")
            };
            dbContext.ExperimentRuns.AddRange(runs);
            await dbContext.SaveChangesAsync();
            runIds = runs.Select(r => r.Id).ToList();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(runIds);

        // Assert
        result.Should().NotBeNull();
        result!.ProviderResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_IncludesPositionResults()
    {
        // Arrange
        int runId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run = CreateExperimentRun(null, "openai", "gpt-4o", ExperimentType.Position);
            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync();
            runId = run.Id;

            dbContext.PositionTests.AddRange(
                new PositionTest
                {
                    RunId = runId,
                    Position = NeedlePosition.Start,
                    NeedleFact = "test fact",
                    FactRetrieved = true,
                    ResponseSnippet = "found it"
                },
                new PositionTest
                {
                    RunId = runId,
                    Position = NeedlePosition.Middle,
                    NeedleFact = "test fact",
                    FactRetrieved = false,
                    ResponseSnippet = ""
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(new List<int> { runId });

        // Assert
        result.Should().NotBeNull();
        var providerResult = result!.ProviderResults.First();
        providerResult.PositionResults.Should().NotBeNull();
        providerResult.PositionResults!.Start.Found.Should().BeTrue();
        providerResult.PositionResults.Middle.Found.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithRunIds_IncludesClaimResults()
    {
        // Arrange
        int runId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run = CreateExperimentRun(null, "openai", "gpt-4o", ExperimentType.Persona);
            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync();
            runId = run.Id;

            dbContext.ExperimentClaims.AddRange(
                new ExperimentClaim
                {
                    RunId = runId,
                    ClaimText = "Claim 1",
                    IsSupported = true,
                    Persona = "goggins"
                },
                new ExperimentClaim
                {
                    RunId = runId,
                    ClaimText = "Claim 2",
                    IsSupported = false,
                    Persona = "lasso"
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(new List<int> { runId });

        // Assert
        result.Should().NotBeNull();
        var providerResult = result!.ProviderResults.First();
        providerResult.PersonaClaims.Should().NotBeNull();
        providerResult.PersonaClaims!.Should().ContainKey("goggins");
        providerResult.PersonaClaims.Should().ContainKey("lasso");
    }

    #endregion

    #region GenerateHtmlReportAsync Tests

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ForUnknownBatch_ReturnsErrorHtml()
    {
        // Arrange
        var unknownBatchId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.GenerateHtmlReportAsync(unknownBatchId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Report Generation Error");
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ReturnsValidHtml()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 2);

        // Act
        var result = await _service.GenerateHtmlReportAsync(batchId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("</html>");
        result.Should().Contain("MindSetCoach");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ContainsBatchId()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 1);

        // Act
        var result = await _service.GenerateHtmlReportAsync(batchId);

        // Assert
        result.Should().Contain(batchId);
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ContainsProviderInfo()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.Add(CreateExperimentRun(batchId, "openai", "gpt-4o"));
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateHtmlReportAsync(batchId);

        // Assert
        result.Should().Contain("openai");
        result.Should().Contain("gpt-4o");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ContainsBootstrapCss()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 1);

        // Act
        var result = await _service.GenerateHtmlReportAsync(batchId);

        // Assert
        result.Should().Contain("bootstrap");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithBatchId_ContainsEmbeddedStyles()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        await SeedExperimentRuns(batchId, 1);

        // Act
        var result = await _service.GenerateHtmlReportAsync(batchId);

        // Assert
        result.Should().Contain("<style>");
        result.Should().Contain(".experiment-card");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithRunIds_ForNoRuns_ReturnsErrorHtml()
    {
        // Act
        var result = await _service.GenerateHtmlReportAsync(new List<int> { 999 });

        // Assert
        result.Should().Contain("Report Generation Error");
        result.Should().Contain("No experiment runs found");
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_WithRunIds_ReturnsValidHtml()
    {
        // Arrange
        int runId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run = CreateExperimentRun(null, "openai", "gpt-4o");
            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync();
            runId = run.Id;
        }

        // Act
        var result = await _service.GenerateHtmlReportAsync(new List<int> { runId });

        // Assert
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("</html>");
    }

    #endregion

    #region Comparison Data Tests

    [Fact]
    public async Task GenerateJsonReportAsync_WithCompletedRuns_IncludesComparison()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                CreateExperimentRun(batchId, "openai", "gpt-4o", status: ExperimentStatus.Completed, estimatedCost: 0.01m, tokensUsed: 1000),
                CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", status: ExperimentStatus.Completed, estimatedCost: 0.02m, tokensUsed: 1500)
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.Comparison.Should().NotBeNull();
        result.Comparison!.CostByProvider.Should().HaveCount(2);
        result.Comparison.CheapestProvider.Should().Contain("openai");
    }

    [Fact]
    public async Task GenerateJsonReportAsync_WithPositionTests_IncludesPositionComparison()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run1 = CreateExperimentRun(batchId, "openai", "gpt-4o", ExperimentType.Position, ExperimentStatus.Completed);
            var run2 = CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", ExperimentType.Position, ExperimentStatus.Completed);
            dbContext.ExperimentRuns.AddRange(run1, run2);
            await dbContext.SaveChangesAsync();

            dbContext.PositionTests.AddRange(
                new PositionTest { RunId = run1.Id, Position = NeedlePosition.Start, NeedleFact = "test", FactRetrieved = true },
                new PositionTest { RunId = run1.Id, Position = NeedlePosition.Middle, NeedleFact = "test", FactRetrieved = false },
                new PositionTest { RunId = run2.Id, Position = NeedlePosition.Start, NeedleFact = "test", FactRetrieved = true },
                new PositionTest { RunId = run2.Id, Position = NeedlePosition.Middle, NeedleFact = "test", FactRetrieved = true }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.Comparison.Should().NotBeNull();
        result.Comparison!.PositionComparison.Should().NotBeNull();
        result.Comparison.PositionComparison!.StartFound.Should().HaveCount(2);
        result.Comparison.PositionComparison.MiddleFound.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_IdentifiesFastestProvider()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run1 = CreateExperimentRun(batchId, "openai", "gpt-4o", status: ExperimentStatus.Completed);
            run1.StartedAt = DateTime.UtcNow.AddMinutes(-10);
            run1.CompletedAt = DateTime.UtcNow.AddMinutes(-5); // 5 minutes duration

            var run2 = CreateExperimentRun(batchId, "anthropic", "claude-3-sonnet", status: ExperimentStatus.Completed);
            run2.StartedAt = DateTime.UtcNow.AddMinutes(-10);
            run2.CompletedAt = DateTime.UtcNow.AddMinutes(-8); // 2 minutes duration (faster)

            dbContext.ExperimentRuns.AddRange(run1, run2);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await _service.GenerateJsonReportAsync(batchId);

        // Assert
        result.Should().NotBeNull();
        result!.Comparison.Should().NotBeNull();
        result.Comparison!.FastestProvider.Should().Contain("anthropic");
    }

    #endregion

    #region Helper Methods

    private async Task SeedExperimentRuns(string batchId, int count, ExperimentType type = ExperimentType.Persona, int athleteId = 1)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

        var providers = new[] { "openai", "anthropic", "google", "deepseek", "ollama" };
        var models = new[] { "gpt-4o", "claude-3-sonnet", "gemini-pro", "deepseek-chat", "llama3" };

        for (int i = 0; i < count; i++)
        {
            var run = new ExperimentRun
            {
                BatchId = batchId,
                Provider = providers[i % providers.Length],
                Model = models[i % models.Length],
                AthleteId = athleteId,
                Persona = "lasso",
                ExperimentType = type,
                Status = ExperimentStatus.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow,
                Temperature = 0.7,
                PromptVersion = "v1",
                EntryOrder = "reverse"
            };
            dbContext.ExperimentRuns.Add(run);
        }

        await dbContext.SaveChangesAsync();
    }

    private static ExperimentRun CreateExperimentRun(
        string? batchId,
        string provider,
        string model,
        ExperimentType type = ExperimentType.Persona,
        ExperimentStatus status = ExperimentStatus.Completed,
        int athleteId = 1,
        decimal estimatedCost = 0.005m,
        int tokensUsed = 500,
        bool isDeleted = false)
    {
        return new ExperimentRun
        {
            BatchId = batchId,
            Provider = provider,
            Model = model,
            AthleteId = athleteId,
            Persona = "lasso",
            ExperimentType = type,
            Status = status,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = status == ExperimentStatus.Completed ? DateTime.UtcNow : null,
            Temperature = 0.7,
            PromptVersion = "v1",
            EntryOrder = "reverse",
            EstimatedCost = estimatedCost,
            TokensUsed = tokensUsed,
            IsDeleted = isDeleted
        };
    }

    #endregion
}
