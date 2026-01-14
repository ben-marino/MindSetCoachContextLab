using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSetCoach.Api.Models;

public class JournalEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Athlete))]
    public int AthleteId { get; set; }

    [Required]
    public DateTime EntryDate { get; set; }

    [Required]
    public string EmotionalState { get; set; } = string.Empty;

    [Required]
    public string SessionReflection { get; set; } = string.Empty;

    [Required]
    public string MentalBarriers { get; set; } = string.Empty;

    public bool IsFlagged { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Athlete Athlete { get; set; } = null!;
}
