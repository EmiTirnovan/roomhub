using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Domain.Participants;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Infrastructure.Persistence;

public static class RepositoryRegistrationExtensions
{
    public static IServiceCollection AddRepositoriesAndUnitOfWork(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Register DbContext with Scoped lifetime (default in EF Core)
        services.AddDbContext<CampusHubDbContext>(options =>
        {
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=campushub.db");
        });

        // 2. CRITICAL: Register IUnitOfWork to resolve the EXACT SAME Scoped instance of CampusHubDbContext.
        // DO NOT write: services.AddScoped<IUnitOfWork, CampusHubDbContext>(); 
        // because that might create a second distinct instance if resolved differently.
        // Forwarding via factory method guarantees single shared instance per request.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CampusHubDbContext>());

        // 3. Register Aggregate Repositories as Scoped (they inject CampusHubDbContext)
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();

        return services;
    }
}
