namespace MindSetCoach.Api.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? CoachId { get; set; }
    public int? AthleteId { get; set; }
}
