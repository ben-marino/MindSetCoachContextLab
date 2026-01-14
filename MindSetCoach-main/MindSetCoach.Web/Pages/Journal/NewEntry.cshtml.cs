using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Journal;

public class NewEntryModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<NewEntryModel> _logger;

    [BindProperty]
    public JournalEntryRequest Entry { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public NewEntryModel(IApiClient apiClient, ILogger<NewEntryModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        // Check if user is authenticated
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Check if user is an athlete (read from claims, not session)
        var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                       ?? User.FindFirst("role")?.Value;
        if (userRole != "Athlete")
        {
            TempData["ErrorMessage"] = "Only athletes can create journal entries.";
            return RedirectToPage("/Index");
        }

        // Initialize with today's date
        Entry.EntryDate = DateTime.Today;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Check authentication
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Check role (read from claims, not session)
        var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                       ?? User.FindFirst("role")?.Value;
        if (userRole != "Athlete")
        {
            TempData["ErrorMessage"] = "Only athletes can create journal entries.";
            return RedirectToPage("/Index");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Model state is invalid");
            foreach (var validationError in ModelState.Values.SelectMany(v => v.Errors))
            {
                _logger.LogWarning("Validation error: {Error}", validationError.ErrorMessage);
            }
            return Page();
        }

        // DEBUG: Log what we're about to send to the API
        _logger.LogInformation("DEBUG - Submitting journal entry:");
        _logger.LogInformation("  EntryDate: {Date}", Entry.EntryDate);
        _logger.LogInformation("  EmotionalState: '{EmotionalState}' (Length: {Length})",
            Entry.EmotionalState, Entry.EmotionalState?.Length ?? 0);
        _logger.LogInformation("  SessionReflection: '{SessionReflection}' (Length: {Length})",
            Entry.SessionReflection, Entry.SessionReflection?.Length ?? 0);
        _logger.LogInformation("  MentalBarriers: '{MentalBarriers}' (Length: {Length})",
            Entry.MentalBarriers, Entry.MentalBarriers?.Length ?? 0);

        var (success, error) = await _apiClient.CreateJournalEntryAsync(Entry, token);

        if (success)
        {
            var userEmail = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                           ?? User.FindFirst("email")?.Value;
            _logger.LogInformation("Journal entry created for {Email} on {Date}", userEmail, Entry.EntryDate);

            SuccessMessage = "Your journal entry has been saved successfully!";

            // Clear the form for a new entry
            Entry = new JournalEntryRequest
            {
                EntryDate = DateTime.Today
            };

            return Page();
        }

        ErrorMessage = error ?? "Failed to save journal entry. Please try again.";
        return Page();
    }
}
