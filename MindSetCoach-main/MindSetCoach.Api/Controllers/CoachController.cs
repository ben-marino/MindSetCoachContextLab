using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services;

namespace MindSetCoach.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Coach")]
public class CoachController : ControllerBase
{
    private readonly ICoachService _coachService;
    private readonly ILogger<CoachController> _logger;

    public CoachController(ICoachService coachService, ILogger<CoachController> logger)
    {
        _coachService = coachService;
        _logger = logger;
    }

    /// <summary>
    /// Get all athletes assigned to this coach
    /// </summary>
    /// <returns>List of athletes with summary metrics</returns>
    [HttpGet("athletes")]
    [ProducesResponseType(typeof(List<AthleteListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AthleteListResponse>>> GetMyAthletes()
    {
        try
        {
            var coachId = GetCoachIdFromClaims();
            if (coachId == null)
            {
                _logger.LogWarning("User attempted to access coach endpoint but coachId not found in claims");
                return BadRequest(new { message = "CoachId not found in token. Please ensure you are logged in as a coach." });
            }

            var athletes = await _coachService.GetMyAthletesAsync(coachId.Value);
            _logger.LogInformation("Coach {CoachId} retrieved {Count} athletes", coachId, athletes.Count);

            return Ok(athletes);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error retrieving athletes");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving athletes");
            return StatusCode(500, new { message = "An unexpected error occurred while retrieving athletes." });
        }
    }

    /// <summary>
    /// Get detailed information about a specific athlete including all journal entries
    /// </summary>
    /// <param name="athleteId">Athlete ID</param>
    /// <returns>Detailed athlete profile with journal entries</returns>
    [HttpGet("athletes/{athleteId}")]
    [ProducesResponseType(typeof(AthleteDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AthleteDetailResponse>> GetAthleteDetail(int athleteId)
    {
        try
        {
            var coachId = GetCoachIdFromClaims();
            if (coachId == null)
            {
                _logger.LogWarning("User attempted to access coach endpoint but coachId not found in claims");
                return BadRequest(new { message = "CoachId not found in token. Please ensure you are logged in as a coach." });
            }

            var athleteDetail = await _coachService.GetAthleteDetailAsync(coachId.Value, athleteId);
            _logger.LogInformation("Coach {CoachId} retrieved detail for athlete {AthleteId}", coachId, athleteId);

            return Ok(athleteDetail);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Athlete {AthleteId} not found", athleteId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving athlete detail for athlete {AthleteId}", athleteId);
            return StatusCode(500, new { message = "An unexpected error occurred while retrieving athlete details." });
        }
    }

    /// <summary>
    /// Get journal entries for a specific athlete with optional filtering
    /// </summary>
    /// <param name="athleteId">Athlete ID</param>
    /// <param name="flaggedOnly">Optional: filter to show only flagged entries (true) or non-flagged entries (false)</param>
    /// <returns>List of journal entries</returns>
    [HttpGet("athletes/{athleteId}/entries")]
    [ProducesResponseType(typeof(List<JournalEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<JournalEntryResponse>>> GetAthleteEntries(int athleteId, [FromQuery] bool? flaggedOnly = null)
    {
        try
        {
            var coachId = GetCoachIdFromClaims();
            if (coachId == null)
            {
                _logger.LogWarning("User attempted to access coach endpoint but coachId not found in claims");
                return BadRequest(new { message = "CoachId not found in token. Please ensure you are logged in as a coach." });
            }

            var entries = await _coachService.GetAthleteEntriesAsync(coachId.Value, athleteId, flaggedOnly);
            _logger.LogInformation("Coach {CoachId} retrieved {Count} entries for athlete {AthleteId} (flaggedOnly: {FlaggedOnly})",
                coachId, entries.Count, athleteId, flaggedOnly);

            return Ok(entries);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Athlete {AthleteId} not found", athleteId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving entries for athlete {AthleteId}", athleteId);
            return StatusCode(500, new { message = "An unexpected error occurred while retrieving journal entries." });
        }
    }

    /// <summary>
    /// Get coachId from JWT claims
    /// </summary>
    private int? GetCoachIdFromClaims()
    {
        var coachIdClaim = User.FindFirst("CoachId");
        if (coachIdClaim != null && int.TryParse(coachIdClaim.Value, out var coachId))
        {
            return coachId;
        }
        return null;
    }
}
