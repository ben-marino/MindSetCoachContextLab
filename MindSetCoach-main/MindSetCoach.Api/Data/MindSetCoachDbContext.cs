using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Models;

namespace MindSetCoach.Api.Data;

public class MindSetCoachDbContext : DbContext
{
    public MindSetCoachDbContext(DbContextOptions<MindSetCoachDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Coach> Coaches { get; set; } = null!;
    public DbSet<Athlete> Athletes { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure unique index for User email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Configure User-Coach relationship (one-to-one)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Coach)
            .WithOne(c => c.User)
            .HasForeignKey<Coach>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure User-Athlete relationship (one-to-one)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Athlete)
            .WithOne(a => a.User)
            .HasForeignKey<Athlete>(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Coach-Athlete relationship (one-to-many)
        modelBuilder.Entity<Coach>()
            .HasMany(c => c.Athletes)
            .WithOne(a => a.Coach)
            .HasForeignKey(a => a.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Athlete-JournalEntry relationship (one-to-many)
        modelBuilder.Entity<Athlete>()
            .HasMany(a => a.JournalEntries)
            .WithOne(j => j.Athlete)
            .HasForeignKey(j => j.AthleteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Static hashed password for test accounts (password: Test123!)
        // This hash is fixed to avoid regeneration on each build
        // Generated using: BCrypt.Net.BCrypt.HashPassword("Test123!")
        var passwordHash = "$2a$11$jWRAhGYBJJw4JlX8QWpY5uBQxRBOg3N93ktjv8m9u4XTnXg/2hrw6";

        // Static base date for seed data
        var baseDate = new DateTime(2025, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var userCreatedDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed Users
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Email = "coach@test.com", PasswordHash = passwordHash, Role = UserRole.Coach, CreatedAt = userCreatedDate },
            new User { Id = 2, Email = "athlete1@test.com", PasswordHash = passwordHash, Role = UserRole.Athlete, CreatedAt = userCreatedDate },
            new User { Id = 3, Email = "athlete2@test.com", PasswordHash = passwordHash, Role = UserRole.Athlete, CreatedAt = userCreatedDate }
        );

        // Seed Coach
        modelBuilder.Entity<Coach>().HasData(
            new Coach { Id = 1, UserId = 1, Name = "Coach Mike", Email = "coach@test.com" }
        );

        // Seed Athletes
        modelBuilder.Entity<Athlete>().HasData(
            new Athlete { Id = 1, UserId = 2, Name = "Athlete One", Email = "athlete1@test.com", CoachId = 1 },
            new Athlete { Id = 2, UserId = 3, Name = "Athlete Two", Email = "athlete2@test.com", CoachId = 1 }
        );

        // Seed Journal Entries for Athlete1
        modelBuilder.Entity<JournalEntry>().HasData(
            new JournalEntry
            {
                Id = 1,
                AthleteId = 1,
                EntryDate = baseDate,
                EmotionalState = "Feeling nervous about the upcoming competition. My anxiety is high but I'm trying to stay positive.",
                SessionReflection = "Training went okay today. I struggled with consistency in my technique. Need to focus more on breathing.",
                MentalBarriers = "Self-doubt is creeping in. I keep comparing myself to other athletes and feeling like I'm not good enough.",
                IsFlagged = true,
                CreatedAt = baseDate
            },
            new JournalEntry
            {
                Id = 2,
                AthleteId = 1,
                EntryDate = baseDate.AddDays(1),
                EmotionalState = "More confident today after talking with my coach. Feeling motivated.",
                SessionReflection = "Had a great session! My form was much better and I hit all my targets. Coach gave positive feedback.",
                MentalBarriers = "Still worried about letting my team down, but working through it with visualization techniques.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(1)
            },
            new JournalEntry
            {
                Id = 3,
                AthleteId = 1,
                EntryDate = baseDate.AddDays(2),
                EmotionalState = "Feeling strong and focused. Ready to push myself today.",
                SessionReflection = "Pushed through a tough workout. My mental game was on point - stayed present and didn't let frustration take over.",
                MentalBarriers = "Had a moment of doubt during the hardest set, but used my breathing techniques to refocus. It worked!",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(2)
            },
            new JournalEntry
            {
                Id = 4,
                AthleteId = 1,
                EntryDate = baseDate.AddDays(3),
                EmotionalState = "Tired and a bit overwhelmed. Feeling the pressure of balancing training with life.",
                SessionReflection = "Not my best session. I was distracted and couldn't focus. Made several mistakes that I don't usually make.",
                MentalBarriers = "Feeling burnt out. The voice in my head keeps saying I should take a break, but I'm afraid of losing momentum.",
                IsFlagged = true,
                CreatedAt = baseDate.AddDays(3)
            },
            new JournalEntry
            {
                Id = 5,
                AthleteId = 1,
                EntryDate = baseDate.AddDays(4),
                EmotionalState = "Recharged after taking yesterday afternoon off. Feeling balanced and ready.",
                SessionReflection = "Amazing session today! Everything clicked. I realized that rest was exactly what I needed.",
                MentalBarriers = "Learning to trust the process and not feel guilty about taking care of myself. This is progress.",
                IsFlagged = false,
                CreatedAt = baseDate.AddDays(4)
            }
        );
    }
}
