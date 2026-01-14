using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;

namespace MindSetCoach.Api.Services;

public class CoachService : ICoachService
{
    private readonly MindSetCoachDbContext _context;
    private readonly ILogger<CoachService> _logger;

    public CoachService(MindSetCoachDbContext context, ILogger<CoachService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<AthleteListResponse>> GetMyAthletesAsync(int coachId)
    {
        // Verify coach exists
        var coachExists = await _context.Coaches.AnyAsync(c => c.Id == coachId);
        if (!coachExists)
        {
            throw new InvalidOperationException($"Coach with ID {coachId} not found.");
        }

        // Get all athletes for this coach with their journal entries
        var athletes = await _context.Athletes
            .Where(a => a.CoachId == coachId)
            .Include(a => a.JournalEntries)
            .OrderBy(a => a.Name)
            .ToListAsync();

        // Map to response with calculated metrics
        var response = athletes.Select(a => new AthleteListResponse
        {
            AthleteId = a.Id,
            Name = a.Name,
            Email = a.Email,
            TotalEntries = a.JournalEntries.Count,
            LastEntryDate = a.JournalEntries.Any()
                ? a.JournalEntries.Max(e => e.EntryDate)
                : null,
            FlaggedEntriesCount = a.JournalEntries.Count(e => e.IsFlagged)
        }).ToList();

        _logger.LogInformation("Retrieved {Count} athletes for coach {CoachId}", response.Count, coachId);

        return response;
    }

    public async Task<AthleteDetailResponse> GetAthleteDetailAsync(int coachId, int athleteId)
    {
        // Get athlete with related data
        var athlete = await _context.Athletes
            .Include(a => a.Coach)
            .Include(a => a.JournalEntries)
            .FirstOrDefaultAsync(a => a.Id == athleteId);

        if (athlete == null)
        {
            throw new InvalidOperationException($"Athlete with ID {athleteId} not found.");
        }

        // Security check: Verify athlete belongs to this coach
        if (athlete.CoachId != coachId)
        {
            _logger.LogWarning("Coach {CoachId} attempted to access athlete {AthleteId} belonging to coach {ActualCoachId}",
                coachId, athleteId, athlete.CoachId);
            throw new UnauthorizedAccessException($"You do not have permission to view this athlete's information.");
        }

        // Map journal entries to response
        var journalEntries = athlete.JournalEntries
            .OrderByDescending(e => e.EntryDate)
            .Select(e => new JournalEntryResponse
            {
                Id = e.Id,
                AthleteId = e.AthleteId,
                AthleteName = athlete.Name,
                EntryDate = e.EntryDate,
                EmotionalState = e.EmotionalState,
                SessionReflection = e.SessionReflection,
                MentalBarriers = e.MentalBarriers,
                IsFlagged = e.IsFlagged,
                CreatedAt = e.CreatedAt
            })
            .ToList();

        var response = new AthleteDetailResponse
        {
            AthleteId = athlete.Id,
            Name = athlete.Name,
            Email = athlete.Email,
            CoachName = athlete.Coach.Name,
            JournalEntries = journalEntries,
            TotalEntries = journalEntries.Count,
            FlaggedEntriesCount = journalEntries.Count(e => e.IsFlagged)
        };

        _logger.LogInformation("Retrieved detail for athlete {AthleteId} by coach {CoachId}", athleteId, coachId);

        return response;
    }

    public async Task<List<JournalEntryResponse>> GetAthleteEntriesAsync(int coachId, int athleteId, bool? flaggedOnly = null)
    {
        // Get athlete to verify ownership
        var athlete = await _context.Athletes
            .FirstOrDefaultAsync(a => a.Id == athleteId);

        if (athlete == null)
        {
            throw new InvalidOperationException($"Athlete with ID {athleteId} not found.");
        }

        // Security check: Verify athlete belongs to this coach
        if (athlete.CoachId != coachId)
        {
            _logger.LogWarning("Coach {CoachId} attempted to access entries for athlete {AthleteId} belonging to coach {ActualCoachId}",
                coachId, athleteId, athlete.CoachId);
            throw new UnauthorizedAccessException($"You do not have permission to view this athlete's journal entries.");
        }

        // Query journal entries
        var query = _context.JournalEntries
            .Include(e => e.Athlete)
            .Where(e => e.AthleteId == athleteId);

        // Apply flagged filter if specified
        if (flaggedOnly.HasValue)
        {
            query = query.Where(e => e.IsFlagged == flaggedOnly.Value);
        }

        // Get entries sorted by date (newest first)
        var entries = await query
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();

        var response = entries.Select(e => new JournalEntryResponse
        {
            Id = e.Id,
            AthleteId = e.AthleteId,
            AthleteName = e.Athlete.Name,
            EntryDate = e.EntryDate,
            EmotionalState = e.EmotionalState,
            SessionReflection = e.SessionReflection,
            MentalBarriers = e.MentalBarriers,
            IsFlagged = e.IsFlagged,
            CreatedAt = e.CreatedAt
        }).ToList();

        _logger.LogInformation("Retrieved {Count} entries for athlete {AthleteId} (flaggedOnly: {FlaggedOnly}) by coach {CoachId}",
            response.Count, athleteId, flaggedOnly, coachId);

        return response;
    }
}
