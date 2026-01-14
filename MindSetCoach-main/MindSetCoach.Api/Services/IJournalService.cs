using MindSetCoach.Api.DTOs;

namespace MindSetCoach.Api.Services;

public interface IJournalService
{
    Task<JournalEntryResponse> CreateEntryAsync(int athleteId, CreateJournalEntryRequest request);
    Task<JournalEntryResponse> GetEntryAsync(int entryId);
    Task<List<JournalEntryResponse>> GetAthleteEntriesAsync(int athleteId);
    Task<JournalEntryResponse> UpdateEntryAsync(int entryId, UpdateJournalEntryRequest request);
    Task<bool> FlagEntryAsync(int entryId, bool flagged);
    Task DeleteEntryAsync(int entryId);
}
