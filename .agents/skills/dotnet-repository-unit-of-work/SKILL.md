---
name: dotnet-repository-unit-of-work
description: >-
  Implements Repository and Unit of Work patterns with Entity Framework Core and SQLite.
  Guides definition of aggregate-specific repository contracts, atomic transaction boundaries,
  clean asynchronous data access, and correct Scoped Dependency Injection lifetimes.
---

# Repository & Unit of Work Patterns in .NET 10

This skill defines the requirements, structure, and lifecycle rules for implementing the **Repository** and **Unit of Work** patterns using Entity Framework Core and SQLite in ASP.NET Core .NET 10 Minimal APIs. It ensures clean decoupling between Domain contracts and EF Core infrastructure.

---

## 1. Architectural Taxonomy & Core Concepts

Understanding the distinction between these 5 architectural components is vital:

| Concept | Role & Responsibility | Layer |
| :--- | :--- | :--- |
| **Repository** | Mediates between Domain and persistence. Mimics an in-memory collection of Aggregate Roots. Encapsulates query logic and hides EF Core specifics. | Interface in **Domain**; Implementation in **Infrastructure**. |
| **Unit of Work (`IUnitOfWork`)** | Controls the atomic commit boundary (`SaveChangesAsync`) across one or multiple repositories. Ensures all changes in a use case succeed or fail together. | Interface in **Domain/Application**; Implemented by DbContext in **Infrastructure**. |
| **`DbContext`** | EF Core's built-in combination of the Unit of Work and Identity Map patterns. Manages the physical database connection, mapping metadata, and query generation. | **Infrastructure** only. |
| **`ChangeTracker`** | The internal engine of `DbContext` that observes and tracks entity states (`Added`, `Modified`, `Unchanged`, `Deleted`). | Managed automatically by EF Core. |
| **`SaveChangesAsync`** | The execution trigger: inspects the `ChangeTracker`, generates optimized SQL statements (`INSERT`, `UPDATE`, `DELETE`), wraps them in a transaction, and dispatches them to SQLite. | Invoked strictly by the Handler via `IUnitOfWork`. |

---

## 2. Mandatory Rules of Implementation

1. **Domain Interfaces**: Repository interfaces (`IEventRepository`, `IParticipantRepository`) reside in the **Domain layer** alongside their Aggregate Roots.
2. **Zero EF Core Leakage**: Domain repository interfaces must never reference `Microsoft.EntityFrameworkCore`, `DbSet<T>`, or leak `IQueryable<T>`.
3. **Infrastructure Implementations**: Repositories (`EventRepository`, `ParticipantRepository`) and `CampusHubDbContext` reside in the **Infrastructure layer**.
4. **Use Case Focused**: Repositories only expose operations required by actual use cases. Never expose generic dump methods (`GetAll()`, `DeleteRange(Expression)`).
5. **Add Never Saves**: Repository `AddAsync` must only register the entity in the `ChangeTracker` (e.g. `await dbContext.Events.AddAsync(ev)`). **NEVER call `SaveChangesAsync()` inside a repository method!**
6. **IUnitOfWork Contract**: `IUnitOfWork` exposes `Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)`.
7. **DbContext as Unit of Work**: `CampusHubDbContext : DbContext, IUnitOfWork` implements `IUnitOfWork` directly.
8. **Single Scoped DbContext Instance**: Repositories and `IUnitOfWork` must share the exact same `Scoped` instance of `CampusHubDbContext` during an HTTP request.
9. **No Generic Repository Cargo-Culting**: Do not create `GenericRepository<T>` just to wrap `DbSet<T>`. It adds useless indirection and leaks query abstractions.
10. **Pragmatic Read Queries**: Do not force complex read-only queries into repositories if a lightweight query projection directly returning a DTO with `.AsNoTracking()` is cleaner.
11. **Always Propagate `CancellationToken`**: All async repository and UoW methods must accept and forward `CancellationToken`.

---

## 3. Dependency Injection Configuration (The Golden Rule)

In `Program.cs`, you must register `IUnitOfWork` so that it resolves the **exact same instance** of `CampusHubDbContext` used by the repositories:

```csharp
// 1. Register DbContext with Scoped lifetime
services.AddDbContext<CampusHubDbContext>(options =>
{
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=campushub.db");
});

// 2. FORWARD IUnitOfWork to the already registered CampusHubDbContext:
// ❌ WRONG: services.AddScoped<IUnitOfWork, CampusHubDbContext>(); // May create a second instance!
// ✅ CORRECT:
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CampusHubDbContext>());

// 3. Register Repositories as Scoped
services.AddScoped<IEventRepository, EventRepository>();
services.AddScoped<IParticipantRepository, ParticipantRepository>();
```

---

## 4. Complete Code Blueprints

### A. Domain Contracts (No EF Core Dependencies)
```csharp
namespace MyApp.Domain.Common;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

namespace MyApp.Domain.Events;

using MyApp.Domain.Common;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Event?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default);
    Task AddAsync(Event ev, CancellationToken cancellationToken = default);
    void Remove(Event ev);
}

namespace MyApp.Domain.Participants;

public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Participant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(Participant participant, CancellationToken cancellationToken = default);
}
```

---

### B. Infrastructure Implementations

#### CampusHubDbContext Implementing `IUnitOfWork`
```csharp
namespace MyApp.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Domain.Participants;

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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
```

#### Aggregate Repositories
```csharp
namespace MyApp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence;

public sealed class EventRepository(CampusHubDbContext dbContext) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Event?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default)
    {
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
        // Only registers in ChangeTracker; NEVER calls SaveChangesAsync!
        await dbContext.Events.AddAsync(ev, cancellationToken);
    }

    public void Remove(Event ev)
    {
        dbContext.Events.Remove(ev);
    }
}
```

---

## 5. Anti-Patterns to Reject

| Prohibited Anti-Pattern | Why It Breaks the Architecture | Correct Approach |
| :--- | :--- | :--- |
| **`SaveChangesAsync` in `AddAsync`** | Causes premature commits, destroys multi-entity transaction boundaries, and prevents rollback. | `AddAsync` only adds to `DbSet`. The Handler calls `IUnitOfWork.SaveChangesAsync`. |
| **DbContext as `Singleton`** | Causes concurrency exceptions and leaks cached entities across all user requests. | Always register `DbContext` as `Scoped`. |
| **Different DbContext instances in DI** | Repository modifies Instance A, but `IUnitOfWork.SaveChangesAsync` is called on Instance B, resulting in 0 rows saved! | Register `IUnitOfWork` via `sp => sp.GetRequiredService<CampusHubDbContext>()`. |
| **Leaking `IQueryable` from Repository** | `public IQueryable<Event> GetAll()` leaks SQL query building into handlers/controllers. | Materialize queries inside the repository and return `Task<Event?>` or `Task<IReadOnlyList<Event>>`. |
| **Generic Repository Over-Abstraction** | `IRepository<T>` with 40 methods adds useless boilerplate without adding domain value. | Use aggregate-focused repositories with explicit methods (`GetByIdAsync`, `ExistsByEmailAsync`). |
| **Missing `AsNoTracking` on Reads** | Causes unnecessary memory consumption and change tracker tracking for read-only queries. | Always apply `.AsNoTracking()` to read-only queries. |

---

## 6. Verification Checklist

- [ ] Are repository interfaces placed in Domain without any EF Core namespace imports?
- [ ] Are repository implementations placed in Infrastructure?
- [ ] Does `CampusHubDbContext` implement `IUnitOfWork`?
- [ ] Does `AddAsync` strictly mark entities without calling `SaveChangesAsync`?
- [ ] Does `IUnitOfWork` resolve the same `Scoped` `CampusHubDbContext` instance via factory lambda in DI?
- [ ] Are all async operations accepting and propagating `CancellationToken`?
- [ ] Does the solution build cleanly with zero warnings (`dotnet build --no-incremental`)?

---

## 7. Examples & References
- Domain interfaces: [DomainContracts.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-repository-unit-of-work/examples/DomainContracts.cs)
- Infrastructure implementations: [InfrastructureRepositories.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-repository-unit-of-work/examples/InfrastructureRepositories.cs)
- Dependency Injection setup: [DependencyInjectionSetup.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-repository-unit-of-work/examples/DependencyInjectionSetup.cs)
