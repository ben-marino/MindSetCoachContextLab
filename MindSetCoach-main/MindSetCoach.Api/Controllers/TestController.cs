using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;

namespace MindSetCoach.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly MindSetCoachDbContext _context;
    private readonly ILogger<TestController> _logger;

    public TestController(MindSetCoachDbContext context, ILogger<TestController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// DEBUG: Get athlete by ID with coach information
    /// </summary>
    [HttpGet("athlete/{athleteId}")]
    public async Task<ActionResult> GetAthleteWithCoach(int athleteId)
    {
        var athlete = await _context.Athletes
            .Include(a => a.User)
            .Include(a => a.Coach)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(a => a.Id == athleteId);

        if (athlete == null)
        {
            return NotFound(new { message = $"Athlete with ID {athleteId} not found" });
        }

        var result = new
        {
            athleteId = athlete.Id,
            athleteName = athlete.Name,
            athleteEmail = athlete.Email,
            athleteUserId = athlete.UserId,
            coachId = athlete.CoachId,
            coach = athlete.Coach == null ? null : new
            {
                coachId = athlete.Coach.Id,
                coachName = athlete.Coach.Name,
                coachEmail = athlete.Coach.Email,
                coachUserId = athlete.Coach.UserId
            }
        };

        _logger.LogInformation("Retrieved athlete {AthleteId} with CoachId={CoachId}", athleteId, athlete.CoachId);

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all athletes with their coaches
    /// </summary>
    [HttpGet("athletes")]
    public async Task<ActionResult> GetAllAthletes()
    {
        var athletes = await _context.Athletes
            .Include(a => a.User)
            .Include(a => a.Coach)
            .ToListAsync();

        var result = athletes.Select(a => new
        {
            athleteId = a.Id,
            athleteName = a.Name,
            athleteEmail = a.Email,
            coachId = a.CoachId,
            coachName = a.Coach?.Name,
            coachEmail = a.Coach?.Email
        });

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all coaches
    /// </summary>
    [HttpGet("coaches")]
    public async Task<ActionResult> GetAllCoaches()
    {
        var coaches = await _context.Coaches
            .Include(c => c.User)
            .ToListAsync();

        var result = coaches.Select(c => new
        {
            coachId = c.Id,
            coachName = c.Name,
            coachEmail = c.Email,
            userId = c.UserId
        });

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all users (without password hashes)
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult> GetAllUsers()
    {
        var users = await _context.Users.ToListAsync();

        var result = users.Select(u => new
        {
            userId = u.Id,
            email = u.Email,
            role = u.Role.ToString(),
            createdAt = u.CreatedAt,
            passwordHashPreview = u.PasswordHash.Substring(0, Math.Min(20, u.PasswordHash.Length)) + "..." // First 20 chars only for debugging
        });

        return Ok(result);
    }
}
