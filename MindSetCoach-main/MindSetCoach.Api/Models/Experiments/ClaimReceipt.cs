using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSetCoach.Api.Models.Experiments;

public class ClaimReceipt
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ExperimentClaim))]
    public int ClaimId { get; set; }

    public int JournalEntryId { get; set; }

    [Required]
    public string MatchedSnippet { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }

    public double Confidence { get; set; }

    // Navigation property
    public ExperimentClaim ExperimentClaim { get; set; } = null!;
}
