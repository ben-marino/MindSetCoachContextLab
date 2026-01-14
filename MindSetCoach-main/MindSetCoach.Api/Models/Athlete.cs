using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSetCoach.Api.Models;

public class Athlete
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(Coach))]
    public int CoachId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Coach Coach { get; set; } = null!;
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}
