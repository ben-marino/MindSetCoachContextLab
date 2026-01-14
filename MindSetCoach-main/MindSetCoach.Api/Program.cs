using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Services;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;
using MindSetCoach.Api.Configuration;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add console logging immediately
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);

    // Log startup info
    Console.WriteLine("=== MINDSETCOACH STARTUP ===");
    Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"DATABASE_URL exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))}");
    Console.WriteLine($"JWT_KEY exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_KEY"))}");
    Console.WriteLine($"PORT: {Environment.GetEnvironmentVariable("PORT")}");
    Console.WriteLine("============================");

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MindSetCoach API",
            Version = "v1",
            Description = "Mental training platform for athletes and coaches"
        });

        // Add JWT Authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Database configuration
    if (builder.Environment.IsProduction())
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            // Parse Fly.io DATABASE_URL format: postgres://user:password@host:port/dbname
            var uri = new Uri(databaseUrl);
            var connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Disable;Trust Server Certificate=true";

            builder.Services.AddDbContext<MindSetCoachDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                // Suppress the pending model changes warning in production
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
        }
    }
    else
    {
        // Development - use SQLite
        builder.Services.AddDbContext<MindSetCoachDbContext>(options =>
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
    builder.Services.AddDbContext<ExperimentsDbContext>(options =>
        options.UseSqlite($"Data Source={experimentsDbPath}"));

    // Register ContextExperimentLogger as scoped (needs DbContext)
    builder.Services.AddScoped<ContextExperimentLogger>();

    // Register application services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IJournalService, JournalService>();
    builder.Services.AddScoped<ICoachService, CoachService>();
    builder.Services.AddScoped<IClaimExtractorService, ClaimExtractorService>();

    // Register Semantic Kernel and AI services
    builder.Services.AddSemanticKernelServices(builder.Configuration);

    // Configure CORS based on environment
    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: Allow all origins for local testing
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        }
        else
        {
            // Production: Allow specific origins
            var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',')
                ?? new[] { "*" }; // Fallback to allow all if not configured

            options.AddPolicy("AllowAll", policy =>
            {
                if (allowedOrigins.Contains("*"))
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowCredentials();
                }
                policy.AllowAnyMethod()
                      .AllowAnyHeader();
            });
        }
    });

    // Configure JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MindSetCoach";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MindSetCoachUsers";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Apply migrations automatically on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MindSetCoachDbContext>();
        db.Database.Migrate();

        // Apply experiments database migrations
        var experimentsDb = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();
        experimentsDb.Database.Migrate();
    }

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        // Production: Use HSTS and generic error handling
        app.UseHsts();
        app.UseExceptionHandler("/error");
    }

    // Enable Swagger in all environments (for now, can restrict later)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MindsetCoach API v1");
        c.RoutePrefix = "swagger";
    });

    app.UseHttpsRedirection();

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Root endpoint
    app.MapGet("/", () => Results.Ok(new {
        message = "MindsetCoach API is running!",
        swagger = "/swagger",
        health = "/health"
    }));

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
       .WithName("HealthCheck")
       .WithOpenApi();

    Console.WriteLine("=== STARTING APPLICATION ===");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("=== FATAL STARTUP ERROR ===");
    Console.WriteLine($"Type: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        Console.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
    }
    Console.WriteLine("===========================");
    throw;
}
