namespace MindSetCoach.Api.Configuration;

/// <summary>
/// Configuration options for automated experiment scheduling.
/// </summary>
public class ExperimentSchedulerOptions
{
    public const string SectionName = "Experiments";

    /// <summary>
    /// Whether to automatically run a baseline experiment on startup if no experiments exist.
    /// </summary>
    public bool AutoRunOnStartup { get; set; } = false;

    /// <summary>
    /// The default athlete ID to use for automated experiments.
    /// </summary>
    public int DefaultAthleteId { get; set; } = 1;

    /// <summary>
    /// The preset name to run on startup when no experiments exist.
    /// </summary>
    public string DefaultPreset { get; set; } = "Quick U-Curve Test";

    /// <summary>
    /// Cron expression for scheduled experiment runs (e.g., "0 6 * * *" for 6am daily).
    /// Leave empty to disable scheduled runs.
    /// </summary>
    public string? Schedule { get; set; }

    /// <summary>
    /// The preset name to run on schedule.
    /// </summary>
    public string ScheduledPreset { get; set; } = "Full Provider Sweep";
}
