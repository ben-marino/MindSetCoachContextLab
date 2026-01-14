using System.ComponentModel.DataAnnotations;

namespace MindSetCoach.Api.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    [RegularExpression("^(Coach|Athlete)$", ErrorMessage = "Role must be either 'Coach' or 'Athlete'")]
    public string Role { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid coach email format")]
    public string? CoachEmail { get; set; }
}
