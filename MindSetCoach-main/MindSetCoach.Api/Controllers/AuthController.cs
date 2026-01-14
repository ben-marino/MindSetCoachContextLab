using Microsoft.AspNetCore.Mvc;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services;

namespace MindSetCoach.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user (Coach or Athlete)
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication response with JWT token</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            _logger.LogInformation("User {Email} registered successfully as {Role}", request.Email, request.Role);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // Email already exists or coach not found
            _logger.LogWarning("Registration failed for {Email}: {Message}", request.Email, ex.Message);

            // Return 404 if coach not found, 400 for other cases
            if (ex.Message.Contains("Coach with email") && ex.Message.Contains("not found"))
            {
                return NotFound(new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Invalid role or missing coach email
            _logger.LogWarning("Registration validation failed for {Email}: {Message}", request.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for {Email}", request.Email);
            return StatusCode(500, new { message = "An unexpected error occurred during registration. Please try again." });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with JWT token</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            _logger.LogInformation("User {Email} logged in successfully", request.Email);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Invalid credentials
            _logger.LogWarning("Login failed for {Email}: {Message}", request.Email, ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Profile not found (data integrity issue)
            _logger.LogError("Login failed for {Email} due to missing profile: {Message}", request.Email, ex.Message);
            return StatusCode(500, new { message = "An error occurred during login. Please contact support." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Email}", request.Email);
            return StatusCode(500, new { message = "An unexpected error occurred during login. Please try again." });
        }
    }
}
