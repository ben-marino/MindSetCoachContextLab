using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Services;

/// <summary>
/// Service for seeding demo data into the database.
/// Provides realistic journal entries for testing experiments.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly MindSetCoachDbContext _dbContext;
    private readonly ILogger<DataSeeder> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string DemoCoachEmail = "demo.coach@mindsetcoach.app";
    private const string DemoAthleteEmail = "demo.athlete@mindsetcoach.app";
    private const string DemoPassword = "Demo123!";

    public DataSeeder(
        MindSetCoachDbContext dbContext,
        ILogger<DataSeeder> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    public async Task<bool> IsDemoDataSeededAsync()
    {
        return await _dbContext.Users.AnyAsync(u => u.Email == DemoAthleteEmail);
    }

    public async Task SeedDemoDataAsync()
    {
        if (await IsDemoDataSeededAsync())
        {
            _logger.LogInformation("Demo data already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding demo data...");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Create demo coach user
            var coachUser = new User
            {
                Email = DemoCoachEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
                Role = UserRole.Coach,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            _dbContext.Users.Add(coachUser);
            await _dbContext.SaveChangesAsync();

            // Create demo coach
            var coach = new Coach
            {
                UserId = coachUser.Id,
                Name = "Coach Sarah Mitchell",
                Email = DemoCoachEmail
            };
            _dbContext.Coaches.Add(coach);
            await _dbContext.SaveChangesAsync();

            // Create demo athlete user
            var athleteUser = new User
            {
                Email = DemoAthleteEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
                Role = UserRole.Athlete,
                CreatedAt = DateTime.UtcNow.AddDays(-28)
            };
            _dbContext.Users.Add(athleteUser);
            await _dbContext.SaveChangesAsync();

            // Create demo athlete
            var athlete = new Athlete
            {
                UserId = athleteUser.Id,
                CoachId = coach.Id,
                Name = "Alex Rivera",
                Email = DemoAthleteEmail
            };
            _dbContext.Athletes.Add(athlete);
            await _dbContext.SaveChangesAsync();

            // Load and create journal entries
            var entries = await LoadJournalEntriesAsync();
            foreach (var entry in entries)
            {
                entry.AthleteId = athlete.Id;
                _dbContext.JournalEntries.Add(entry);
            }
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Demo data seeded successfully: 1 coach, 1 athlete, {EntryCount} journal entries",
                entries.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to seed demo data");
            throw;
        }
    }

    public async Task ClearDemoDataAsync()
    {
        _logger.LogWarning("Clearing demo data...");

        var demoAthlete = await _dbContext.Athletes
            .Include(a => a.JournalEntries)
            .FirstOrDefaultAsync(a => a.Email == DemoAthleteEmail);

        if (demoAthlete != null)
        {
            _dbContext.JournalEntries.RemoveRange(demoAthlete.JournalEntries);
            _dbContext.Athletes.Remove(demoAthlete);
        }

        var demoAthleteUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == DemoAthleteEmail);
        if (demoAthleteUser != null)
        {
            _dbContext.Users.Remove(demoAthleteUser);
        }

        var demoCoach = await _dbContext.Coaches
            .FirstOrDefaultAsync(c => c.Email == DemoCoachEmail);
        if (demoCoach != null)
        {
            _dbContext.Coaches.Remove(demoCoach);
        }

        var demoCoachUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == DemoCoachEmail);
        if (demoCoachUser != null)
        {
            _dbContext.Users.Remove(demoCoachUser);
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Demo data cleared successfully");
    }

    private async Task<List<JournalEntry>> LoadJournalEntriesAsync()
    {
        var jsonPath = Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "SeedData",
            "DemoJournalEntries.json");

        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("Demo journal entries file not found at {Path}, using embedded data", jsonPath);
            return GetEmbeddedJournalEntries();
        }

        var json = await File.ReadAllTextAsync(jsonPath);
        var entriesData = JsonSerializer.Deserialize<List<JournalEntryData>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (entriesData == null || entriesData.Count == 0)
        {
            _logger.LogWarning("No entries found in JSON file, using embedded data");
            return GetEmbeddedJournalEntries();
        }

        var baseDate = DateTime.UtcNow.Date;
        return entriesData.Select(e => new JournalEntry
        {
            EntryDate = baseDate.AddDays(e.DaysAgo * -1),
            EmotionalState = e.EmotionalState,
            SessionReflection = e.SessionReflection,
            MentalBarriers = e.MentalBarriers,
            IsFlagged = e.IsFlagged,
            CreatedAt = baseDate.AddDays(e.DaysAgo * -1).AddHours(20) // Evening entries
        }).ToList();
    }

    /// <summary>
    /// Embedded journal entries in case JSON file is not available.
    /// Contains realistic running/training data with the required "shin splints on Tuesday" needle fact.
    /// </summary>
    private List<JournalEntry> GetEmbeddedJournalEntries()
    {
        var baseDate = DateTime.UtcNow.Date;
        var entries = new List<JournalEntry>
        {
            // Week 3 (oldest) - Days 21-15
            new()
            {
                EntryDate = baseDate.AddDays(-21),
                EmotionalState = "Excited but nervous. Starting my marathon training block today. 16 weeks until race day.",
                SessionReflection = "Easy 8km shake-out run. Legs felt fresh, heart rate stayed low. Focused on form and breathing rhythm. Good starting point for the training block.",
                MentalBarriers = "Already feeling pressure about the race. Need to take it one week at a time and trust the process.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-21).AddHours(19)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-20),
                EmotionalState = "Motivated. Ready to push myself in today's speed session.",
                SessionReflection = "Track workout: 6x800m at 5K pace with 90s recovery. Splits were 3:12, 3:10, 3:08, 3:11, 3:09, 3:06. Last rep felt strong! Total session including warm-up: 12km.",
                MentalBarriers = "Third rep was tough mentally - wanted to quit but pushed through. Visualization helped.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-20).AddHours(18)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-19),
                EmotionalState = "Tired but satisfied. Recovery day mindset.",
                SessionReflection = "30 min easy jog followed by stretching and foam rolling. Listened to a podcast about elite marathon training - very inspiring.",
                MentalBarriers = "Had to fight the urge to run faster. Learning that recovery is training too.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-19).AddHours(20)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-18),
                EmotionalState = "Strong and confident.",
                SessionReflection = "Tempo run: 3km warm-up, 8km at marathon pace (4:45/km), 2km cool-down. Hit every split perfectly. This is the pace I need to hold for 42km.",
                MentalBarriers = "Around km 6 of the tempo, doubt crept in. Used my mantra 'smooth and strong' to refocus.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-18).AddHours(19)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-17),
                EmotionalState = "Peaceful. Enjoying the process.",
                SessionReflection = "Easy 10km through the park. Beautiful morning, ran by feel. Noticed my breathing is getting more efficient.",
                MentalBarriers = "None today. Sometimes the best runs are the ones with no agenda.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-17).AddHours(7)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-16),
                EmotionalState = "Anxious about tomorrow's long run.",
                SessionReflection = "Rest day. Did some light yoga and worked on hip mobility. Prepped my nutrition and gear for tomorrow's 24km.",
                MentalBarriers = "Can't stop thinking about the long run. What if I bonk? Need to trust my fitness.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-16).AddHours(21)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-15),
                EmotionalState = "Accomplished! Huge confidence boost.",
                SessionReflection = "Long run: 24km at easy pace. First 18km felt comfortable, last 6km required mental toughness. Took gels at 8km and 16km. Finished strong with a 4:35 final km.",
                MentalBarriers = "Hit a wall at 20km but remembered why I'm doing this. Dedicated that last 4km to my family who always supports me.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-15).AddHours(11)
            },

            // Week 2 - Days 14-8 (INCLUDES SHIN SPLINTS NEEDLE FACT)
            new()
            {
                EntryDate = baseDate.AddDays(-14),
                EmotionalState = "Legs are sore but spirits are high.",
                SessionReflection = "Recovery swim: 30 min easy laps. Great for loosening up without impact. Also did 15 min of stretching.",
                MentalBarriers = "Impatience - I want to run but know I need to recover properly.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-14).AddHours(18)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-13), // This is a Tuesday - NEEDLE FACT
                EmotionalState = "Frustrated and worried. Woke up with pain in my shins.",
                SessionReflection = "Had to cut my run short today. Noticed shin splints on Tuesday morning during my warm-up jog. Only managed 3km before the pain became too uncomfortable. Iced immediately after and booked a physio appointment.",
                MentalBarriers = "Fear of injury derailing my training. Catastrophizing about missing the marathon. Need to stay calm and address this properly.",
                IsFlagged = true,
                CreatedAt = baseDate.AddDays(-13).AddHours(8)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-12),
                EmotionalState = "Cautiously optimistic after physio visit.",
                SessionReflection = "No running today per physio advice. Did 45 min on the stationary bike and strength exercises for my calves and tibialis anterior. The physio said it's mild and caught early.",
                MentalBarriers = "Frustration at not being able to run. But I'm channeling that energy into recovery work.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-12).AddHours(19)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-11),
                EmotionalState = "Patient but restless.",
                SessionReflection = "Pool running for 40 minutes. Weird sensation but good workout. Also did more calf strengthening. Shins feeling better already.",
                MentalBarriers = "FOMO seeing other runners outside. Reminding myself this is temporary.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-11).AddHours(17)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-10),
                EmotionalState = "Relieved and grateful.",
                SessionReflection = "Test jog: 15 min very easy on grass. No pain! Kept heart rate super low. Followed with extensive stretching and foam rolling.",
                MentalBarriers = "Had to resist the urge to run longer. Smart training means knowing when to hold back.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-10).AddHours(18)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-9),
                EmotionalState = "Back in action! Feeling like myself again.",
                SessionReflection = "Easy 6km run, all systems go. Wore compression sleeves as a precaution. Focused on landing softly and maintaining good cadence.",
                MentalBarriers = "Still some residual anxiety about the injury returning. Taking it day by day.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-9).AddHours(19)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-8),
                EmotionalState = "Building momentum again.",
                SessionReflection = "Modified tempo: 2km warm-up, 5km at tempo pace, 2km cool-down. Felt controlled and comfortable. Shins held up perfectly.",
                MentalBarriers = "Mental battle between wanting to make up for lost time and being patient. Patience won.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-8).AddHours(18)
            },

            // Week 1 (most recent) - Days 7-0
            new()
            {
                EntryDate = baseDate.AddDays(-7),
                EmotionalState = "Confident. The setback made me mentally stronger.",
                SessionReflection = "Long run: 20km. Intentionally shorter than planned to be safe. Consistent 5:00/km pace throughout. Nutrition strategy worked well.",
                MentalBarriers = "Around km 15 I felt a phantom twinge in my shin (probably imagined). Stayed calm, checked my form, and it passed.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-7).AddHours(10)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-6),
                EmotionalState = "Peaceful and content.",
                SessionReflection = "Active recovery: 30 min walk, yoga session focusing on hip openers, and meditation. Sleep has been great this week - averaging 8 hours.",
                MentalBarriers = "None. Learning to enjoy the recovery days as much as the hard sessions.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-6).AddHours(20)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-5),
                EmotionalState = "Focused and determined.",
                SessionReflection = "Speed session: 5x1km at 10K pace with 2 min recovery. Splits: 3:55, 3:52, 3:50, 3:51, 3:48. Getting faster! Total with warm-up/cool-down: 14km.",
                MentalBarriers = "Rep 4 was a grind. Used box breathing between reps to stay centered.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-5).AddHours(18)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-4),
                EmotionalState = "Grateful for my body's resilience.",
                SessionReflection = "Easy 8km with a friend. Great to have company - the conversation made the miles fly by. Discussed race strategy and pacing.",
                MentalBarriers = "Comparison thoughts crept in - my friend is faster than me. Redirected focus to my own journey.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-4).AddHours(19)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-3),
                EmotionalState = "Slightly fatigued but pushing through.",
                SessionReflection = "Progressive run: Started at 5:30/km, finished last 3km at 4:30/km. Total 12km. This teaches my body to run fast on tired legs.",
                MentalBarriers = "Had to dig deep in the final fast kilometers. Visualized crossing the marathon finish line.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-3).AddHours(18)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-2),
                EmotionalState = "Ready for the weekend long run.",
                SessionReflection = "Easy 6km shakeout plus drills and strides. Legs feel springy. Laid out all my gear for tomorrow's 26km.",
                MentalBarriers = "Pre-long-run anxiety is back but I recognize it now. It's just my body preparing for the challenge.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-2).AddHours(17)
            },
            new()
            {
                EntryDate = baseDate.AddDays(-1),
                EmotionalState = "BREAKTHROUGH! Strongest long run yet!",
                SessionReflection = "Long run: 26km with the last 6km at marathon pace. Executed the plan perfectly. Felt strong at the end - like I could have kept going. This is the fitness I've been building toward!",
                MentalBarriers = "Zero mental barriers today. Everything clicked. This is what happens when you trust the process and stay consistent despite setbacks.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(-1).AddHours(11)
            },
            new()
            {
                EntryDate = baseDate,
                EmotionalState = "Proud and motivated. Looking ahead to the next training block.",
                SessionReflection = "Rest day. Light stretching and reflection. Wrote down my goals for the coming weeks. The shin splints scare taught me the importance of listening to my body.",
                MentalBarriers = "Some impatience to get back to training, but I know rest is when adaptation happens. Trusting the process.",
                IsFlagged = false,
                CreatedAt = baseDate.AddHours(20)
            }
        };

        return entries;
    }

    /// <summary>
    /// DTO for deserializing journal entries from JSON.
    /// </summary>
    private class JournalEntryData
    {
        public int DaysAgo { get; set; }
        public string EmotionalState { get; set; } = string.Empty;
        public string SessionReflection { get; set; } = string.Empty;
        public string MentalBarriers { get; set; } = string.Empty;
        public bool IsFlagged { get; set; }
    }
}
