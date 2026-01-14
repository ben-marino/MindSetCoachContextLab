using MindSetCoach.Api.DTOs;

namespace MindSetCoach.Api.Services;

public interface ICoachService
{
    Task<List<AthleteListResponse>> GetMyAthletesAsync(int coachId);
    Task<AthleteDetailResponse> GetAthleteDetailAsync(int coachId, int athleteId);
    Task<List<JournalEntryResponse>> GetAthleteEntriesAsync(int coachId, int athleteId, bool? flaggedOnly = null);
}
