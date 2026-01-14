using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Journal;

public class EntriesModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<EntriesModel> _logger;

    public List<JournalEntryResponse> Entries { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public EntriesModel(IApiClient apiClient, ILogger<EntriesModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user is authenticated
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Check if user is an athlete
        var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                       ?? User.FindFirst("role")?.Value;
        if (userRole != "Athlete")
        {
            TempData["ErrorMessage"] = "Only athletes can view journal entries.";
            return RedirectToPage("/Index");
        }

        // Get AthleteId from JWT claims (NOT from session!)
        var athleteIdClaim = User.FindFirst("AthleteId")?.Value;
        if (string.IsNullOrEmpty(athleteIdClaim) || !int.TryParse(athleteIdClaim, out int athleteId))
        {
            _logger.LogError("AthleteId claim not found or invalid in JWT token");
            ErrorMessage = "Authentication error: AthleteId not found in token. Please log in again.";
            return RedirectToPage("/Auth/Login");
        }

        // Load entries from API
        var (success, entries, error) = await _apiClient.GetJournalEntriesAsync(athleteId, token);

        if (success && entries != null)
        {
            // Sort by date descending (newest first)
            Entries = entries.OrderByDescending(e => e.EntryDate).ToList();
        }
        else
        {
            ErrorMessage = error;
            _logger.LogError("Failed to load journal entries: {Error}", error);
        }

        return Page();
    }
}
