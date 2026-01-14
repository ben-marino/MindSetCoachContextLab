using MindSetCoach.Web.Models;

namespace MindSetCoach.Web.Services;

public interface IApiClient
{
    Task<(bool Success, AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request);
    Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? Error)> CreateJournalEntryAsync(JournalEntryRequest request, string token);
    Task<(bool Success, List<JournalEntryResponse>? Entries, string? Error)> GetJournalEntriesAsync(int athleteId, string token);
    Task<(bool Success, JournalEntryResponse? Entry, string? Error)> GetJournalEntryByIdAsync(int entryId, string token);
    Task<(bool Success, List<AthleteListResponse>? Athletes, string? Error)> GetMyAthletesAsync(string token);
    Task<(bool Success, AthleteDetailResponse? AthleteDetail, string? Error)> GetAthleteDetailAsync(int athleteId, string token);
    Task<(bool Success, string? Error)> FlagJournalEntryAsync(int entryId, bool flagged, string token);
}
