using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Configurations;

namespace MyApp.Infrastructure.Persistence;

public sealed class CampusHubDbContext(DbContextOptions<CampusHubDbContext> options)
    : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keep OnModelCreating clean: automatically discover all IEntityTypeConfiguration implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusHubDbContext).Assembly);
    }
}
