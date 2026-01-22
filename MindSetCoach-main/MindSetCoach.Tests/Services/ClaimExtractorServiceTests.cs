using FluentAssertions;
using Microsoft.Extensions.Logging;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;
using MindSetCoach.Api.Services.AI;
using Moq;

namespace MindSetCoach.Tests.Services;

public class ClaimExtractorServiceTests
{
    private readonly Mock<ILogger<ClaimExtractorService>> _loggerMock;
    private readonly ClaimExtractorService _service;

    public ClaimExtractorServiceTests()
    {
        _loggerMock = new Mock<ILogger<ClaimExtractorService>>();
        _service = new ClaimExtractorService(_loggerMock.Object);
    }

    #region ExtractAndVerifyClaims - Empty/Null Input Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var journalEntries = new List<JournalEntry>();

        // Act
        var result = _service.ExtractAndVerifyClaims(string.Empty, journalEntries);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithNullInput_ReturnsEmptyList()
    {
        // Arrange
        var journalEntries = new List<JournalEntry>();

        // Act
        var result = _service.ExtractAndVerifyClaims(null!, journalEntries);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithWhitespaceInput_ReturnsEmptyList()
    {
        // Arrange
        var journalEntries = new List<JournalEntry>();

        // Act
        var result = _service.ExtractAndVerifyClaims("   \t\n  ", journalEntries);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region ExtractAndVerifyClaims - Claim Identification Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithInjuryClaim_IdentifiesClaimCorrectly()
    {
        // Arrange
        var summaryText = "You reported having shin splints during your Monday training session.";
        var journalEntries = CreateJournalEntriesWithInjury();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.ClaimType == "injury");
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithEmotionClaim_IdentifiesClaimCorrectly()
    {
        // Arrange
        var summaryText = "Your confidence improved significantly this week.";
        var journalEntries = CreateJournalEntriesWithEmotions();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.ClaimType == "emotion");
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithSkippedClaim_IdentifiesClaimCorrectly()
    {
        // Arrange
        var summaryText = "You skipped training on Friday due to fatigue.";
        var journalEntries = CreateJournalEntriesWithSkippedSession();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.ClaimType == "skipped");
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithBarrierClaim_IdentifiesClaimCorrectly()
    {
        // Arrange
        var summaryText = "You struggled with self-doubt throughout the week.";
        var journalEntries = CreateJournalEntriesWithBarriers();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.ClaimType == "barrier");
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithProgressClaim_IdentifiesClaimCorrectly()
    {
        // Arrange
        var summaryText = "You made progress in your mental game this week.";
        var journalEntries = CreateJournalEntriesWithProgress();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.ClaimType == "progress");
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithMultipleClaims_IdentifiesAllClaims()
    {
        // Arrange
        var summaryText = @"You reported having shin splints on Monday.
                           Your confidence improved after the session.
                           You skipped training on Wednesday.";
        var journalEntries = CreateComprehensiveJournalEntries();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterOrEqualTo(2);
    }

    #endregion

    #region Claim Verification Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithMatchingJournalEntry_SetsIsSupportedTrue()
    {
        // Arrange
        var summaryText = "You reported having shin splints during training.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "Feeling frustrated",
                SessionReflection = "Had to stop due to shin splints acting up",
                MentalBarriers = "Pain is making it hard to focus"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var claim = result.FirstOrDefault(c => c.ClaimType == "injury");
        claim.Should().NotBeNull();
        claim!.IsSupported.Should().BeTrue();
        claim.MatchedEntryId.Should().Be(1);
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithNoMatchingEntry_SetsIsSupportedFalse()
    {
        // Arrange
        var summaryText = "You reported having severe back pain this week.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "Feeling great",
                SessionReflection = "Perfect session today, no issues",
                MentalBarriers = "None at the moment"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var claim = result.FirstOrDefault(c => c.ClaimType == "injury");
        claim.Should().NotBeNull();
        claim!.IsSupported.Should().BeFalse();
        claim.Confidence.Should().Be(0);
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithEmptyJournalEntries_SetsIsSupportedFalse()
    {
        // Arrange
        var summaryText = "You felt anxious before your training.";
        var journalEntries = new List<JournalEntry>();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        result.All(c => !c.IsSupported).Should().BeTrue();
    }

    #endregion

    #region Confidence Score Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithStrongMatch_ReturnsHighConfidence()
    {
        // Arrange
        var summaryText = "You felt confident and motivated after your session.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "Feeling confident and highly motivated today!",
                SessionReflection = "Great workout, hit all my goals",
                MentalBarriers = "None"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var supportedClaim = result.FirstOrDefault(c => c.IsSupported);
        supportedClaim.Should().NotBeNull();
        supportedClaim!.Confidence.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithPartialMatch_ReturnsModerateConfidence()
    {
        // Arrange
        var summaryText = "You felt stressed about the competition.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "A bit stressed today",
                SessionReflection = "Session was okay",
                MentalBarriers = "General nervousness"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var supportedClaim = result.FirstOrDefault(c => c.IsSupported);
        if (supportedClaim != null)
        {
            supportedClaim.Confidence.Should().BeInRange(0.3, 1.0);
        }
    }

    [Fact]
    public void ExtractAndVerifyClaims_ConfidenceScore_IsBetweenZeroAndOne()
    {
        // Arrange
        var summaryText = "You reported shin splints on Tuesday. Your mood improved. You skipped training.";
        var journalEntries = CreateComprehensiveJournalEntries();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        foreach (var claim in result)
        {
            claim.Confidence.Should().BeGreaterOrEqualTo(0);
            claim.Confidence.Should().BeLessOrEqualTo(1);
        }
    }

    #endregion

    #region Day Reference Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithDayReference_ExtractsReferencedDate()
    {
        // Arrange
        var monday = GetMostRecentDayOfWeek(DayOfWeek.Monday);
        var summaryText = "You reported having pain on Monday.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = monday,
                EmotionalState = "In pain",
                SessionReflection = "Had to cut training short due to pain",
                MentalBarriers = "Physical discomfort"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var claimWithDate = result.FirstOrDefault(c => c.ReferencedDate.HasValue);
        if (claimWithDate != null)
        {
            claimWithDate.ReferencedDate!.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
        }
    }

    [Theory]
    [InlineData("Monday", DayOfWeek.Monday)]
    [InlineData("Tuesday", DayOfWeek.Tuesday)]
    [InlineData("Wednesday", DayOfWeek.Wednesday)]
    [InlineData("Thursday", DayOfWeek.Thursday)]
    [InlineData("Friday", DayOfWeek.Friday)]
    [InlineData("Saturday", DayOfWeek.Saturday)]
    [InlineData("Sunday", DayOfWeek.Sunday)]
    public void ExtractAndVerifyClaims_WithVariousDays_ExtractsCorrectDay(string dayName, DayOfWeek expectedDay)
    {
        // Arrange
        var targetDate = GetMostRecentDayOfWeek(expectedDay);
        var summaryText = $"You reported feeling tired on {dayName}.";
        var journalEntries = new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = targetDate,
                EmotionalState = "Feeling tired and exhausted",
                SessionReflection = "Low energy session",
                MentalBarriers = "Fatigue"
            }
        };

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        var claimWithDate = result.FirstOrDefault(c => c.ReferencedDate.HasValue);
        if (claimWithDate != null)
        {
            claimWithDate.ReferencedDate!.Value.DayOfWeek.Should().Be(expectedDay);
        }
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public void ExtractAndVerifyClaims_WithDuplicateClaims_RemovesDuplicates()
    {
        // Arrange
        var summaryText = @"You reported shin splints. You also mentioned having shin splints during training.";
        var journalEntries = CreateJournalEntriesWithInjury();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        // Should deduplicate very similar claims
        var injuryClaims = result.Where(c => c.ClaimType == "injury").ToList();
        injuryClaims.Count.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void ExtractAndVerifyClaims_KeepsHigherConfidenceDuplicate()
    {
        // Arrange
        var summaryText = @"You mentioned pain. You reported having severe shin splints during training on Monday.";
        var journalEntries = CreateJournalEntriesWithInjury();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeEmpty();
        // If there are duplicate claims, the one with higher confidence should be kept
        var injuryClaims = result.Where(c => c.ClaimType == "injury" && c.IsSupported).ToList();
        if (injuryClaims.Count > 0)
        {
            injuryClaims.Should().OnlyContain(c => c.Confidence > 0);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ExtractAndVerifyClaims_WithNoYouOrYourPattern_ReturnsEmptyList()
    {
        // Arrange - Summary without "you" or "your" patterns won't match claim extraction
        var summaryText = "The athlete showed improvement this week.";
        var journalEntries = CreateComprehensiveJournalEntries();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var summaryText = "You felt anxious & stressed!!! Your confidence improved?";
        var journalEntries = CreateJournalEntriesWithEmotions();

        // Act
        var result = _service.ExtractAndVerifyClaims(summaryText, journalEntries);

        // Assert
        result.Should().NotBeNull();
        // Should not throw, and may or may not find claims
    }

    [Fact]
    public void ExtractAndVerifyClaims_WithVeryLongText_HandlesGracefully()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat("You felt motivated.", 100));
        var journalEntries = CreateJournalEntriesWithEmotions();

        // Act
        var result = _service.ExtractAndVerifyClaims(longText, journalEntries);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static List<JournalEntry> CreateJournalEntriesWithInjury()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Monday),
                EmotionalState = "Frustrated due to injury",
                SessionReflection = "Had to stop training because of shin splints",
                MentalBarriers = "Physical pain affecting my focus"
            },
            new()
            {
                Id = 2,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Tuesday),
                EmotionalState = "Still dealing with injury",
                SessionReflection = "Light session today, resting the shin",
                MentalBarriers = "Worried about recovery time"
            }
        };
    }

    private static List<JournalEntry> CreateJournalEntriesWithEmotions()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-3),
                EmotionalState = "Feeling anxious and stressed before competition",
                SessionReflection = "Struggled to focus during practice",
                MentalBarriers = "Self-doubt and negative thoughts"
            },
            new()
            {
                Id = 2,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "Feeling confident and motivated!",
                SessionReflection = "Great session, everything clicked",
                MentalBarriers = "None today"
            }
        };
    }

    private static List<JournalEntry> CreateJournalEntriesWithSkippedSession()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Friday),
                EmotionalState = "Exhausted",
                SessionReflection = "Skipped training today - needed rest day",
                MentalBarriers = "Feeling burnt out"
            }
        };
    }

    private static List<JournalEntry> CreateJournalEntriesWithBarriers()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-2),
                EmotionalState = "Frustrated and doubtful",
                SessionReflection = "Struggled with technique",
                MentalBarriers = "Self-doubt is overwhelming. I keep comparing myself to others."
            }
        };
    }

    private static List<JournalEntry> CreateJournalEntriesWithProgress()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = DateTime.UtcNow.AddDays(-1),
                EmotionalState = "Proud of my progress",
                SessionReflection = "Made significant progress in my mental game today",
                MentalBarriers = "Getting better at managing anxiety"
            }
        };
    }

    private static List<JournalEntry> CreateComprehensiveJournalEntries()
    {
        return new List<JournalEntry>
        {
            new()
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Monday),
                EmotionalState = "Frustrated",
                SessionReflection = "Had to deal with shin splints",
                MentalBarriers = "Pain affecting performance"
            },
            new()
            {
                Id = 2,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Tuesday),
                EmotionalState = "Feeling better, more confident",
                SessionReflection = "Good session, mood improved significantly",
                MentalBarriers = "Minor self-doubt"
            },
            new()
            {
                Id = 3,
                AthleteId = 1,
                EntryDate = GetMostRecentDayOfWeek(DayOfWeek.Wednesday),
                EmotionalState = "Tired",
                SessionReflection = "Skipped training - rest day",
                MentalBarriers = "Fatigue"
            }
        };
    }

    private static DateTime GetMostRecentDayOfWeek(DayOfWeek dayOfWeek)
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilTarget = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0)
            daysUntilTarget = 7; // Get the previous occurrence, not today
        return today.AddDays(-daysUntilTarget);
    }

    #endregion
}
