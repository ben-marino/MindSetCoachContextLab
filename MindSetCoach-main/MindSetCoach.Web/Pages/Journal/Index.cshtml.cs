using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MindSetCoach.Web.Pages.Journal;

public class IndexModel : PageModel
{
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }

    public IActionResult OnGet()
    {
        // Check if user is authenticated using claims
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Auth/Login");
        }

        // Get user info from claims
        UserEmail = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                   ?? User.FindFirst("email")?.Value;
        UserRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                  ?? User.FindFirst("role")?.Value;

        // Redirect athletes to new entry page
        if (UserRole == "Athlete")
        {
            return RedirectToPage("/Journal/NewEntry");
        }

        // Redirect coaches to their dashboard
        if (UserRole == "Coach")
        {
            return RedirectToPage("/Coach/Dashboard");
        }

        return Page();
    }
}
