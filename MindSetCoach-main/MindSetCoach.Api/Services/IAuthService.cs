using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    string GenerateJwtToken(User user, int? profileId);
}
