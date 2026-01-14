using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MindSetCoach.Web.Models;
using MindSetCoach.Web.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MindSetCoach.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public LoginRequest LoginRequest { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public LoginModel(IApiClient apiClient, ILogger<LoginModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public void OnGet()
    {
        // Check if already logged in using authentication
        if (User.Identity?.IsAuthenticated == true)
        {
            var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                          ?? User.FindFirst("role")?.Value;

            if (userRole == "Coach")
            {
                Response.Redirect("/Coach/Dashboard");
            }
            else if (userRole == "Athlete")
            {
                Response.Redirect("/Journal/NewEntry");
            }
            else
            {
                Response.Redirect("/Index");
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Login form validation failed");
            return Page();
        }

        _logger.LogInformation("Processing login attempt for {Email}", LoginRequest.Email);
        var (success, response, error) = await _apiClient.LoginAsync(LoginRequest);

        if (success && response != null)
        {
            // Decode the JWT token to extract all claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(response.Token);

            _logger.LogInformation("JWT Token decoded. Claims count: {ClaimCount}", jwtToken.Claims.Count());

            // Extract all claims from the JWT
            var claims = new List<Claim>();

            foreach (var claim in jwtToken.Claims)
            {
                claims.Add(new Claim(claim.Type, claim.Value));
                _logger.LogInformation("Adding claim from JWT: {Type} = {Value}", claim.Type, claim.Value);
            }

            // Create claims identity with cookie authentication scheme
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Sign the user in with the claims
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Remember login across browser sessions
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            _logger.LogInformation("User {Email} signed in with authentication cookie", response.Email);

            // Store token in session for API calls
            HttpContext.Session.SetString("Token", response.Token);

            // Also store in session for backward compatibility (can be removed later)
            HttpContext.Session.SetInt32("UserId", response.UserId);
            HttpContext.Session.SetString("UserEmail", response.Email);
            HttpContext.Session.SetString("UserRole", response.Role);

            _logger.LogInformation("User {Email} logged in successfully as {Role} with {ClaimCount} claims",
                response.Email, response.Role, claims.Count);

            // Redirect based on role
            if (response.Role == "Athlete")
            {
                return RedirectToPage("/Journal/NewEntry");
            }
            else if (response.Role == "Coach")
            {
                return RedirectToPage("/Coach/Dashboard");
            }
            else
            {
                return RedirectToPage("/Index");
            }
        }

        _logger.LogWarning("Login failed for {Email}. Error: {Error}", LoginRequest.Email, error);
        ErrorMessage = error ?? "Login failed. Please try again.";
        return Page();
    }
}
