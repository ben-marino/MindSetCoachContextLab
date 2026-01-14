using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services;

public class AuthService : IAuthService
{
    private readonly MindSetCoachDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(MindSetCoachDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        _logger.LogInformation("Starting registration for {Email} as {Role}", request.Email, request.Role);

        // Check if email already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");
        }

        // Parse role
        if (!Enum.TryParse<UserRole>(request.Role, out var userRole))
        {
            _logger.LogWarning("Registration failed: Invalid role {Role}", request.Role);
            throw new ArgumentException($"Invalid role: {request.Role}. Must be 'Coach' or 'Athlete'.");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Start transaction for atomic user + profile creation
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create user
            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                Role = userRole,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created with ID: {UserId}", user.Id);

            int? profileId = null;

            // Create profile based on role
            if (userRole == UserRole.Coach)
            {
                _logger.LogInformation("Creating Coach profile for User ID: {UserId}", user.Id);

                var coach = new Coach
                {
                    UserId = user.Id,
                    Name = request.Name,
                    Email = request.Email
                };

                _context.Coaches.Add(coach);
                await _context.SaveChangesAsync();
                profileId = coach.Id;

                _logger.LogInformation("Coach profile created with ID: {CoachId}", coach.Id);

                if (coach.Id == 0)
                {
                    _logger.LogError("ERROR: Coach ID is 0 after SaveChanges! Coach was not saved properly.");
                    throw new InvalidOperationException("Failed to save coach profile. Coach ID is 0.");
                }
            }
            else // Athlete
            {
                _logger.LogInformation("Creating Athlete profile for User ID: {UserId}", user.Id);

                // Find coach if CoachEmail is provided
                Coach? assignedCoach = null;
                if (!string.IsNullOrWhiteSpace(request.CoachEmail))
                {
                    _logger.LogInformation("Looking for coach with email: {CoachEmail}", request.CoachEmail);

                    assignedCoach = await _context.Coaches
                        .Include(c => c.User)
                        .FirstOrDefaultAsync(c => c.Email == request.CoachEmail || c.User.Email == request.CoachEmail);

                    if (assignedCoach == null)
                    {
                        _logger.LogWarning("Coach with email {CoachEmail} not found", request.CoachEmail);
                        throw new InvalidOperationException($"Coach with email '{request.CoachEmail}' not found. Please register with a valid coach email or contact your coach.");
                    }

                    _logger.LogInformation("Found coach: ID={CoachId}, Name={CoachName}", assignedCoach.Id, assignedCoach.Name);
                }
                else
                {
                    _logger.LogWarning("Athlete registration missing coach email");
                    throw new ArgumentException("Athletes must provide a coach email (CoachEmail) during registration.");
                }

                var athlete = new Athlete
                {
                    UserId = user.Id,
                    Name = request.Name,
                    Email = request.Email,
                    CoachId = assignedCoach.Id
                };

                _logger.LogInformation("Adding Athlete to context: UserId={UserId}, CoachId={CoachId}", athlete.UserId, athlete.CoachId);
                _context.Athletes.Add(athlete);

                _logger.LogInformation("Calling SaveChangesAsync for Athlete...");
                await _context.SaveChangesAsync();

                profileId = athlete.Id;
                _logger.LogInformation("Athlete profile created with ID: {AthleteId}", athlete.Id);

                if (athlete.Id == 0)
                {
                    _logger.LogError("ERROR: Athlete ID is 0 after SaveChanges! Athlete was not saved properly.");
                    throw new InvalidOperationException("Failed to save athlete profile. Athlete ID is 0.");
                }
            }

            // Commit transaction
            _logger.LogInformation("Committing transaction for {Email}", request.Email);
            await transaction.CommitAsync();

            // Generate JWT token
            _logger.LogInformation("Generating JWT token for User ID: {UserId}, Profile ID: {ProfileId}", user.Id, profileId);
            var token = GenerateJwtToken(user, profileId);

            // Return auth response
            var response = new AuthResponse
            {
                Token = token,
                Email = user.Email,
                Name = request.Name,
                Role = user.Role.ToString(),
                UserId = user.Id,
                CoachId = userRole == UserRole.Coach ? profileId : null,
                AthleteId = userRole == UserRole.Athlete ? profileId : null
            };

            _logger.LogInformation("Registration successful for {Email} with User ID: {UserId}, Profile ID: {ProfileId}, Role: {Role}",
                request.Email, user.Id, profileId, userRole);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}. Error: {Message}", request.Email, ex.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Find user by email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Verify password
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Load profile based on role
        string name = string.Empty;
        int? profileId = null;

        if (user.Role == UserRole.Coach)
        {
            var coach = await _context.Coaches
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (coach == null)
            {
                throw new InvalidOperationException($"Coach profile not found for user {user.Email}. Please contact support.");
            }

            name = coach.Name;
            profileId = coach.Id;
        }
        else // Athlete
        {
            var athlete = await _context.Athletes
                .FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (athlete == null)
            {
                throw new InvalidOperationException($"Athlete profile not found for user {user.Email}. Please contact support.");
            }

            name = athlete.Name;
            profileId = athlete.Id;
        }

        // Generate JWT token
        var token = GenerateJwtToken(user, profileId);

        // Return auth response
        return new AuthResponse
        {
            Token = token,
            Email = user.Email,
            Name = name,
            Role = user.Role.ToString(),
            UserId = user.Id,
            CoachId = user.Role == UserRole.Coach ? profileId : null,
            AthleteId = user.Role == UserRole.Athlete ? profileId : null
        };
    }

    public string GenerateJwtToken(User user, int? profileId)
    {
        // Read JWT configuration
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is not configured.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "MindSetCoach";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "MindSetCoachUsers";
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add profileId claim if available
        if (profileId.HasValue)
        {
            var profileClaimType = user.Role == UserRole.Coach ? "CoachId" : "AthleteId";
            claims.Add(new Claim(profileClaimType, profileId.Value.ToString()));
        }

        // Create signing credentials
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Create token
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        // Return serialized token
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
