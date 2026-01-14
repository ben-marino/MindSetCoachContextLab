using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Coach;

[Authorize(Roles = "Coach")]
public class AthleteDetailModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<AthleteDetailModel> _logger;

    public AthleteDetailResponse? AthleteDetail { get; set; }
    public string? ErrorMessage { get; set; }

    public AthleteDetailModel(IApiClient apiClient, ILogger<AthleteDetailModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int athleteId)
    {
        // Get token from session
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No token found in session, redirecting to login");
            return RedirectToPage("/Auth/Login");
        }

        // Get coach email for logging
        var coachEmail = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                        ?? User.FindFirst("email")?.Value;

        _logger.LogInformation("Coach {Email} accessing detail for athlete {AthleteId}", coachEmail, athleteId);

        // Fetch athlete details from API
        var (success, athleteDetail, error) = await _apiClient.GetAthleteDetailAsync(athleteId, token);

        if (success && athleteDetail != null)
        {
            AthleteDetail = athleteDetail;
            _logger.LogInformation("Loaded details for athlete {AthleteId} with {EntryCount} entries",
                athleteId, athleteDetail.JournalEntries.Count);
        }
        else
        {
            ErrorMessage = error ?? "Failed to load athlete details";
            _logger.LogWarning("Failed to load athlete {AthleteId} for coach {Email}: {Error}",
                athleteId, coachEmail, ErrorMessage);

            // If it's a 403/404, redirect back to dashboard
            if (error?.Contains("not found") == true || error?.Contains("not assigned") == true)
            {
                TempData["ErrorMessage"] = ErrorMessage;
                return RedirectToPage("/Coach/Dashboard");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostFlagEntryAsync(int entryId, int athleteId)
    {
        // Get token from session
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No token found in session, redirecting to login");
            return RedirectToPage("/Auth/Login");
        }

        // Call API to flag entry
        var (success, error) = await _apiClient.FlagJournalEntryAsync(entryId, true, token);

        if (success)
        {
            _logger.LogInformation("Successfully flagged entry {EntryId}", entryId);
        }
        else
        {
            _logger.LogWarning("Failed to flag entry {EntryId}: {Error}", entryId, error);
            TempData["ErrorMessage"] = error ?? "Failed to flag entry";
        }

        // Redirect back to same page to refresh
        return RedirectToPage(new { athleteId });
    }

    public async Task<IActionResult> OnPostUnflagEntryAsync(int entryId, int athleteId)
    {
        // Get token from session
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No token found in session, redirecting to login");
            return RedirectToPage("/Auth/Login");
        }

        // Call API to unflag entry
        var (success, error) = await _apiClient.FlagJournalEntryAsync(entryId, false, token);

        if (success)
        {
            _logger.LogInformation("Successfully unflagged entry {EntryId}", entryId);
        }
        else
        {
            _logger.LogWarning("Failed to unflag entry {EntryId}: {Error}", entryId, error);
            TempData["ErrorMessage"] = error ?? "Failed to unflag entry";
        }

        // Redirect back to same page to refresh
        return RedirectToPage(new { athleteId });
    }
}
