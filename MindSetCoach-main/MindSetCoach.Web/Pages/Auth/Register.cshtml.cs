using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;

namespace MindSetCoach.Web.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<RegisterModel> _logger;

    [BindProperty]
    public RegisterRequest RegisterRequest { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public RegisterModel(IApiClient apiClient, ILogger<RegisterModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public void OnGet()
    {
        // Check if already logged in
        if (HttpContext.Session.GetString("Token") != null)
        {
            Response.Redirect("/Journal/Index");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Registration form validation failed");
            return Page();
        }

        // Additional validation for coach email
        if (RegisterRequest.Role == "Athlete" && string.IsNullOrWhiteSpace(RegisterRequest.CoachEmail))
        {
            ModelState.AddModelError("RegisterRequest.CoachEmail", "Coach email is required for athletes");
            _logger.LogWarning("Athlete registration missing coach email");
            return Page();
        }

        _logger.LogInformation("Processing registration for {Email} as {Role}", RegisterRequest.Email, RegisterRequest.Role);
        var (success, error) = await _apiClient.RegisterAsync(RegisterRequest);

        if (success)
        {
            _logger.LogInformation("User {Email} registered successfully as {Role}", RegisterRequest.Email, RegisterRequest.Role);
            SuccessMessage = "Registration successful! Please login with your credentials.";

            // Clear the form
            RegisterRequest = new();

            return Page();
        }

        _logger.LogWarning("Registration failed for {Email}. Error: {Error}", RegisterRequest.Email, error);
        ErrorMessage = error ?? "Registration failed. Please try again.";
        return Page();
    }
}
