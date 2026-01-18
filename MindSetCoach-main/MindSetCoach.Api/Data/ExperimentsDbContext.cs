using Microsoft.EntityFrameworkCore;
using MindSetCoach.Api.Models.Experiments;

namespace MindSetCoach.Api.Data;

public class ExperimentsDbContext : DbContext
{
    public ExperimentsDbContext(DbContextOptions<ExperimentsDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExperimentRun> ExperimentRuns { get; set; } = null!;
    public DbSet<ExperimentClaim> ExperimentClaims { get; set; } = null!;
    public DbSet<ClaimReceipt> ClaimReceipts { get; set; } = null!;
    public DbSet<PositionTest> PositionTests { get; set; } = null!;
    public DbSet<ExperimentPreset> ExperimentPresets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ExperimentRun configuration
        modelBuilder.Entity<ExperimentRun>(entity =>
        {
            entity.HasIndex(e => e.AthleteId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.BatchId);

            entity.Property(e => e.EstimatedCost)
                .HasPrecision(18, 6);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.ExperimentType)
                .HasConversion<string>();
        });

        // ExperimentClaim configuration
        modelBuilder.Entity<ExperimentClaim>(entity =>
        {
            entity.HasOne(c => c.ExperimentRun)
                .WithMany(r => r.Claims)
                .HasForeignKey(c => c.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ClaimReceipt configuration
        modelBuilder.Entity<ClaimReceipt>(entity =>
        {
            entity.HasOne(r => r.ExperimentClaim)
                .WithMany(c => c.Receipts)
                .HasForeignKey(r => r.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => r.JournalEntryId);
        });

        // PositionTest configuration
        modelBuilder.Entity<PositionTest>(entity =>
        {
            entity.HasOne(p => p.ExperimentRun)
                .WithMany(r => r.PositionTests)
                .HasForeignKey(p => p.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Position)
                .HasConversion<string>();
        });

        // ExperimentPreset configuration
        modelBuilder.Entity<ExperimentPreset>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsDefault);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
