using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MindSetCoach.Web.Models;

namespace MindSetCoach.Web.Services;

public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IHttpClientFactory httpClientFactory, ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Success, AuthResponse? Response, string? Error)> LoginAsync(LoginRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Attempting login for user: {Email}", request.Email);

            var response = await client.PostAsync("/api/auth/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Login response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (true, authResponse, null);
            }

            // Log the error response for debugging
            _logger.LogWarning("Login failed with status {StatusCode}. Response: {Response}",
                response.StatusCode, responseContent);

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var errorMessage = errorResponse?.Message ?? "Login failed";

                // Add validation errors if present
                if (errorResponse?.Errors != null && errorResponse.Errors.Any())
                {
                    var validationErrors = string.Join("; ", errorResponse.Errors.SelectMany(e => e.Value));
                    errorMessage = $"{errorMessage}: {validationErrors}";
                }

                return (false, null, errorMessage);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse error response: {Response}", responseContent);
                return (false, null, $"Login failed. Server response: {responseContent}");
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Network error during login. Could not connect to API server.");
            return (false, null, "Could not connect to server. Please make sure the API is running at http://localhost:5000");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return (false, null, $"An error occurred: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Attempting registration for user: {Email} as {Role}", request.Email, request.Role);

            var response = await client.PostAsync("/api/auth/register", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Registration response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            // Log the error response for debugging
            _logger.LogWarning("Registration failed with status {StatusCode}. Response: {Response}",
                response.StatusCode, responseContent);

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var errorMessage = errorResponse?.Message ?? "Registration failed";

                // If there are validation errors, combine them
                if (errorResponse?.Errors != null && errorResponse.Errors.Any())
                {
                    var validationErrors = string.Join("; ", errorResponse.Errors.SelectMany(e => e.Value));
                    errorMessage = $"{errorMessage}: {validationErrors}";
                }

                return (false, errorMessage);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse error response: {Response}", responseContent);
                return (false, $"Registration failed. Server response: {responseContent}");
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Network error during registration. Could not connect to API server.");
            return (false, "Could not connect to server. Please make sure the API is running at http://localhost:5000");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return (false, $"An error occurred: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> CreateJournalEntryAsync(JournalEntryRequest request, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(request);

            // DEBUG: Log the JSON being sent to the API
            _logger.LogInformation("DEBUG - Sending journal entry to API:");
            _logger.LogInformation("JSON: {Json}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/journal", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("DEBUG - API Response Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("DEBUG - API Response Content: {Content}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // If there are validation errors, combine them
                if (errorResponse?.Errors != null && errorResponse.Errors.Any())
                {
                    var errors = string.Join(", ", errorResponse.Errors.SelectMany(e => e.Value));
                    return (false, errors);
                }

                return (false, errorResponse?.Message ?? "Failed to save journal entry");
            }
            catch
            {
                return (false, "Failed to save journal entry. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating journal entry");
            return (false, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, List<JournalEntryResponse>? Entries, string? Error)> GetJournalEntriesAsync(int athleteId, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/api/journal/athlete/{athleteId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var entries = JsonSerializer.Deserialize<List<JournalEntryResponse>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (true, entries ?? new List<JournalEntryResponse>(), null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (false, null, errorResponse?.Message ?? "Failed to load journal entries");
            }
            catch
            {
                return (false, null, "Failed to load journal entries. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading journal entries");
            return (false, null, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, JournalEntryResponse? Entry, string? Error)> GetJournalEntryByIdAsync(int entryId, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/api/journal/{entryId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var entry = JsonSerializer.Deserialize<JournalEntryResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (true, entry, null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (false, null, errorResponse?.Message ?? "Failed to load journal entry");
            }
            catch
            {
                return (false, null, "Failed to load journal entry. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading journal entry");
            return (false, null, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, List<AthleteListResponse>? Athletes, string? Error)> GetMyAthletesAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/coach/athletes");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var athletes = JsonSerializer.Deserialize<List<AthleteListResponse>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (true, athletes ?? new List<AthleteListResponse>(), null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (false, null, errorResponse?.Message ?? "Failed to load athletes");
            }
            catch
            {
                return (false, null, "Failed to load athletes. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading athletes");
            return (false, null, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, AthleteDetailResponse? AthleteDetail, string? Error)> GetAthleteDetailAsync(int athleteId, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/api/coach/athletes/{athleteId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var athleteDetail = JsonSerializer.Deserialize<AthleteDetailResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (true, athleteDetail, null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (false, null, errorResponse?.Message ?? "Failed to load athlete details");
            }
            catch
            {
                return (false, null, "Failed to load athlete details. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading athlete details for athlete {AthleteId}", athleteId);
            return (false, null, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, string? Error)> FlagJournalEntryAsync(int entryId, bool flagged, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MindSetCoachAPI");

            // Add JWT token to Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var requestBody = new { Flagged = flagged };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"/api/journal/{entryId}/flag", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            // Try to parse error message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiError>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return (false, errorResponse?.Message ?? "Failed to update flag status");
            }
            catch
            {
                return (false, "Failed to update flag status. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flagging journal entry {EntryId}", entryId);
            return (false, "An error occurred. Please try again later.");
        }
    }
}
