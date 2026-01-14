using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// Service for extracting and verifying claims from LLM summary output.
/// </summary>
public interface IClaimExtractorService
{
    /// <summary>
    /// Extracts verifiable claims from LLM summary text and matches them against journal entries.
    /// </summary>
    /// <param name="summaryText">The raw LLM summary text to parse.</param>
    /// <param name="journalEntries">The journal entries that were used as context.</param>
    /// <returns>A list of extracted claims with support evidence.</returns>
    List<ExtractedClaim> ExtractAndVerifyClaims(string summaryText, List<JournalEntry> journalEntries);
}
