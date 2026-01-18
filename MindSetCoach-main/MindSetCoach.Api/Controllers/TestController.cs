using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Controllers;

#region Request DTOs

/// <summary>
/// Request body for seeding test journal entries.
/// </summary>
public class SeedEntriesRequest
{
    [Required]
    public int AthleteId { get; set; } = 1;

    [Range(1, 30)]
    public int Count { get; set; } = 14;

    [Required]
    public string Scenario { get; set; } = "mixed-random";
}

/// <summary>
/// Response for seed entries endpoint.
/// </summary>
public class SeedEntriesResponse
{
    public int EntriesCreated { get; set; }
    public int AthleteId { get; set; }
    public string Scenario { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly MindSetCoachDbContext _context;
    private readonly ILogger<TestController> _logger;

    public TestController(MindSetCoachDbContext context, ILogger<TestController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// DEBUG: Get athlete by ID with coach information
    /// </summary>
    [HttpGet("athlete/{athleteId}")]
    public async Task<ActionResult> GetAthleteWithCoach(int athleteId)
    {
        var athlete = await _context.Athletes
            .Include(a => a.User)
            .Include(a => a.Coach)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(a => a.Id == athleteId);

        if (athlete == null)
        {
            return NotFound(new { message = $"Athlete with ID {athleteId} not found" });
        }

        var result = new
        {
            athleteId = athlete.Id,
            athleteName = athlete.Name,
            athleteEmail = athlete.Email,
            athleteUserId = athlete.UserId,
            coachId = athlete.CoachId,
            coach = athlete.Coach == null ? null : new
            {
                coachId = athlete.Coach.Id,
                coachName = athlete.Coach.Name,
                coachEmail = athlete.Coach.Email,
                coachUserId = athlete.Coach.UserId
            }
        };

        _logger.LogInformation("Retrieved athlete {AthleteId} with CoachId={CoachId}", athleteId, athlete.CoachId);

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all athletes with their coaches
    /// </summary>
    [HttpGet("athletes")]
    public async Task<ActionResult> GetAllAthletes()
    {
        var athletes = await _context.Athletes
            .Include(a => a.User)
            .Include(a => a.Coach)
            .ToListAsync();

        var result = athletes.Select(a => new
        {
            athleteId = a.Id,
            athleteName = a.Name,
            athleteEmail = a.Email,
            coachId = a.CoachId,
            coachName = a.Coach?.Name,
            coachEmail = a.Coach?.Email
        });

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all coaches
    /// </summary>
    [HttpGet("coaches")]
    public async Task<ActionResult> GetAllCoaches()
    {
        var coaches = await _context.Coaches
            .Include(c => c.User)
            .ToListAsync();

        var result = coaches.Select(c => new
        {
            coachId = c.Id,
            coachName = c.Name,
            coachEmail = c.Email,
            userId = c.UserId
        });

        return Ok(result);
    }

    /// <summary>
    /// DEBUG: List all users (without password hashes)
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult> GetAllUsers()
    {
        var users = await _context.Users.ToListAsync();

        var result = users.Select(u => new
        {
            userId = u.Id,
            email = u.Email,
            role = u.Role.ToString(),
            createdAt = u.CreatedAt,
            passwordHashPreview = u.PasswordHash.Substring(0, Math.Min(20, u.PasswordHash.Length)) + "..." // First 20 chars only for debugging
        });

        return Ok(result);
    }

    /// <summary>
    /// Generate realistic test journal entries for experiments.
    /// Scenarios: injury-recovery, motivation-dip, consistent-growth, mixed-random
    /// </summary>
    [HttpPost("seed-entries")]
    public async Task<ActionResult<SeedEntriesResponse>> SeedEntries([FromBody] SeedEntriesRequest request)
    {
        // Validate athlete exists
        var athlete = await _context.Athletes.FindAsync(request.AthleteId);
        if (athlete == null)
        {
            return NotFound(new { message = $"Athlete with ID {request.AthleteId} not found" });
        }

        var validScenarios = new[] { "injury-recovery", "motivation-dip", "consistent-growth", "mixed-random" };
        if (!validScenarios.Contains(request.Scenario.ToLower()))
        {
            return BadRequest(new { message = $"Invalid scenario. Valid options: {string.Join(", ", validScenarios)}" });
        }

        // Delete existing entries for this athlete
        var existingEntries = await _context.JournalEntries
            .Where(e => e.AthleteId == request.AthleteId)
            .ToListAsync();
        _context.JournalEntries.RemoveRange(existingEntries);

        // Generate new entries
        var entries = GenerateEntriesForScenario(request.AthleteId, request.Count, request.Scenario.ToLower());

        _context.JournalEntries.AddRange(entries);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} journal entries for Athlete {AthleteId} with scenario '{Scenario}'",
            entries.Count, request.AthleteId, request.Scenario);

        return Ok(new SeedEntriesResponse
        {
            EntriesCreated = entries.Count,
            AthleteId = request.AthleteId,
            Scenario = request.Scenario,
            StartDate = entries.Min(e => e.EntryDate),
            EndDate = entries.Max(e => e.EntryDate),
            Message = $"Successfully created {entries.Count} journal entries for scenario '{request.Scenario}'"
        });
    }

    private List<JournalEntry> GenerateEntriesForScenario(int athleteId, int count, string scenario)
    {
        var entries = new List<JournalEntry>();
        var baseDate = DateTime.UtcNow.Date.AddDays(-count + 1);
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < count; i++)
        {
            var entryDate = baseDate.AddDays(i);
            var dayOfWeek = entryDate.DayOfWeek;
            var progress = (double)i / (count - 1); // 0.0 to 1.0

            var entry = scenario switch
            {
                "injury-recovery" => GenerateInjuryRecoveryEntry(athleteId, entryDate, i, progress, random),
                "motivation-dip" => GenerateMotivationDipEntry(athleteId, entryDate, i, progress, dayOfWeek, random),
                "consistent-growth" => GenerateConsistentGrowthEntry(athleteId, entryDate, i, progress, random),
                "mixed-random" => GenerateMixedRandomEntry(athleteId, entryDate, i, dayOfWeek, random),
                _ => GenerateMixedRandomEntry(athleteId, entryDate, i, dayOfWeek, random)
            };

            entries.Add(entry);
        }

        return entries;
    }

    private JournalEntry GenerateInjuryRecoveryEntry(int athleteId, DateTime date, int dayIndex, double progress, Random random)
    {
        // Starts with injury, gradually improves
        var emotionalStates = progress switch
        {
            < 0.2 => new[] { "Frustrated and in pain", "Worried about recovery", "Disappointed", "Anxious about missing training" },
            < 0.4 => new[] { "Still struggling but hopeful", "Cautiously optimistic", "Impatient but trying to stay positive" },
            < 0.6 => new[] { "Feeling a bit better today", "Encouraged by small progress", "More hopeful about recovery" },
            < 0.8 => new[] { "Getting stronger each day", "Excited to be back moving", "Confident in my recovery", "Grateful for progress" },
            _ => new[] { "Feeling great and ready to push", "Back to my old self", "Energized and motivated", "Strong and confident" }
        };

        var barriers = progress switch
        {
            < 0.3 => new[] {
                "Shin splints are really bothering me - can barely walk",
                "The pain in my shins is constant",
                "Shin splints making every step difficult",
                "Doctor says shin splints need rest but I'm impatient"
            },
            < 0.5 => new[] {
                "Shin splints still present but less intense",
                "Managing the shin pain with ice and rest",
                "Worried about re-aggravating the shin splints"
            },
            < 0.7 => new[] {
                "Occasional twinge in shins but mostly manageable",
                "Fear of re-injury holding me back a bit",
                "Taking it slow to protect my shins"
            },
            _ => new[] {
                "Minor tightness but nothing serious",
                "Just need to maintain good warm-up habits",
                "Feeling strong, no significant barriers"
            }
        };

        var reflections = progress switch
        {
            < 0.2 => new[] {
                "Had to skip training completely. Very light stretching only.",
                "Just icing and resting. Watching teammates train is hard.",
                "Physical therapy exercises only. Feeling left behind."
            },
            < 0.4 => new[] {
                "Started some pool running today. Movement feels good.",
                "Light cross-training - stationary bike for 20 mins.",
                "First pain-free walk in a week. Small victory."
            },
            < 0.6 => new[] {
                "Easy jog for 10 minutes - first run in weeks!",
                "Gradual return to training. Being very careful.",
                "Did 50% of normal workout. Felt controlled and safe."
            },
            < 0.8 => new[] {
                "Completed full warm-up and 75% of regular training.",
                "Ran 3 miles at easy pace. No pain during or after.",
                "Pushed a bit harder today. Body responding well."
            },
            _ => new[] {
                "Full training session completed! Felt amazing to be back.",
                "Hit all my targets today. Stronger than before the injury.",
                "Personal best in drills. The forced rest might have helped."
            }
        };

        return new JournalEntry
        {
            AthleteId = athleteId,
            EntryDate = date,
            EmotionalState = emotionalStates[random.Next(emotionalStates.Length)],
            MentalBarriers = barriers[random.Next(barriers.Length)],
            SessionReflection = reflections[random.Next(reflections.Length)],
            IsFlagged = progress < 0.3 // Flag early injury entries
        };
    }

    private JournalEntry GenerateMotivationDipEntry(int athleteId, DateTime date, int dayIndex, double progress, DayOfWeek dayOfWeek, Random random)
    {
        // Strong start, mid-week slump, weekend recovery
        var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
        var isEarlyWeek = dayOfWeek == DayOfWeek.Monday || dayOfWeek == DayOfWeek.Tuesday;
        var isMidWeek = dayOfWeek == DayOfWeek.Wednesday || dayOfWeek == DayOfWeek.Thursday;

        string[] emotionalStates;
        string[] barriers;
        string[] reflections;

        if (isEarlyWeek || progress < 0.2)
        {
            emotionalStates = new[] { "Motivated and ready to go", "Fresh start energy", "Feeling strong and focused", "Excited for the week" };
            barriers = new[] { "None - feeling great", "Just minor fatigue from weekend activities", "Ready to tackle anything" };
            reflections = new[] {
                "Great session today, hit all my numbers.",
                "Started the week strong. Energy levels high.",
                "Productive training, everything clicked."
            };
        }
        else if (isMidWeek || (progress > 0.3 && progress < 0.7))
        {
            emotionalStates = new[] { "Tired and unmotivated", "Struggling to find energy", "Just going through the motions", "Feeling burnt out" };
            barriers = new[] {
                "Work stress is draining all my energy",
                "Shin splints flared up, making me question everything",
                "Sleep has been terrible, can't focus",
                "Doubting if I'm making any progress"
            };
            reflections = new[] {
                "Barely made it through today's session. Cut it short.",
                "Went to the gym but just couldn't push. Left early.",
                "Skipped training entirely. Needed a mental break.",
                "Did half the workout, everything felt heavy."
            };
        }
        else // Weekend or late week recovery
        {
            emotionalStates = new[] { "Recovering well", "Getting my spark back", "Better today than yesterday", "Finding my rhythm again" };
            barriers = new[] {
                "Still some residual fatigue but improving",
                "Mind is clearer, body following",
                "Remembered why I do this - passion returning"
            };
            reflections = new[] {
                "Lighter session but quality work. Feeling recharged.",
                "Good recovery day. Active rest helped.",
                "Back to enjoying training. The slump is passing."
            };
        }

        return new JournalEntry
        {
            AthleteId = athleteId,
            EntryDate = date,
            EmotionalState = emotionalStates[random.Next(emotionalStates.Length)],
            MentalBarriers = barriers[random.Next(barriers.Length)],
            SessionReflection = reflections[random.Next(reflections.Length)],
            IsFlagged = isMidWeek && random.Next(100) < 40 // Flag some mid-week struggles
        };
    }

    private JournalEntry GenerateConsistentGrowthEntry(int athleteId, DateTime date, int dayIndex, double progress, Random random)
    {
        // Steady positive progression
        var emotionalStates = new[]
        {
            "Focused and determined", "Confident in my progress", "Steady and consistent",
            "Building momentum", "Feeling stronger each day", "In a good rhythm",
            "Calm and collected", "Positive mindset", "Ready for the challenge"
        };

        var barriers = new[]
        {
            "Minor muscle tightness but manageable",
            "Some fatigue but pushing through",
            "Weather wasn't ideal but adapted",
            "Slight soreness from yesterday but normal",
            "None today - all systems go",
            "Had to adjust timing but made it work"
        };

        // Add shin splints mention in middle entries for needle tests
        if (dayIndex == 6 || dayIndex == 7)
        {
            barriers = new[]
            {
                "Noticed mild shin splints starting - increased stretching",
                "Shin area felt tight, added extra foam rolling",
                "Being proactive about shin splints prevention"
            };
        }

        var metricsImprovement = (int)(progress * 15); // 0-15% improvement
        var reflections = new[]
        {
            $"Solid session. Pace improved by {metricsImprovement}% from baseline.",
            "Consistent effort today. Building the foundation.",
            "Hit all planned targets. Staying the course.",
            "Good technical work. Form is improving.",
            $"New personal record in drills! {5 + metricsImprovement} reps.",
            "Quality over quantity today. Smart training.",
            "Recovery metrics looking good. Training is sustainable."
        };

        return new JournalEntry
        {
            AthleteId = athleteId,
            EntryDate = date,
            EmotionalState = emotionalStates[random.Next(emotionalStates.Length)],
            MentalBarriers = barriers[random.Next(barriers.Length)],
            SessionReflection = reflections[random.Next(reflections.Length)],
            IsFlagged = false
        };
    }

    private JournalEntry GenerateMixedRandomEntry(int athleteId, DateTime date, int dayIndex, DayOfWeek dayOfWeek, Random random)
    {
        // Realistic random variations
        var emotionalStates = new[]
        {
            "Feeling great today!", "A bit tired but pushed through", "Motivated and focused",
            "Struggling with energy", "Excited about progress", "Anxious about upcoming competition",
            "Calm and centered", "Frustrated with plateau", "Grateful for the opportunity",
            "Overwhelmed by schedule", "Confident after yesterday's session", "Uncertain about technique"
        };

        var barriers = new[]
        {
            "None today - all clear",
            "Work deadlines creating stress",
            "Didn't sleep well last night",
            "Shin splints acting up again",
            "Self-doubt creeping in",
            "Weather disrupted outdoor plans",
            "Sore from previous session",
            "Distracted by personal issues",
            "Minor knee discomfort",
            "Comparison to teammates affecting mood",
            "Fear of not being good enough",
            "Tight hamstrings limiting range"
        };

        var reflections = new[]
        {
            "Great workout! Everything clicked today.",
            "Pushed through a tough session. Proud of the effort.",
            "Technical focus day - worked on form details.",
            "Recovery session - keeping it light and easy.",
            "Missed some targets but learned from it.",
            "Personal best in the main set!",
            "Struggled with motivation but showed up anyway.",
            "Partnered training - accountability helped.",
            "Video analysis revealed areas to improve.",
            "Competition prep going well.",
            "Active recovery - mobility and stretching.",
            "Interval work was brutal but completed it."
        };

        // Flag entries with concerning content
        var selectedEmotional = emotionalStates[random.Next(emotionalStates.Length)];
        var selectedBarrier = barriers[random.Next(barriers.Length)];
        var shouldFlag = selectedBarrier.Contains("Self-doubt") ||
                         selectedBarrier.Contains("Fear") ||
                         selectedEmotional.Contains("Frustrated") ||
                         selectedEmotional.Contains("Overwhelmed");

        return new JournalEntry
        {
            AthleteId = athleteId,
            EntryDate = date,
            EmotionalState = selectedEmotional,
            MentalBarriers = selectedBarrier,
            SessionReflection = reflections[random.Next(reflections.Length)],
            IsFlagged = shouldFlag
        };
    }
}
