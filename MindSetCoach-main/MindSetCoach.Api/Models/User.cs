using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.Models;

public enum UserRole
{
    Athlete,
    Coach
}

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Coach? Coach { get; set; }
    public Athlete? Athlete { get; set; }
}
