using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.DTOs;

public class CreateJournalEntryRequest
{
    [Required(ErrorMessage = "Entry date is required")]
    public DateTime EntryDate { get; set; }

    [Required(ErrorMessage = "Emotional state is required")]
    [MaxLength(1000, ErrorMessage = "Emotional state cannot exceed 1000 characters")]
    public string EmotionalState { get; set; } = string.Empty;

    [Required(ErrorMessage = "Session reflection is required")]
    [MaxLength(2000, ErrorMessage = "Session reflection cannot exceed 2000 characters")]
    public string SessionReflection { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mental barriers is required")]
    [MaxLength(1000, ErrorMessage = "Mental barriers cannot exceed 1000 characters")]
    public string MentalBarriers { get; set; } = string.Empty;
}
