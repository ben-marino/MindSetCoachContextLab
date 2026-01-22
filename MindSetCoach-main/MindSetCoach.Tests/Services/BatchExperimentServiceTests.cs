using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;
using Moq;

namespace MindSetCoach.Tests.Services;

public class BatchExperimentServiceTests : IDisposable
{
    private readonly Mock<IExperimentRunnerService> _experimentRunnerMock;
    private readonly Mock<ILogger<BatchExperimentService>> _loggerMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BatchExperimentService _service;
    private readonly string _databaseName;

    public BatchExperimentServiceTests()
    {
        _experimentRunnerMock = new Mock<IExperimentRunnerService>();
        _loggerMock = new Mock<ILogger<BatchExperimentService>>();
        _databaseName = Guid.NewGuid().ToString();

        // Setup service collection with in-memory databases
        var serviceCollection = new ServiceCollection();

        // Configure ExperimentsDbContext with in-memory database
        serviceCollection.AddDbContext<ExperimentsDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName));

        // Configure MindSetCoachDbContext with in-memory database
        serviceCollection.AddDbContext<MindSetCoachDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName + "_main"));

        // Register mock services needed by BatchExperimentService's internal methods
        var mockAIService = new Mock<IMentalCoachAIService>();
        serviceCollection.AddSingleton(mockAIService.Object);

        var mockClaimExtractor = new Mock<IClaimExtractorService>();
        serviceCollection.AddSingleton(mockClaimExtractor.Object);

        // Register ContextExperimentLogger with real dependencies
        serviceCollection.AddScoped<ContextExperimentLogger>();
        serviceCollection.AddSingleton<ILogger<ContextExperimentLogger>>(
            new Mock<ILogger<ContextExperimentLogger>>().Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _service = new BatchExperimentService(
            _scopeFactory,
            _experimentRunnerMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    #region StartBatchAsync Tests

    [Fact]
    public async Task StartBatchAsync_WithValidRequest_CreatesCorrectNumberOfRuns()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "position",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o" },
                new() { Provider = "anthropic", Model = "claude-3-sonnet" },
                new() { Provider = "google", Model = "gemini-pro" }
            },
            Persona = "goggins",
            Temperature = 0.7
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.RunIds.Should().HaveCount(3);
        response.BatchId.Should().NotBeNullOrEmpty();
        response.Status.Should().Be("running");
    }

    [Fact]
    public async Task StartBatchAsync_AssignsUniqueBatchId()
    {
        // Arrange
        var request1 = CreateValidBatchRequest(2);
        var request2 = CreateValidBatchRequest(2);

        // Act
        var response1 = await _service.StartBatchAsync(request1);
        var response2 = await _service.StartBatchAsync(request2);

        // Assert
        response1.BatchId.Should().NotBeNullOrEmpty();
        response2.BatchId.Should().NotBeNullOrEmpty();
        response1.BatchId.Should().NotBe(response2.BatchId);
    }

    [Fact]
    public async Task StartBatchAsync_WithSingleProvider_CreatesOneRun()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "persona",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o-mini" }
            },
            Persona = "lasso"
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        response.RunIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartBatchAsync_SetsCorrectExperimentType_Compression()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "compression",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.ExperimentType.Should().Be(ExperimentType.Compression);
    }

    [Fact]
    public async Task StartBatchAsync_SetsCorrectExperimentType_Persona()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "persona",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "anthropic", Model = "claude-sonnet-4" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.ExperimentType.Should().Be(ExperimentType.Persona);
    }

    [Fact]
    public async Task StartBatchAsync_SetsCorrectExperimentType_Position()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "position",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "google", Model = "gemini-2.5-flash" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.ExperimentType.Should().Be(ExperimentType.Position);
    }

    [Fact]
    public async Task StartBatchAsync_StoresProviderAndModel()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "position",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "deepseek", Model = "deepseek-chat" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.Provider.Should().Be("deepseek");
        run.Model.Should().Be("deepseek-chat");
    }

    [Fact]
    public async Task StartBatchAsync_StoresBatchIdOnAllRuns()
    {
        // Arrange
        var request = CreateValidBatchRequest(3);

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var runs = await dbContext.ExperimentRuns
            .Where(r => response.RunIds.Contains(r.Id))
            .ToListAsync();

        runs.Should().HaveCount(3);
        runs.Should().OnlyContain(r => r.BatchId == response.BatchId);
    }

    [Fact]
    public async Task StartBatchAsync_SetsTemperatureCorrectly()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "persona",
            Temperature = 0.3,
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.Temperature.Should().Be(0.3);
    }

    [Fact]
    public async Task StartBatchAsync_SetsInitialStatusToPending()
    {
        // Arrange
        var request = CreateValidBatchRequest(1);

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.Status.Should().Be(ExperimentStatus.Pending);
    }

    [Fact]
    public async Task StartBatchAsync_ReturnsMessageWithProviderCount()
    {
        // Arrange
        var request = CreateValidBatchRequest(4);

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        response.Message.Should().Contain("4 providers");
    }

    [Fact]
    public async Task StartBatchAsync_SetsAthleteIdCorrectly()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 42,
            ExperimentType = "position",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.AthleteId.Should().Be(42);
    }

    [Fact]
    public async Task StartBatchAsync_SetsPersonaCorrectly()
    {
        // Arrange
        var request = new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "persona",
            Persona = "goggins",
            Providers = new List<ProviderModelPair>
            {
                new() { Provider = "openai", Model = "gpt-4o" }
            }
        };

        // Act
        var response = await _service.StartBatchAsync(request);

        // Assert
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        var run = await dbContext.ExperimentRuns.FirstOrDefaultAsync(r => r.Id == response.RunIds[0]);

        run.Should().NotBeNull();
        run!.Persona.Should().Be("goggins");
    }

    #endregion

    #region GetBatchProgressChannel Tests

    [Fact]
    public async Task GetBatchProgressChannel_ForRunningBatch_ReturnsValidChannel()
    {
        // Arrange
        var request = CreateValidBatchRequest(2);
        var response = await _service.StartBatchAsync(request);

        // Act
        var channel = _service.GetBatchProgressChannel(response.BatchId);

        // Assert
        channel.Should().NotBeNull();
    }

    [Fact]
    public void GetBatchProgressChannel_ForUnknownBatchId_ReturnsNull()
    {
        // Arrange
        var unknownBatchId = Guid.NewGuid().ToString();

        // Act
        var channel = _service.GetBatchProgressChannel(unknownBatchId);

        // Assert
        channel.Should().BeNull();
    }

    [Fact]
    public void GetBatchProgressChannel_WithEmptyBatchId_ReturnsNull()
    {
        // Act
        var channel = _service.GetBatchProgressChannel(string.Empty);

        // Assert
        channel.Should().BeNull();
    }

    [Fact]
    public void GetBatchProgressChannel_WithNullBatchId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.GetBatchProgressChannel(null!);

        // Assert - The service throws ArgumentNullException for null keys
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region IsBatchRunning Tests

    [Fact]
    public async Task IsBatchRunning_ForActiveBatch_ReturnsTrue()
    {
        // Arrange
        var request = CreateValidBatchRequest(2);
        var response = await _service.StartBatchAsync(request);

        // Act
        var isRunning = _service.IsBatchRunning(response.BatchId);

        // Assert
        isRunning.Should().BeTrue();
    }

    [Fact]
    public void IsBatchRunning_ForUnknownBatchId_ReturnsFalse()
    {
        // Arrange
        var unknownBatchId = Guid.NewGuid().ToString();

        // Act
        var isRunning = _service.IsBatchRunning(unknownBatchId);

        // Assert
        isRunning.Should().BeFalse();
    }

    [Fact]
    public void IsBatchRunning_WithEmptyBatchId_ReturnsFalse()
    {
        // Act
        var isRunning = _service.IsBatchRunning(string.Empty);

        // Assert
        isRunning.Should().BeFalse();
    }

    [Fact]
    public void IsBatchRunning_WithNullBatchId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.IsBatchRunning(null!);

        // Assert - The service throws ArgumentNullException for null keys
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetBatchResultsAsync Tests

    [Fact]
    public async Task GetBatchResultsAsync_ForUnknownBatchId_ReturnsNull()
    {
        // Arrange
        var unknownBatchId = Guid.NewGuid().ToString();

        // Act
        var results = await _service.GetBatchResultsAsync(unknownBatchId);

        // Assert
        results.Should().BeNull();
    }

    [Fact]
    public async Task GetBatchResultsAsync_WithExistingRuns_ReturnsResults()
    {
        // Arrange - Create runs directly in the database
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                new ExperimentRun
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    AthleteId = 1,
                    Persona = "goggins",
                    ExperimentType = ExperimentType.Position,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                },
                new ExperimentRun
                {
                    Provider = "anthropic",
                    Model = "claude-3-sonnet",
                    AthleteId = 1,
                    Persona = "goggins",
                    ExperimentType = ExperimentType.Position,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.BatchId.Should().Be(batchId);
        results.TotalProviders.Should().Be(2);
        results.CompletedProviders.Should().Be(2);
        results.ProviderResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBatchResultsAsync_WithMixedStatus_ReturnsPartialStatus()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                new ExperimentRun
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                },
                new ExperimentRun
                {
                    Provider = "anthropic",
                    Model = "claude-3-sonnet",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Failed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.Status.Should().Be("partial");
    }

    [Fact]
    public async Task GetBatchResultsAsync_WithAllFailed_ReturnsFailedStatus()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                new ExperimentRun
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    AthleteId = 1,
                    Persona = "goggins",
                    ExperimentType = ExperimentType.Position,
                    Status = ExperimentStatus.Failed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                },
                new ExperimentRun
                {
                    Provider = "anthropic",
                    Model = "claude-3-sonnet",
                    AthleteId = 1,
                    Persona = "goggins",
                    ExperimentType = ExperimentType.Position,
                    Status = ExperimentStatus.Failed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.Status.Should().Be("failed");
    }

    [Fact]
    public async Task GetBatchResultsAsync_WithAllCompleted_ReturnsCompletedStatus()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                new ExperimentRun
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                },
                new ExperimentRun
                {
                    Provider = "anthropic",
                    Model = "claude-3-sonnet",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task GetBatchResultsAsync_ExcludesDeletedRuns()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            dbContext.ExperimentRuns.AddRange(
                new ExperimentRun
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    IsDeleted = false,
                    StartedAt = DateTime.UtcNow
                },
                new ExperimentRun
                {
                    Provider = "anthropic",
                    Model = "claude-3-sonnet",
                    AthleteId = 1,
                    Persona = "lasso",
                    ExperimentType = ExperimentType.Persona,
                    Status = ExperimentStatus.Completed,
                    BatchId = batchId,
                    IsDeleted = true, // This should be excluded
                    StartedAt = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.TotalProviders.Should().Be(1);
        results.ProviderResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBatchResultsAsync_IncludesPositionTestResults()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
            var run = new ExperimentRun
            {
                Provider = "openai",
                Model = "gpt-4o",
                AthleteId = 1,
                Persona = "goggins",
                ExperimentType = ExperimentType.Position,
                Status = ExperimentStatus.Completed,
                BatchId = batchId,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow
            };
            dbContext.ExperimentRuns.Add(run);
            await dbContext.SaveChangesAsync();

            // Add position tests
            dbContext.PositionTests.AddRange(
                new PositionTest
                {
                    RunId = run.Id,
                    Position = NeedlePosition.Start,
                    NeedleFact = "shin splints",
                    FactRetrieved = true,
                    ResponseSnippet = "Found shin splints"
                },
                new PositionTest
                {
                    RunId = run.Id,
                    Position = NeedlePosition.Middle,
                    NeedleFact = "shin splints",
                    FactRetrieved = false,
                    ResponseSnippet = ""
                },
                new PositionTest
                {
                    RunId = run.Id,
                    Position = NeedlePosition.End,
                    NeedleFact = "shin splints",
                    FactRetrieved = true,
                    ResponseSnippet = "shin splints mentioned"
                }
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await _service.GetBatchResultsAsync(batchId);

        // Assert
        results.Should().NotBeNull();
        results!.ProviderResults.Should().HaveCount(1);
        var providerResult = results.ProviderResults.First();
        providerResult.PositionResults.Should().NotBeNull();
        providerResult.PositionResults!.Start.Found.Should().BeTrue();
        providerResult.PositionResults.Middle.Found.Should().BeFalse();
        providerResult.PositionResults.End.Found.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static BatchExperimentRequest CreateValidBatchRequest(int providerCount)
    {
        var providers = new List<ProviderModelPair>();
        var providerOptions = new[]
        {
            ("openai", "gpt-4o"),
            ("anthropic", "claude-3-sonnet"),
            ("google", "gemini-pro"),
            ("deepseek", "deepseek-chat"),
            ("ollama", "llama3")
        };

        for (int i = 0; i < providerCount && i < providerOptions.Length; i++)
        {
            providers.Add(new ProviderModelPair
            {
                Provider = providerOptions[i].Item1,
                Model = providerOptions[i].Item2
            });
        }

        return new BatchExperimentRequest
        {
            AthleteId = 1,
            ExperimentType = "position",
            Providers = providers,
            Persona = "goggins",
            Temperature = 0.7
        };
    }

    #endregion
}
