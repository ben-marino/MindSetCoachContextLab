namespace MindSetCoach.Api.Services;

/// <summary>
/// Service for seeding demo data into the database.
/// Used for development and demo environments.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seeds demo data including coach, athlete, and journal entries.
    /// Idempotent - will not duplicate data if already seeded.
    /// </summary>
    Task SeedDemoDataAsync();

    /// <summary>
    /// Checks if demo data has already been seeded.
    /// </summary>
    Task<bool> IsDemoDataSeededAsync();

    /// <summary>
    /// Clears all demo data from the database.
    /// Use with caution - this is destructive.
    /// </summary>
    Task ClearDemoDataAsync();
}
