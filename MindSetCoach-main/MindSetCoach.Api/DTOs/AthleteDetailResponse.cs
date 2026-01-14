namespace MindSetCoach.Api.DTOs;

public class AthleteDetailResponse
{
    public int AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public List<JournalEntryResponse> JournalEntries { get; set; } = new List<JournalEntryResponse>();
    public int TotalEntries { get; set; }
    public int FlaggedEntriesCount { get; set; }
}
