using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Configurations;

namespace MyApp.Infrastructure.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddCampusHubPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Data Source=campushub.db";

        // DbContext registered with Scoped lifetime (default for AddDbContext)
        services.AddDbContext<CampusHubDbContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                // Configure migration assembly if separate project
                sqliteOptions.MigrationsAssembly(typeof(CampusHubDbContext).Assembly.FullName);
            });

            // Enable sensitive logging strictly in development if needed
            #if DEBUG
            options.EnableDetailedErrors();
            #endif
        });

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations at startup.
    /// Never call EnsureCreated if the project uses migrations!
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusHubDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CampusHubDbContext>>();

        try
        {
            logger.LogInformation("Applying pending EF Core migrations...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }
    }

    /// <summary>
    /// Idempotent data seeding limited to Development environment.
    /// Checks existence via AnyAsync before inserting.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusHubDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CampusHubDbContext>>();

        // Idempotent check using AnyAsync
        if (await db.Participants.AnyAsync())
        {
            logger.LogInformation("Database already seeded. Skipping seed.");
            return;
        }

        logger.LogInformation("Seeding development data...");

        var now = timeProvider.GetUtcNow();
        var participant = Participant.Create("Alex Popescu", "alex.popescu@campus.edu", now);
        await db.Participants.AddAsync(participant);

        var schedule = DateRange.Create(now.AddDays(7), now.AddDays(7).AddHours(3)).Value;
        var ev = Event.Create(
            "AI & .NET 10 Summit",
            "Deep dive into agentic systems and .NET 10 Minimal APIs.",
            schedule,
            100,
            timeProvider).Value;

        ev.Publish(timeProvider);
        await db.Events.AddAsync(ev);

        await db.SaveChangesAsync();
        logger.LogInformation("Development data seeded successfully.");
    }
}
