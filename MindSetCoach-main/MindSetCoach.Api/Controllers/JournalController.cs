using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MindSetCoach.Api.DTOs;
using MindSetCoach.Api.Services;

namespace MindSetCoach.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JournalController : ControllerBase
{
    private readonly IJournalService _journalService;
    private readonly ILogger<JournalController> _logger;

    public JournalController(IJournalService journalService, ILogger<JournalController> logger)
    {
        _journalService = journalService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new journal entry
    /// </summary>
    /// <param name="request">Journal entry details</param>
    /// <returns>Created journal entry</returns>
    [HttpPost]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JournalEntryResponse>> CreateEntry([FromBody] CreateJournalEntryRequest request)
    {
        try
        {
            // DEBUG: Log what the API received
            _logger.LogInformation("DEBUG - API received journal entry request:");
            _logger.LogInformation("  EntryDate: {Date}", request.EntryDate);
            _logger.LogInformation("  EmotionalState: '{EmotionalState}' (Length: {Length})",
                request.EmotionalState, request.EmotionalState?.Length ?? 0);
            _logger.LogInformation("  SessionReflection: '{SessionReflection}' (Length: {Length})",
                request.SessionReflection, request.SessionReflection?.Length ?? 0);
            _logger.LogInformation("  MentalBarriers: '{MentalBarriers}' (Length: {Length})",
                request.MentalBarriers, request.MentalBarriers?.Length ?? 0);

            // Get athleteId from JWT claims
            var athleteId = GetAthleteIdFromClaims();
            if (athleteId == null)
            {
                _logger.LogWarning("User attempted to create journal entry but athleteId not found in claims");
                return BadRequest(new { message = "Only athletes can create journal entries. AthleteId not found in token." });
            }

            var response = await _journalService.CreateEntryAsync(athleteId.Value, request);
            _logger.LogInformation("Athlete {AthleteId} created journal entry {EntryId}", athleteId, response.Id);

            return CreatedAtAction(nameof(GetEntry), new { entryId = response.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error creating journal entry");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating journal entry");
            return StatusCode(500, new { message = "An unexpected error occurred while creating the journal entry." });
        }
    }

    /// <summary>
    /// Get a single journal entry by ID
    /// </summary>
    /// <param name="entryId">Journal entry ID</param>
    /// <returns>Journal entry details</returns>
    [HttpGet("{entryId}")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JournalEntryResponse>> GetEntry(int entryId)
    {
        try
        {
            // DEBUG: Log all claims to see what we have
            _logger.LogInformation("=== DEBUG: GET /api/journal/{EntryId} - Checking claims ===", entryId);
            foreach (var claim in User.Claims)
            {
                _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            // Check authorization before fetching
            var isAuthorized = await IsAuthorizedToAccessEntry(entryId);
            if (!isAuthorized)
            {
                _logger.LogWarning("User attempted to access journal entry {EntryId} without authorization", entryId);
                return StatusCode(403, new { message = "You are not authorized to access this journal entry." });
            }

            var response = await _journalService.GetEntryAsync(entryId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Journal entry {EntryId} not found", entryId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving journal entry {EntryId}", entryId);
            return StatusCode(500, new { message = "An unexpected error occurred while retrieving the journal entry." });
        }
    }

    /// <summary>
    /// Get all journal entries for a specific athlete
    /// </summary>
    /// <param name="athleteId">Athlete ID</param>
    /// <returns>List of journal entries sorted by date (newest first)</returns>
    [HttpGet("athlete/{athleteId}")]
    [ProducesResponseType(typeof(List<JournalEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<JournalEntryResponse>>> GetAthleteEntries(int athleteId)
    {
        try
        {
            // Check authorization
            var isAuthorized = IsAuthorizedToAccessAthleteEntries(athleteId);
            if (!isAuthorized)
            {
                _logger.LogWarning("User attempted to access entries for athlete {AthleteId} without authorization", athleteId);
                return StatusCode(403, new { message = "You are not authorized to access this athlete's journal entries." });
            }

            var entries = await _journalService.GetAthleteEntriesAsync(athleteId);
            return Ok(entries);
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
    /// Update an existing journal entry
    /// </summary>
    /// <param name="entryId">Journal entry ID</param>
    /// <param name="request">Updated journal entry data</param>
    /// <returns>Updated journal entry</returns>
    [HttpPut("{entryId}")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JournalEntryResponse>> UpdateEntry(int entryId, [FromBody] UpdateJournalEntryRequest request)
    {
        try
        {
            // Check authorization - only athletes can update their own entries
            var role = GetUserRole();
            if (role != "Athlete")
            {
                _logger.LogWarning("{Role} attempted to update journal entry {EntryId}", role, entryId);
                return StatusCode(403, new { message = "Only athletes can update journal entries." });
            }

            var isAuthorized = await IsAuthorizedToAccessEntry(entryId);
            if (!isAuthorized)
            {
                _logger.LogWarning("User attempted to update journal entry {EntryId} without authorization", entryId);
                return StatusCode(403, new { message = "You are not authorized to update this journal entry." });
            }

            var response = await _journalService.UpdateEntryAsync(entryId, request);
            _logger.LogInformation("Updated journal entry {EntryId}", entryId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Journal entry {EntryId} not found for update", entryId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating journal entry {EntryId}", entryId);
            return StatusCode(500, new { message = "An unexpected error occurred while updating the journal entry." });
        }
    }

    /// <summary>
    /// Delete a journal entry
    /// </summary>
    /// <param name="entryId">Journal entry ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{entryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteEntry(int entryId)
    {
        try
        {
            // Check authorization - only athletes can delete their own entries
            var role = GetUserRole();
            if (role != "Athlete")
            {
                _logger.LogWarning("{Role} attempted to delete journal entry {EntryId}", role, entryId);
                return StatusCode(403, new { message = "Only athletes can delete journal entries." });
            }

            var isAuthorized = await IsAuthorizedToAccessEntry(entryId);
            if (!isAuthorized)
            {
                _logger.LogWarning("User attempted to delete journal entry {EntryId} without authorization", entryId);
                return StatusCode(403, new { message = "You are not authorized to delete this journal entry." });
            }

            await _journalService.DeleteEntryAsync(entryId);
            _logger.LogInformation("Deleted journal entry {EntryId}", entryId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Journal entry {EntryId} not found for deletion", entryId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting journal entry {EntryId}", entryId);
            return StatusCode(500, new { message = "An unexpected error occurred while deleting the journal entry." });
        }
    }

    /// <summary>
    /// Flag or unflag a journal entry (coaches only)
    /// </summary>
    /// <param name="entryId">Journal entry ID</param>
    /// <param name="request">Flag status</param>
    /// <returns>Updated flagged status</returns>
    [HttpPut("{entryId}/flag")]
    [Authorize(Roles = "Coach")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> FlagEntry(int entryId, [FromBody] FlagEntryRequest request)
    {
        try
        {
            var flagged = await _journalService.FlagEntryAsync(entryId, request.Flagged);
            _logger.LogInformation("Coach flagged journal entry {EntryId} as {Flagged}", entryId, flagged);
            return Ok(new { entryId, isFlagged = flagged });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Journal entry {EntryId} not found for flagging", entryId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error flagging journal entry {EntryId}", entryId);
            return StatusCode(500, new { message = "An unexpected error occurred while flagging the journal entry." });
        }
    }

    /// <summary>
    /// Get athleteId from JWT claims
    /// </summary>
    private int? GetAthleteIdFromClaims()
    {
        var athleteIdClaim = User.FindFirst("AthleteId");
        _logger.LogInformation("DEBUG - GetAthleteIdFromClaims: Found={Found}, Value={Value}",
            athleteIdClaim != null, athleteIdClaim?.Value ?? "NULL");

        if (athleteIdClaim != null && int.TryParse(athleteIdClaim.Value, out var athleteId))
        {
            return athleteId;
        }
        return null;
    }

    /// <summary>
    /// Get coachId from JWT claims
    /// </summary>
    private int? GetCoachIdFromClaims()
    {
        var coachIdClaim = User.FindFirst("CoachId");
        _logger.LogInformation("DEBUG - GetCoachIdFromClaims: Found={Found}, Value={Value}",
            coachIdClaim != null, coachIdClaim?.Value ?? "NULL");

        if (coachIdClaim != null && int.TryParse(coachIdClaim.Value, out var coachId))
        {
            return coachId;
        }
        return null;
    }

    /// <summary>
    /// Get user role from JWT claims
    /// </summary>
    private string? GetUserRole()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        _logger.LogInformation("DEBUG - GetUserRole: Role={Role}", role ?? "NULL");
        return role;
    }

    /// <summary>
    /// Check if the current user is authorized to access an entry
    /// </summary>
    private async Task<bool> IsAuthorizedToAccessEntry(int entryId)
    {
        var role = GetUserRole();
        _logger.LogInformation("DEBUG - IsAuthorizedToAccessEntry: EntryId={EntryId}, Role={Role}", entryId, role ?? "NULL");

        // Get the entry to check ownership
        JournalEntryResponse entry;
        try
        {
            entry = await _journalService.GetEntryAsync(entryId);
            _logger.LogInformation("DEBUG - Entry found: EntryId={EntryId}, AthleteId={AthleteId}", entry.Id, entry.AthleteId);
        }
        catch (InvalidOperationException)
        {
            // Entry doesn't exist, return false (will result in 404)
            _logger.LogWarning("DEBUG - Entry {EntryId} not found", entryId);
            return false;
        }

        if (role == "Athlete")
        {
            var athleteId = GetAthleteIdFromClaims();
            _logger.LogInformation("DEBUG - Athlete attempting access: AthleteId from claims={AthleteId}, Entry AthleteId={EntryAthleteId}",
                athleteId?.ToString() ?? "NULL", entry.AthleteId);

            if (athleteId == null)
            {
                _logger.LogWarning("DEBUG - AthleteId claim not found for athlete user");
                return false;
            }

            // Athletes can only access their own entries
            var isAuthorized = entry.AthleteId == athleteId.Value;
            _logger.LogInformation("DEBUG - Athlete authorization result: {IsAuthorized}", isAuthorized);
            return isAuthorized;
        }
        else if (role == "Coach")
        {
            var coachId = GetCoachIdFromClaims();
            _logger.LogInformation("DEBUG - Coach attempting access: CoachId from claims={CoachId}",
                coachId?.ToString() ?? "NULL");

            if (coachId == null)
            {
                _logger.LogWarning("DEBUG - CoachId claim not found for coach user");
                return false;
            }

            // Coaches can access entries from their assigned athletes
            // Note: This requires getting the athlete's CoachId from the database
            // For now, we'll allow all coaches to access (TODO: Implement proper check)
            _logger.LogInformation("DEBUG - Coach authorization: Allowing access (TODO: check athlete assignment)");
            return true;
        }

        _logger.LogWarning("DEBUG - Unknown role or no role: {Role}", role ?? "NULL");
        return false;
    }

    /// <summary>
    /// Check if the current user is authorized to access an athlete's entries
    /// </summary>
    private bool IsAuthorizedToAccessAthleteEntries(int athleteId)
    {
        var role = GetUserRole();

        if (role == "Athlete")
        {
            var userAthleteId = GetAthleteIdFromClaims();
            if (userAthleteId == null)
            {
                return false;
            }
            // Athletes can only access their own entries
            return athleteId == userAthleteId.Value;
        }
        else if (role == "Coach")
        {
            // Coaches can access entries from their assigned athletes
            // TODO: Add check to verify athlete is assigned to this coach
            return true;
        }

        return false;
    }
}

/// <summary>
/// Request to flag/unflag a journal entry
/// </summary>
public class FlagEntryRequest
{
    public bool Flagged { get; set; }
}
