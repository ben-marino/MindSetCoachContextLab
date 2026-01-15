using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.Models.Experiments;

public class ExperimentPreset
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON configuration containing experiment settings.
    /// Stored as JSON string for flexibility.
    /// </summary>
    [Required]
    public string Config { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True for system-provided default presets, false for user-created.
    /// </summary>
    public bool IsDefault { get; set; } = false;
}
