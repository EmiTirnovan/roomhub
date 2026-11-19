namespace MyApp.Domain.Common;

/// <summary>
/// Unit of Work contract residing in Domain/Application layer.
/// Controls the atomic persistence boundary across repositories.
/// Has zero dependency on Entity Framework Core.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

namespace MyApp.Domain.Events;

using MyApp.Domain.Common;

/// <summary>
/// Repository contract for the Event aggregate root.
/// Exposes strictly the operations required by business use cases.
/// Never exposes IQueryable or DbSet.
/// </summary>
public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Event?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default);
    Task AddAsync(Event ev, CancellationToken cancellationToken = default);
    void Remove(Event ev);
}

namespace MyApp.Domain.Participants;

using MyApp.Domain.Common;

/// <summary>
/// Repository contract for Participant aggregate/entity.
/// </summary>
public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Participant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(Participant participant, CancellationToken cancellationToken = default);
}
