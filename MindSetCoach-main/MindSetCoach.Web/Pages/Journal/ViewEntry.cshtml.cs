using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Journal;

public class ViewEntryModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<ViewEntryModel> _logger;

    public JournalEntryResponse? Entry { get; set; }
    public string? ErrorMessage { get; set; }

    public ViewEntryModel(IApiClient apiClient, ILogger<ViewEntryModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Check if user is authenticated
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Check if user is an athlete
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Athlete")
        {
            TempData["ErrorMessage"] = "Only athletes can view journal entries.";
            return RedirectToPage("/Index");
        }

        // Load entry from API
        var (success, entry, error) = await _apiClient.GetJournalEntryByIdAsync(id, token);

        if (success && entry != null)
        {
            Entry = entry;
        }
        else
        {
            ErrorMessage = error ?? "Failed to load journal entry.";
            _logger.LogError("Failed to load journal entry {Id}: {Error}", id, error);
        }

        return Page();
    }
}
