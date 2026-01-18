using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindSetCoach.Api.Configuration;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Services;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;

namespace MindSetCoach.Cli;

/// <summary>
/// Factory for creating configured service providers for CLI commands.
/// </summary>
public static class ServiceFactory
{
    /// <summary>
    /// Build a service provider with all required services for experiment operations.
    /// </summary>
    public static ServiceProvider CreateServiceProvider(bool verbose = false)
    {
        var services = new ServiceCollection();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
        });

        // Main database configuration
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            // Parse Fly.io DATABASE_URL format: postgres://user:password@host:port/dbname
            var uri = new Uri(databaseUrl);
            var connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Disable;Trust Server Certificate=true";

            services.AddDbContext<MindSetCoachDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
        }
        else
        {
            // Development - use SQLite
            services.AddDbContext<MindSetCoachDbContext>(options =>
            {
                options.UseSqlite("Data Source=mindsetcoach.db");
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
        }

        // Experiments database - always SQLite at ./data/experiments.db
        var experimentsDbPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "experiments.db");
        var experimentsDbDir = Path.GetDirectoryName(experimentsDbPath);
        if (!string.IsNullOrEmpty(experimentsDbDir) && !Directory.Exists(experimentsDbDir))
        {
            Directory.CreateDirectory(experimentsDbDir);
        }
        services.AddDbContext<ExperimentsDbContext>(options =>
            options.UseSqlite($"Data Source={experimentsDbPath}"));

        // Register services
        services.AddScoped<ContextExperimentLogger>();
        services.AddSingleton<IExperimentRunnerService, ExperimentRunnerService>();
        services.AddSingleton<IBatchExperimentService, BatchExperimentService>();
        services.AddScoped<IReportGeneratorService, ReportGeneratorService>();
        services.AddSingleton<IKernelFactory, KernelFactory>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<ICoachService, CoachService>();
        services.AddScoped<IClaimExtractorService, ClaimExtractorService>();

        // Register Semantic Kernel and AI services
        services.AddSemanticKernelServices(configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Ensure databases are initialized and migrations are applied.
    /// </summary>
    public static async Task EnsureDatabasesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var mainDb = scope.ServiceProvider.GetRequiredService<MindSetCoachDbContext>();
        await mainDb.Database.MigrateAsync();

        var experimentsDb = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        await experimentsDb.Database.MigrateAsync();
    }
}
