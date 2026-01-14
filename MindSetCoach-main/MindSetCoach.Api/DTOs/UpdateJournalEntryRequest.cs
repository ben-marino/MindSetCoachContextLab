using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.DTOs;

public class UpdateJournalEntryRequest
{
    [MaxLength(1000, ErrorMessage = "Emotional state cannot exceed 1000 characters")]
    public string? EmotionalState { get; set; }

    [MaxLength(2000, ErrorMessage = "Session reflection cannot exceed 2000 characters")]
    public string? SessionReflection { get; set; }

    [MaxLength(1000, ErrorMessage = "Mental barriers cannot exceed 1000 characters")]
    public string? MentalBarriers { get; set; }
}
