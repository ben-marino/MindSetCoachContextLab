using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services;

public class JournalService : IJournalService
{
    private readonly MindSetCoachDbContext _context;
    private readonly ILogger<JournalService> _logger;

    public JournalService(MindSetCoachDbContext context, ILogger<JournalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<JournalEntryResponse> CreateEntryAsync(int athleteId, CreateJournalEntryRequest request)
    {
        // Verify athlete exists
        var athlete = await _context.Athletes
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == athleteId);

        if (athlete == null)
        {
            throw new InvalidOperationException($"Athlete with ID {athleteId} not found.");
        }

        // Create journal entry
        var entry = new JournalEntry
        {
            AthleteId = athleteId,
            EntryDate = DateTime.SpecifyKind(request.EntryDate, DateTimeKind.Utc),
            EmotionalState = request.EmotionalState,
            SessionReflection = request.SessionReflection,
            MentalBarriers = request.MentalBarriers,
            IsFlagged = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created journal entry {EntryId} for athlete {AthleteId}", entry.Id, athleteId);

        // Return response with athlete name
        return new JournalEntryResponse
        {
            Id = entry.Id,
            AthleteId = entry.AthleteId,
            AthleteName = athlete.Name,
            EntryDate = entry.EntryDate,
            EmotionalState = entry.EmotionalState,
            SessionReflection = entry.SessionReflection,
            MentalBarriers = entry.MentalBarriers,
            IsFlagged = entry.IsFlagged,
            CreatedAt = entry.CreatedAt
        };
    }

    public async Task<JournalEntryResponse> GetEntryAsync(int entryId)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Athlete)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry == null)
        {
            throw new InvalidOperationException($"Journal entry with ID {entryId} not found.");
        }

        return new JournalEntryResponse
        {
            Id = entry.Id,
            AthleteId = entry.AthleteId,
            AthleteName = entry.Athlete.Name,
            EntryDate = entry.EntryDate,
            EmotionalState = entry.EmotionalState,
            SessionReflection = entry.SessionReflection,
            MentalBarriers = entry.MentalBarriers,
            IsFlagged = entry.IsFlagged,
            CreatedAt = entry.CreatedAt
        };
    }

    public async Task<List<JournalEntryResponse>> GetAthleteEntriesAsync(int athleteId)
    {
        // Verify athlete exists
        var athleteExists = await _context.Athletes.AnyAsync(a => a.Id == athleteId);
        if (!athleteExists)
        {
            throw new InvalidOperationException($"Athlete with ID {athleteId} not found.");
        }

        var entries = await _context.JournalEntries
            .Include(e => e.Athlete)
            .Where(e => e.AthleteId == athleteId)
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();

        return entries.Select(e => new JournalEntryResponse
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
    }

    public async Task<JournalEntryResponse> UpdateEntryAsync(int entryId, UpdateJournalEntryRequest request)
    {
        var entry = await _context.JournalEntries
            .Include(e => e.Athlete)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry == null)
        {
            throw new InvalidOperationException($"Journal entry with ID {entryId} not found.");
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.EmotionalState))
        {
            entry.EmotionalState = request.EmotionalState;
        }

        if (!string.IsNullOrWhiteSpace(request.SessionReflection))
        {
            entry.SessionReflection = request.SessionReflection;
        }

        if (!string.IsNullOrWhiteSpace(request.MentalBarriers))
        {
            entry.MentalBarriers = request.MentalBarriers;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated journal entry {EntryId} for athlete {AthleteId}", entryId, entry.AthleteId);

        return new JournalEntryResponse
        {
            Id = entry.Id,
            AthleteId = entry.AthleteId,
            AthleteName = entry.Athlete.Name,
            EntryDate = entry.EntryDate,
            EmotionalState = entry.EmotionalState,
            SessionReflection = entry.SessionReflection,
            MentalBarriers = entry.MentalBarriers,
            IsFlagged = entry.IsFlagged,
            CreatedAt = entry.CreatedAt
        };
    }

    public async Task<bool> FlagEntryAsync(int entryId, bool flagged)
    {
        var entry = await _context.JournalEntries
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry == null)
        {
            throw new InvalidOperationException($"Journal entry with ID {entryId} not found.");
        }

        entry.IsFlagged = flagged;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Flagged journal entry {EntryId} as {Flagged}", entryId, flagged);

        return entry.IsFlagged;
    }

    public async Task DeleteEntryAsync(int entryId)
    {
        var entry = await _context.JournalEntries
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry == null)
        {
            throw new InvalidOperationException($"Journal entry with ID {entryId} not found.");
        }

        _context.JournalEntries.Remove(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted journal entry {EntryId} for athlete {AthleteId}", entryId, entry.AthleteId);
    }
}
