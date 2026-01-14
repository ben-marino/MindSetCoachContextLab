using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MindSetCoach.Web.Pages.Auth;

public class LogoutModel : PageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var email = User.Identity?.Name ?? HttpContext.Session.GetString("UserEmail");

        // Sign out from cookie authentication
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Clear all session data
        HttpContext.Session.Clear();

        _logger.LogInformation("User {Email} logged out", email);

        // Redirect to login page
        return RedirectToPage("/Auth/Login");
    }
}
