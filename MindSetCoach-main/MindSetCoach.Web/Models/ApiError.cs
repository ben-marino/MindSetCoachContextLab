namespace MindSetCoach.Web.Models;

public class ApiError
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
}
