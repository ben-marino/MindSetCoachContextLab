namespace MindSetCoach.Api.DTOs;

public class AthleteListResponse
{
    public int AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalEntries { get; set; }
    public DateTime? LastEntryDate { get; set; }
    public int FlaggedEntriesCount { get; set; }
}
