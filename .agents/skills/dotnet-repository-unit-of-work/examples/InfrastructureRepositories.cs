using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Domain.Participants;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// Dedicated DbContext implementing IUnitOfWork directly.
/// This guarantees that IUnitOfWork and Repositories share the exact same
/// DbContext change tracker and database transaction boundary.
/// </summary>
public sealed class CampusHubDbContext(DbContextOptions<CampusHubDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participant> Participants => Set<Participant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusHubDbContext).Assembly);
    }

    // Explicit IUnitOfWork implementation forwarding to base EF Core SaveChangesAsync
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}

namespace MyApp.Infrastructure.Persistence.Repositories;

using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence;

public sealed class EventRepository(CampusHubDbContext dbContext) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked entity query: required for state-mutating operations
        return await dbContext.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Event?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // AsNoTracking query: avoids ChangeTracker overhead for read-only operations
        return await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await dbContext.Events
            .AnyAsync(e => e.Title == title, cancellationToken);
    }

    public async Task AddAsync(Event ev, CancellationToken cancellationToken = default)
    {
        // Strictly marks entity for addition in the ChangeTracker.
        // NEVER call SaveChangesAsync here!
        await dbContext.Events.AddAsync(ev, cancellationToken);
    }

    public void Remove(Event ev)
    {
        dbContext.Events.Remove(ev);
    }
}

namespace MyApp.Infrastructure.Persistence.Repositories;

using MyApp.Domain.Participants;
using MyApp.Infrastructure.Persistence;

public sealed class ParticipantRepository(CampusHubDbContext dbContext) : IParticipantRepository
{
    public async Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Participant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Participants
            .AnyAsync(p => p.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(Participant participant, CancellationToken cancellationToken = default)
    {
        // Only registers with ChangeTracker as EntityState.Added
        await dbContext.Participants.AddAsync(participant, cancellationToken);
    }
}
