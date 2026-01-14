namespace MindSetCoach.Api.DTOs;

/// <summary>
/// Represents a claim extracted from an LLM summary with evidence matching.
/// </summary>
public class ExtractedClaim
{
    /// <summary>
    /// The text of the claim extracted from the LLM summary.
    /// </summary>
    public string ClaimText { get; set; } = string.Empty;

    /// <summary>
    /// Whether supporting evidence was found in the journal entries.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// The ID of the journal entry that supports this claim (if found).
    /// </summary>
    public int? MatchedEntryId { get; set; }

    /// <summary>
    /// The text snippet from the journal entry that supports the claim.
    /// </summary>
    public string? MatchedSnippet { get; set; }

    /// <summary>
    /// Confidence score for the match (0-1).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The type of claim detected (e.g., "injury", "emotion", "skipped", "event").
    /// </summary>
    public string? ClaimType { get; set; }

    /// <summary>
    /// The date referenced in the claim, if any.
    /// </summary>
    public DateTime? ReferencedDate { get; set; }
}
