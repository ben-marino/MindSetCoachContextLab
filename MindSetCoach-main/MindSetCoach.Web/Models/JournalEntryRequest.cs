using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Web.Models;

public class JournalEntryRequest
{
    [Required(ErrorMessage = "Entry date is required")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Please describe how you're feeling")]
    [MaxLength(1000, ErrorMessage = "Emotional state cannot exceed 1000 characters")]
    public string EmotionalState { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please reflect on what went well/didn't go well")]
    [MaxLength(2000, ErrorMessage = "Session reflection cannot exceed 2000 characters")]
    public string SessionReflection { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please describe any mental barriers")]
    [MaxLength(1000, ErrorMessage = "Mental barriers cannot exceed 1000 characters")]
    public string MentalBarriers { get; set; } = string.Empty;
}
