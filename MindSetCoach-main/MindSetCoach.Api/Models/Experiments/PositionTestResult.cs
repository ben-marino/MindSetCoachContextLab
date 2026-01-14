using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSetCoach.Api.Models.Experiments;

public enum NeedlePosition
{
    Start,
    Middle,
    End
}

/// <summary>
/// Entity for storing position test results (U-curve experiments).
/// Named differently from the DTO in Services.AI to avoid conflicts.
/// </summary>
public class PositionTest
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ExperimentRun))]
    public int RunId { get; set; }

    public NeedlePosition Position { get; set; }

    [Required]
    public string NeedleFact { get; set; } = string.Empty;

    public bool FactRetrieved { get; set; }

    public string ResponseSnippet { get; set; } = string.Empty;

    // Navigation property
    public ExperimentRun ExperimentRun { get; set; } = null!;
}
