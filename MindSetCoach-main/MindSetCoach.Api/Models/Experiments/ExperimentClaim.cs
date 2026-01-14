using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSetCoach.Api.Models.Experiments;

public class ExperimentClaim
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ExperimentRun))]
    public int RunId { get; set; }

    [Required]
    public string ClaimText { get; set; } = string.Empty;

    public bool IsSupported { get; set; }

    [MaxLength(50)]
    public string Persona { get; set; } = string.Empty;

    // Navigation properties
    public ExperimentRun ExperimentRun { get; set; } = null!;
    public ICollection<ClaimReceipt> Receipts { get; set; } = new List<ClaimReceipt>();
}
