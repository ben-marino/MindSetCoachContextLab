using FluentAssertions;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Tests.DTOs;

public class ExperimentDtoMapperTests
{
    #region ToDto Tests (ExperimentRun to ExperimentRunDto)

    [Fact]
    public void ToDto_MapsBasicProperties()
    {
        // Arrange
        var run = CreateExperimentRun();

        // Act
        var dto = run.ToDto();

        // Assert
        dto.Id.Should().Be(run.Id);
        dto.Provider.Should().Be("openai");
        dto.Model.Should().Be("gpt-4o");
        dto.Temperature.Should().Be(0.7);
        dto.AthleteId.Should().Be(1);
        dto.Persona.Should().Be("goggins");
    }

    [Fact]
    public void ToDto_MapsExperimentType_ToLowercase()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.ExperimentType = ExperimentType.Position;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.ExperimentType.Should().Be("position");
    }

    [Theory]
    [InlineData(ExperimentType.Position, "position")]
    [InlineData(ExperimentType.Persona, "persona")]
    [InlineData(ExperimentType.Compression, "compression")]
    public void ToDto_MapsAllExperimentTypes(ExperimentType type, string expected)
    {
        // Arrange
        var run = CreateExperimentRun();
        run.ExperimentType = type;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.ExperimentType.Should().Be(expected);
    }

    [Fact]
    public void ToDto_MapsStatus_ToLowercase()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Status = ExperimentStatus.Running;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.Status.Should().Be("running");
    }

    [Theory]
    [InlineData(ExperimentStatus.Pending, "pending")]
    [InlineData(ExperimentStatus.Running, "running")]
    [InlineData(ExperimentStatus.Completed, "completed")]
    [InlineData(ExperimentStatus.Failed, "failed")]
    public void ToDto_MapsAllStatuses(ExperimentStatus status, string expected)
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Status = status;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.Status.Should().Be(expected);
    }

    [Fact]
    public void ToDto_MapsTimestamps()
    {
        // Arrange
        var startedAt = DateTime.UtcNow.AddMinutes(-10);
        var completedAt = DateTime.UtcNow;
        var run = CreateExperimentRun();
        run.StartedAt = startedAt;
        run.CompletedAt = completedAt;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.StartedAt.Should().Be(startedAt);
        dto.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void ToDto_MapsNullCompletedAt()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Status = ExperimentStatus.Running;
        run.CompletedAt = null;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ToDto_MapsTokensAndCost()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.TokensUsed = 1500;
        run.EstimatedCost = 0.0125m;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.TokensUsed.Should().Be(1500);
        dto.EstimatedCost.Should().Be(0.0125m);
    }

    [Fact]
    public void ToDto_MapsClaimCount()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new() { Id = 1, ClaimText = "Claim 1", IsSupported = true },
            new() { Id = 2, ClaimText = "Claim 2", IsSupported = false },
            new() { Id = 3, ClaimText = "Claim 3", IsSupported = true }
        };

        // Act
        var dto = run.ToDto();

        // Assert
        dto.ClaimCount.Should().Be(3);
    }

    [Fact]
    public void ToDto_MapsSupportedClaimCount()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new() { Id = 1, ClaimText = "Claim 1", IsSupported = true },
            new() { Id = 2, ClaimText = "Claim 2", IsSupported = false },
            new() { Id = 3, ClaimText = "Claim 3", IsSupported = true }
        };

        // Act
        var dto = run.ToDto();

        // Assert
        dto.SupportedClaimCount.Should().Be(2);
    }

    [Fact]
    public void ToDto_MapsPositionTestCount()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>
        {
            new() { Position = NeedlePosition.Start, FactRetrieved = true },
            new() { Position = NeedlePosition.Middle, FactRetrieved = false },
            new() { Position = NeedlePosition.End, FactRetrieved = true }
        };

        // Act
        var dto = run.ToDto();

        // Assert
        dto.PositionTestCount.Should().Be(3);
    }

    [Fact]
    public void ToDto_MapsBatchId()
    {
        // Arrange
        var batchId = Guid.NewGuid().ToString();
        var run = CreateExperimentRun();
        run.BatchId = batchId;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.BatchId.Should().Be(batchId);
    }

    [Fact]
    public void ToDto_WithNullClaims_ReturnsZeroCounts()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = null!;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.ClaimCount.Should().Be(0);
        dto.SupportedClaimCount.Should().Be(0);
    }

    [Fact]
    public void ToDto_WithNullPositionTests_ReturnsZeroCount()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = null!;

        // Act
        var dto = run.ToDto();

        // Assert
        dto.PositionTestCount.Should().Be(0);
    }

    #endregion

    #region ToDetailDto Tests (ExperimentRun to ExperimentRunDetailDto)

    [Fact]
    public void ToDetailDto_MapsBasicProperties()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.EntriesUsed = 7;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.Model.Should().Be("gpt-4o");
        detail.Temperature.Should().Be(0.7);
        detail.PromptVersion.Should().Be("v1");
        detail.Entries.Should().Be(7);
        detail.Order.Should().Be("reverse");
        detail.AthleteId.Should().Be(1);
    }

    [Fact]
    public void ToDetailDto_MapsStatus_ToLowercase()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Status = ExperimentStatus.Completed;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.Status.Should().Be("completed");
    }

    [Fact]
    public void ToDetailDto_MapsExperimentType_ToLowercase()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.ExperimentType = ExperimentType.Compression;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.ExperimentType.Should().Be("compression");
    }

    [Fact]
    public void ToDetailDto_MapsTokensAndCost()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.TokensUsed = 2000;
        run.EstimatedCost = 0.025m;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.Tokens.Should().Be(2000);
        detail.EstCost.Should().Be(0.025m);
    }

    [Fact]
    public void ToDetailDto_GroupsClaimsByPersona()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new() { Id = 1, ClaimText = "Goggins claim 1", IsSupported = true, Persona = "goggins" },
            new() { Id = 2, ClaimText = "Goggins claim 2", IsSupported = false, Persona = "goggins" },
            new() { Id = 3, ClaimText = "Lasso claim 1", IsSupported = true, Persona = "lasso" }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PersonaClaims.Should().NotBeEmpty();
        detail.PersonaClaims.Should().ContainKey("goggins");
        detail.PersonaClaims.Should().ContainKey("lasso");
        detail.PersonaClaims["goggins"].Should().HaveCount(2);
        detail.PersonaClaims["lasso"].Should().HaveCount(1);
    }

    [Fact]
    public void ToDetailDto_GroupsClaimsByPersona_CaseInsensitive()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new() { Id = 1, ClaimText = "Claim 1", IsSupported = true, Persona = "Goggins" },
            new() { Id = 2, ClaimText = "Claim 2", IsSupported = false, Persona = "GOGGINS" },
            new() { Id = 3, ClaimText = "Claim 3", IsSupported = true, Persona = "goggins" }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PersonaClaims.Should().ContainKey("goggins");
        detail.PersonaClaims["goggins"].Should().HaveCount(3);
    }

    [Fact]
    public void ToDetailDto_MapsClaimReceipts()
    {
        // Arrange
        var run = CreateExperimentRun();
        var entryDate = DateTime.UtcNow.AddDays(-1);
        run.Claims = new List<ExperimentClaim>
        {
            new()
            {
                Id = 1,
                ClaimText = "Test claim",
                IsSupported = true,
                Persona = "goggins",
                Receipts = new List<ClaimReceipt>
                {
                    new()
                    {
                        Id = 1,
                        JournalEntryId = 5,
                        EntryDate = entryDate,
                        MatchedSnippet = "matched text",
                        Confidence = 0.85
                    }
                }
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        var claim = detail.PersonaClaims["goggins"].First();
        claim.Receipts.Should().HaveCount(1);
        claim.Receipts[0].EntryId.Should().Be(5);
        claim.Receipts[0].Date.Should().Be(entryDate);
        claim.Receipts[0].Snippet.Should().Be("matched text");
        claim.Receipts[0].Confidence.Should().Be(0.85);
    }

    [Fact]
    public void ToDetailDto_MapsReceiptSource()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new()
            {
                Id = 1,
                ClaimText = "Test claim",
                IsSupported = true,
                Persona = "lasso",
                Receipts = new List<ClaimReceipt>
                {
                    new()
                    {
                        Id = 1,
                        JournalEntryId = 42,
                        MatchedSnippet = "snippet"
                    }
                }
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        var receipt = detail.PersonaClaims["lasso"].First().Receipts.First();
        receipt.Source.Should().Be("Entry #42");
    }

    [Fact]
    public void ToDetailDto_WithNullReceipts_ReturnsEmptyList()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>
        {
            new()
            {
                Id = 1,
                ClaimText = "Test claim",
                IsSupported = true,
                Persona = "goggins",
                Receipts = null!
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        var claim = detail.PersonaClaims["goggins"].First();
        claim.Receipts.Should().NotBeNull();
        claim.Receipts.Should().BeEmpty();
    }

    [Fact]
    public void ToDetailDto_MapsPositionResults_Start()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>
        {
            new()
            {
                Position = NeedlePosition.Start,
                NeedleFact = "shin splints",
                FactRetrieved = true,
                ResponseSnippet = "Found shin splints"
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().NotBeNull();
        detail.PositionResults!.Start.Found.Should().BeTrue();
        detail.PositionResults.Start.NeedleFact.Should().Be("shin splints");
        detail.PositionResults.Start.Snippet.Should().Be("Found shin splints");
    }

    [Fact]
    public void ToDetailDto_MapsPositionResults_Middle()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>
        {
            new()
            {
                Position = NeedlePosition.Middle,
                NeedleFact = "test fact",
                FactRetrieved = false,
                ResponseSnippet = ""
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().NotBeNull();
        detail.PositionResults!.Middle.Found.Should().BeFalse();
        detail.PositionResults.Middle.NeedleFact.Should().Be("test fact");
    }

    [Fact]
    public void ToDetailDto_MapsPositionResults_End()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>
        {
            new()
            {
                Position = NeedlePosition.End,
                NeedleFact = "end fact",
                FactRetrieved = true,
                ResponseSnippet = "found at end"
            }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().NotBeNull();
        detail.PositionResults!.End.Found.Should().BeTrue();
        detail.PositionResults.End.NeedleFact.Should().Be("end fact");
    }

    [Fact]
    public void ToDetailDto_MapsAllPositions()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>
        {
            new() { Position = NeedlePosition.Start, NeedleFact = "fact", FactRetrieved = true, ResponseSnippet = "start" },
            new() { Position = NeedlePosition.Middle, NeedleFact = "fact", FactRetrieved = false, ResponseSnippet = "" },
            new() { Position = NeedlePosition.End, NeedleFact = "fact", FactRetrieved = true, ResponseSnippet = "end" }
        };

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().NotBeNull();
        detail.PositionResults!.Start.Found.Should().BeTrue();
        detail.PositionResults.Middle.Found.Should().BeFalse();
        detail.PositionResults.End.Found.Should().BeTrue();
    }

    [Fact]
    public void ToDetailDto_WithNullClaims_ReturnsEmptyPersonaClaims()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = null!;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PersonaClaims.Should().NotBeNull();
        detail.PersonaClaims.Should().BeEmpty();
    }

    [Fact]
    public void ToDetailDto_WithEmptyClaims_ReturnsEmptyPersonaClaims()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.Claims = new List<ExperimentClaim>();

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PersonaClaims.Should().NotBeNull();
        detail.PersonaClaims.Should().BeEmpty();
    }

    [Fact]
    public void ToDetailDto_WithNullPositionTests_ReturnsNullPositionResults()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = null!;

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WithEmptyPositionTests_ReturnsNullPositionResults()
    {
        // Arrange
        var run = CreateExperimentRun();
        run.PositionTests = new List<PositionTest>();

        // Act
        var detail = run.ToDetailDto();

        // Assert
        detail.PositionResults.Should().BeNull();
    }

    #endregion

    #region ToDto Tests (ExperimentPreset to ExperimentPresetDto)

    [Fact]
    public void PresetToDto_MapsBasicProperties()
    {
        // Arrange
        var preset = new ExperimentPreset
        {
            Id = 1,
            Name = "Test Preset",
            Description = "A test preset",
            Config = "{\"experimentType\":\"position\",\"provider\":\"openai\",\"model\":\"gpt-4o\"}",
            CreatedAt = DateTime.UtcNow,
            IsDefault = false
        };

        // Act
        var dto = preset.ToDto();

        // Assert
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test Preset");
        dto.Description.Should().Be("A test preset");
        dto.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void PresetToDto_DeserializesConfig()
    {
        // Arrange
        var preset = new ExperimentPreset
        {
            Id = 1,
            Name = "Test",
            Config = "{\"experimentType\":\"position\",\"provider\":\"anthropic\",\"model\":\"claude-3-sonnet\",\"temperature\":0.5}",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var dto = preset.ToDto();

        // Assert
        dto.Config.Should().NotBeNull();
        dto.Config.ExperimentType.Should().Be("position");
        dto.Config.Provider.Should().Be("anthropic");
        dto.Config.Model.Should().Be("claude-3-sonnet");
        dto.Config.Temperature.Should().Be(0.5);
    }

    [Fact]
    public void PresetToDto_WithInvalidJson_ReturnsEmptyConfig()
    {
        // Arrange
        var preset = new ExperimentPreset
        {
            Id = 1,
            Name = "Test",
            Config = "invalid json {{{",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var dto = preset.ToDto();

        // Assert
        dto.Config.Should().NotBeNull();
        dto.Config.ExperimentType.Should().Be("persona"); // Default value
    }

    #endregion

    #region ToEntity Tests (CreatePresetRequest to ExperimentPreset)

    [Fact]
    public void ToEntity_MapsBasicProperties()
    {
        // Arrange
        var request = new CreatePresetRequest
        {
            Name = "New Preset",
            Description = "A new preset",
            Config = new PresetConfigDto
            {
                ExperimentType = "position",
                Provider = "openai",
                Model = "gpt-4o"
            }
        };

        // Act
        var entity = request.ToEntity();

        // Assert
        entity.Name.Should().Be("New Preset");
        entity.Description.Should().Be("A new preset");
        entity.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_SerializesConfig()
    {
        // Arrange
        var request = new CreatePresetRequest
        {
            Name = "Test",
            Config = new PresetConfigDto
            {
                ExperimentType = "compression",
                Provider = "google",
                Model = "gemini-pro",
                Temperature = 0.3
            }
        };

        // Act
        var entity = request.ToEntity();

        // Assert
        entity.Config.Should().NotBeNullOrEmpty();
        entity.Config.Should().Contain("compression");
        entity.Config.Should().Contain("google");
        entity.Config.Should().Contain("gemini-pro");
    }

    [Fact]
    public void ToEntity_SetsCreatedAt()
    {
        // Arrange
        var request = new CreatePresetRequest
        {
            Name = "Test",
            Config = new PresetConfigDto()
        };
        var beforeCreate = DateTime.UtcNow;

        // Act
        var entity = request.ToEntity();
        var afterCreate = DateTime.UtcNow;

        // Assert
        entity.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        entity.CreatedAt.Should().BeOnOrBefore(afterCreate);
    }

    #endregion

    #region Helper Methods

    private static ExperimentRun CreateExperimentRun()
    {
        return new ExperimentRun
        {
            Id = 1,
            Provider = "openai",
            Model = "gpt-4o",
            Temperature = 0.7,
            PromptVersion = "v1",
            AthleteId = 1,
            Persona = "goggins",
            ExperimentType = ExperimentType.Persona,
            EntryOrder = "reverse",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow,
            Status = ExperimentStatus.Completed,
            TokensUsed = 1000,
            EstimatedCost = 0.01m,
            Claims = new List<ExperimentClaim>(),
            PositionTests = new List<PositionTest>()
        };
    }

    #endregion
}
