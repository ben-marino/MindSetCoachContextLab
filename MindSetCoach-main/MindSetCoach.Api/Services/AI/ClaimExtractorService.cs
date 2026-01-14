using System.Text.RegularExpressions;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services.AI;

/// <summary>
/// Service for extracting and verifying claims from LLM summary output.
/// Uses regex patterns and fuzzy text matching to identify and verify claims.
/// </summary>
public class ClaimExtractorService : IClaimExtractorService
{
    private readonly ILogger<ClaimExtractorService> _logger;

    // Claim type patterns
    private static readonly (string Type, Regex Pattern)[] ClaimPatterns = new[]
    {
        // Injury/physical claims: "you reported shin splints", "you mentioned pain", "you had an injury"
        ("injury", new Regex(
            @"(?:you\s+)?(?:reported|mentioned|noted|had|experienced|described|said\s+about)\s+(?:having\s+)?(?:an?\s+)?([a-z\s]+(?:shin splints?|pain|injury|soreness|strain|ache|hurt|sprain|cramp|stiffness|fatigue|tiredness|exhaustion))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Emotion/mood claims: "your confidence improved", "you felt anxious", "your mood was"
        ("emotion", new Regex(
            @"(?:your\s+)?(?:confidence|mood|motivation|energy|anxiety|stress|focus|mindset|attitude|mental\s+state)\s+(?:was|improved|decreased|increased|dropped|grew|felt|seemed|appeared|became)(?:\s+(?:better|worse|stronger|weaker|higher|lower|more|less))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Alternative emotion pattern: "you felt confident/anxious/etc"
        ("emotion", new Regex(
            @"you\s+(?:felt|were|seemed|appeared|reported\s+feeling|mentioned\s+feeling)\s+(?:more\s+|less\s+)?(?:confident|anxious|stressed|motivated|energized|tired|exhausted|focused|distracted|positive|negative|happy|sad|frustrated|excited|nervous|calm|relaxed)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Skipped/missed session claims: "you skipped", "you missed", "you didn't train"
        ("skipped", new Regex(
            @"you\s+(?:skipped|missed|didn't\s+(?:train|practice|workout|exercise|show\s+up)|took\s+(?:a\s+)?(?:day|time)\s+off|rested|had\s+a\s+rest\s+day)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Event/activity claims: "you completed", "you achieved", "you did"
        ("event", new Regex(
            @"you\s+(?:completed|achieved|accomplished|did|performed|finished|started|began|tried|attempted|worked\s+on|focused\s+on|practiced)\s+(?:your\s+)?([a-z\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Barrier/obstacle claims: "you struggled with", "you faced", "your barrier was"
        ("barrier", new Regex(
            @"(?:you\s+)?(?:struggled\s+with|faced|encountered|dealt\s+with|had\s+(?:difficulty|trouble|issues?)\s+with|your\s+(?:main\s+)?barrier\s+(?:was|is))\s+([a-z\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Progress/improvement claims: "you made progress", "you improved"
        ("progress", new Regex(
            @"you\s+(?:made\s+progress|improved|got\s+better|advanced|developed|grew|showed\s+improvement)\s*(?:in|on|with|at)?\s*([a-z\s]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    // Day name pattern for extracting temporal references
    private static readonly Regex DayNamePattern = new(
        @"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern to extract sentences/phrases that look like claims
    private static readonly Regex SentencePattern = new(
        @"[^.!?\n]*(?:you|your)[^.!?\n]*[.!?]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Keywords that indicate positive emotional states
    private static readonly HashSet<string> PositiveEmotionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "confident", "motivated", "energized", "focused", "positive", "happy", "excited",
        "calm", "relaxed", "strong", "powerful", "determined", "optimistic", "proud",
        "accomplished", "satisfied", "hopeful", "enthusiastic", "inspired", "improved",
        "better", "good", "great", "excellent", "amazing", "fantastic"
    };

    // Keywords that indicate negative emotional states
    private static readonly HashSet<string> NegativeEmotionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "anxious", "stressed", "tired", "exhausted", "frustrated", "sad", "nervous",
        "worried", "distracted", "negative", "weak", "unmotivated", "discouraged",
        "overwhelmed", "drained", "defeated", "disappointed", "afraid", "uncertain",
        "worse", "bad", "terrible", "struggling", "difficult"
    };

    // Keywords for skipped/missed sessions
    private static readonly HashSet<string> SkippedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "skipped", "missed", "didn't", "did not", "couldn't", "could not", "rest day",
        "took off", "day off", "rested", "no training", "no practice", "no workout"
    };

    public ClaimExtractorService(ILogger<ClaimExtractorService> logger)
    {
        _logger = logger;
    }

    public List<ExtractedClaim> ExtractAndVerifyClaims(string summaryText, List<JournalEntry> journalEntries)
    {
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            _logger.LogWarning("Empty summary text provided to claim extractor");
            return new List<ExtractedClaim>();
        }

        _logger.LogInformation("Extracting claims from summary ({Length} chars) against {EntryCount} journal entries",
            summaryText.Length, journalEntries.Count);

        var claims = new List<ExtractedClaim>();

        // Extract potential claim sentences
        var sentences = ExtractClaimSentences(summaryText);
        _logger.LogDebug("Found {Count} potential claim sentences", sentences.Count);

        foreach (var sentence in sentences)
        {
            var claim = AnalyzeSentence(sentence, journalEntries);
            if (claim != null)
            {
                claims.Add(claim);
            }
        }

        // Deduplicate similar claims
        claims = DeduplicateClaims(claims);

        _logger.LogInformation("Extracted {Count} verified claims ({Supported} supported, {Unsupported} unsupported)",
            claims.Count,
            claims.Count(c => c.IsSupported),
            claims.Count(c => !c.IsSupported));

        return claims;
    }

    private List<string> ExtractClaimSentences(string text)
    {
        var sentences = new List<string>();
        var matches = SentencePattern.Matches(text);

        foreach (Match match in matches)
        {
            var sentence = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(sentence) && sentence.Length > 10)
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    private ExtractedClaim? AnalyzeSentence(string sentence, List<JournalEntry> entries)
    {
        // Try to match against known claim patterns
        foreach (var (claimType, pattern) in ClaimPatterns)
        {
            var match = pattern.Match(sentence);
            if (match.Success)
            {
                var claim = new ExtractedClaim
                {
                    ClaimText = sentence,
                    ClaimType = claimType,
                    ReferencedDate = ExtractReferencedDate(sentence, entries)
                };

                // Find supporting evidence
                var evidence = FindSupportingEvidence(sentence, claimType, entries);
                if (evidence.HasValue)
                {
                    claim.IsSupported = true;
                    claim.MatchedEntryId = evidence.Value.EntryId;
                    claim.MatchedSnippet = evidence.Value.Snippet;
                    claim.Confidence = evidence.Value.Confidence;
                }
                else
                {
                    claim.IsSupported = false;
                    claim.Confidence = 0;
                }

                return claim;
            }
        }

        return null;
    }

    private DateTime? ExtractReferencedDate(string sentence, List<JournalEntry> entries)
    {
        var dayMatch = DayNamePattern.Match(sentence);
        if (!dayMatch.Success || entries.Count == 0)
        {
            return null;
        }

        var dayName = dayMatch.Value.ToLower();
        var targetDayOfWeek = dayName switch
        {
            "monday" => DayOfWeek.Monday,
            "tuesday" => DayOfWeek.Tuesday,
            "wednesday" => DayOfWeek.Wednesday,
            "thursday" => DayOfWeek.Thursday,
            "friday" => DayOfWeek.Friday,
            "saturday" => DayOfWeek.Saturday,
            "sunday" => DayOfWeek.Sunday,
            _ => (DayOfWeek?)null
        };

        if (!targetDayOfWeek.HasValue)
        {
            return null;
        }

        // Find the most recent entry matching that day of week
        var matchingEntry = entries
            .Where(e => e.EntryDate.DayOfWeek == targetDayOfWeek.Value)
            .OrderByDescending(e => e.EntryDate)
            .FirstOrDefault();

        return matchingEntry?.EntryDate;
    }

    private (int EntryId, string Snippet, double Confidence)? FindSupportingEvidence(
        string claim,
        string claimType,
        List<JournalEntry> entries)
    {
        var bestMatch = (EntryId: 0, Snippet: string.Empty, Confidence: 0.0);
        var claimLower = claim.ToLower();

        // Extract key terms from the claim
        var keyTerms = ExtractKeyTerms(claim);

        foreach (var entry in entries)
        {
            // Check each field of the journal entry
            var fieldsToSearch = new[]
            {
                (entry.EmotionalState, "EmotionalState"),
                (entry.SessionReflection, "SessionReflection"),
                (entry.MentalBarriers, "MentalBarriers")
            };

            foreach (var (fieldValue, fieldName) in fieldsToSearch)
            {
                if (string.IsNullOrWhiteSpace(fieldValue))
                    continue;

                var matchResult = CalculateMatchScore(claimLower, claimType, keyTerms, fieldValue, entry.EntryDate);

                if (matchResult.Score > bestMatch.Confidence)
                {
                    bestMatch = (entry.Id, matchResult.Snippet, matchResult.Score);
                }
            }
        }

        // Return null if confidence is too low
        if (bestMatch.Confidence < 0.3)
        {
            return null;
        }

        return bestMatch;
    }

    private (double Score, string Snippet) CalculateMatchScore(
        string claimLower,
        string claimType,
        List<string> keyTerms,
        string entryText,
        DateTime entryDate)
    {
        var entryLower = entryText.ToLower();
        var score = 0.0;
        var matchedSnippet = string.Empty;

        // 1. Direct keyword matching (weighted by claim type)
        var matchedTerms = 0;
        foreach (var term in keyTerms)
        {
            if (entryLower.Contains(term))
            {
                matchedTerms++;

                // Extract snippet around the matched term
                var termIndex = entryLower.IndexOf(term);
                var snippetStart = Math.Max(0, termIndex - 30);
                var snippetEnd = Math.Min(entryText.Length, termIndex + term.Length + 30);
                var currentSnippet = entryText.Substring(snippetStart, snippetEnd - snippetStart).Trim();

                if (currentSnippet.Length > matchedSnippet.Length)
                {
                    matchedSnippet = "..." + currentSnippet + "...";
                }
            }
        }

        if (keyTerms.Count > 0)
        {
            score += (double)matchedTerms / keyTerms.Count * 0.5;
        }

        // 2. Claim type-specific matching
        switch (claimType)
        {
            case "emotion":
                score += CalculateEmotionMatchScore(claimLower, entryLower);
                if (string.IsNullOrEmpty(matchedSnippet) && score > 0.3)
                {
                    matchedSnippet = TruncateSnippet(entryText);
                }
                break;

            case "skipped":
                score += CalculateSkippedMatchScore(entryLower);
                if (string.IsNullOrEmpty(matchedSnippet) && score > 0.3)
                {
                    matchedSnippet = TruncateSnippet(entryText);
                }
                break;

            case "injury":
                score += CalculateInjuryMatchScore(claimLower, entryLower);
                break;

            case "barrier":
                score += CalculateBarrierMatchScore(claimLower, entryLower);
                break;
        }

        // 3. Fuzzy string similarity for overall content
        var similarityScore = CalculateFuzzySimilarity(claimLower, entryLower);
        score += similarityScore * 0.2;

        // 4. Day of week matching bonus
        var dayMatch = DayNamePattern.Match(claimLower);
        if (dayMatch.Success)
        {
            var claimDay = ParseDayOfWeek(dayMatch.Value);
            if (claimDay.HasValue && entryDate.DayOfWeek == claimDay.Value)
            {
                score += 0.15;
            }
        }

        // Normalize score to 0-1 range
        score = Math.Min(1.0, Math.Max(0.0, score));

        return (score, matchedSnippet);
    }

    private double CalculateEmotionMatchScore(string claim, string entryText)
    {
        var score = 0.0;

        // Check if claim mentions positive improvement
        var claimIsPositive = PositiveEmotionKeywords.Any(k => claim.Contains(k));
        var claimIsNegative = NegativeEmotionKeywords.Any(k => claim.Contains(k));

        // Check entry for matching sentiment
        var entryPositiveCount = PositiveEmotionKeywords.Count(k => entryText.Contains(k));
        var entryNegativeCount = NegativeEmotionKeywords.Count(k => entryText.Contains(k));

        if (claimIsPositive && entryPositiveCount > entryNegativeCount)
        {
            score += 0.3 + (entryPositiveCount * 0.05);
        }
        else if (claimIsNegative && entryNegativeCount > entryPositiveCount)
        {
            score += 0.3 + (entryNegativeCount * 0.05);
        }

        return Math.Min(0.5, score);
    }

    private double CalculateSkippedMatchScore(string entryText)
    {
        var matchedKeywords = SkippedKeywords.Count(k => entryText.Contains(k));
        return Math.Min(0.5, matchedKeywords * 0.15);
    }

    private double CalculateInjuryMatchScore(string claim, string entryText)
    {
        var injuryTerms = new[] { "shin splint", "pain", "injury", "sore", "strain", "ache", "hurt", "sprain", "cramp", "stiff", "fatigue" };
        var matchCount = injuryTerms.Count(t => claim.Contains(t) && entryText.Contains(t));
        return Math.Min(0.5, matchCount * 0.2);
    }

    private double CalculateBarrierMatchScore(string claim, string entryText)
    {
        var barrierTerms = new[] { "struggle", "difficult", "challenge", "obstacle", "barrier", "block", "issue", "problem", "hard" };
        var matchCount = barrierTerms.Count(t => claim.Contains(t) && entryText.Contains(t));
        return Math.Min(0.4, matchCount * 0.15);
    }

    private List<string> ExtractKeyTerms(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "you", "your", "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "was", "were", "is", "are", "been", "being", "have",
            "has", "had", "do", "does", "did", "will", "would", "could", "should", "may",
            "might", "must", "shall", "that", "this", "these", "those", "it", "its"
        };

        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();

        return words;
    }

    private double CalculateFuzzySimilarity(string text1, string text2)
    {
        // Simple Jaccard similarity using word sets
        var words1 = new HashSet<string>(Regex.Split(text1, @"\W+").Where(w => w.Length > 2));
        var words2 = new HashSet<string>(Regex.Split(text2, @"\W+").Where(w => w.Length > 2));

        if (words1.Count == 0 || words2.Count == 0)
            return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }

    private DayOfWeek? ParseDayOfWeek(string dayName)
    {
        return dayName.ToLower() switch
        {
            "monday" => DayOfWeek.Monday,
            "tuesday" => DayOfWeek.Tuesday,
            "wednesday" => DayOfWeek.Wednesday,
            "thursday" => DayOfWeek.Thursday,
            "friday" => DayOfWeek.Friday,
            "saturday" => DayOfWeek.Saturday,
            "sunday" => DayOfWeek.Sunday,
            _ => null
        };
    }

    private string TruncateSnippet(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private List<ExtractedClaim> DeduplicateClaims(List<ExtractedClaim> claims)
    {
        var uniqueClaims = new List<ExtractedClaim>();

        foreach (var claim in claims)
        {
            // Check if we already have a very similar claim
            var isDuplicate = uniqueClaims.Any(existing =>
                CalculateFuzzySimilarity(existing.ClaimText.ToLower(), claim.ClaimText.ToLower()) > 0.8);

            if (!isDuplicate)
            {
                uniqueClaims.Add(claim);
            }
            else
            {
                // If duplicate but higher confidence, replace
                var existingIndex = uniqueClaims.FindIndex(existing =>
                    CalculateFuzzySimilarity(existing.ClaimText.ToLower(), claim.ClaimText.ToLower()) > 0.8);

                if (existingIndex >= 0 && claim.Confidence > uniqueClaims[existingIndex].Confidence)
                {
                    uniqueClaims[existingIndex] = claim;
                }
            }
        }

        return uniqueClaims;
    }
}
