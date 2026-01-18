using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.Models.Experiments;

public enum ExperimentStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum ExperimentType
{
    Position,
    Persona,
    Compression
}

public class ExperimentRun
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    public double Temperature { get; set; }

    [MaxLength(50)]
    public string PromptVersion { get; set; } = string.Empty;

    public int AthleteId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Persona { get; set; } = string.Empty;

    public ExperimentType ExperimentType { get; set; } = ExperimentType.Persona;

    public int EntriesUsed { get; set; }

    [MaxLength(20)]
    public string EntryOrder { get; set; } = "reverse";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int TokensUsed { get; set; }

    public decimal EstimatedCost { get; set; }

    public ExperimentStatus Status { get; set; } = ExperimentStatus.Pending;

    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Optional batch ID for grouping experiments run together across multiple providers.
    /// </summary>
    [MaxLength(36)]
    public string? BatchId { get; set; }

    // Navigation properties
    public ICollection<ExperimentClaim> Claims { get; set; } = new List<ExperimentClaim>();
    public ICollection<PositionTest> PositionTests { get; set; } = new List<PositionTest>();
}
