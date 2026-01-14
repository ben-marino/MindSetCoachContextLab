using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Coach;

[Authorize(Roles = "Coach")]
public class DashboardModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<DashboardModel> _logger;

    public List<AthleteListResponse> Athletes { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public DashboardModel(IApiClient apiClient, ILogger<DashboardModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
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

        _logger.LogInformation("Coach {Email} accessing dashboard", coachEmail);

        // Fetch athletes from API
        var (success, athletes, error) = await _apiClient.GetMyAthletesAsync(token);

        if (success && athletes != null)
        {
            Athletes = athletes;
            _logger.LogInformation("Loaded {Count} athletes for coach {Email}", Athletes.Count, coachEmail);
        }
        else
        {
            ErrorMessage = error ?? "Failed to load athletes";
            _logger.LogWarning("Failed to load athletes for coach {Email}: {Error}", coachEmail, ErrorMessage);
        }

        return Page();
    }
}
