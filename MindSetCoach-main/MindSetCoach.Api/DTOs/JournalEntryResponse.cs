namespace MindSetCoach.Api.DTOs;

public class JournalEntryResponse
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public string AthleteName { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string EmotionalState { get; set; } = string.Empty;
    public string SessionReflection { get; set; } = string.Empty;
    public string MentalBarriers { get; set; } = string.Empty;
    public bool IsFlagged { get; set; }
    public DateTime CreatedAt { get; set; }
}
