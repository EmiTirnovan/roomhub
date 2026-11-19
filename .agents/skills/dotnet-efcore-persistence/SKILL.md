---
name: dotnet-efcore-persistence
description: >-
  Configures Entity Framework Core with SQLite in .NET 10. Enforces clean domain persistence
  using IEntityTypeConfiguration<T>, complex property / value object conversions, navigation properties,
  clean schema migrations, and SQLite-specific considerations (data types, concurrency, transactions).
---

# EF Core Persistence with SQLite in .NET 10

This skill provides an authoritative guide for implementing data persistence using **Entity Framework Core** and **SQLite** in ASP.NET Core .NET 10 Minimal APIs. It ensures seamless mapping of Rich Domain Models without leaking infrastructure details into the domain or violating entity encapsulation.

---

## 1. Mandatory Architecture Rules

1. **Dedicated DbContext**: Create a focused DbContext (e.g. `CampusHubDbContext`) containing `DbSet<T>` for Aggregate Roots and persisted entities.
2. **One Configuration Class per Entity**: Every entity must implement its own `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/`.
3. **Clean `OnModelCreating`**: Never write inline table mappings inside `OnModelCreating`. Always invoke:
   ```csharp
   modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusHubDbContext).Assembly);
   ```
4. **Explicit Configuration**:
   - Primary Keys: Always declare `builder.HasKey(...)`.
   - Domain-Generated IDs: Use `.ValueGeneratedNever()` when IDs are created in domain factory methods (`Guid.NewGuid()`).
   - String Lengths: Specify `.HasMaxLength(...)` for all string properties.
   - Required Fields: Mark non-nullable columns with `.IsRequired()`.
   - Foreign Keys & Delete Behaviors: Explicitly specify `DeleteBehavior.Cascade` or `DeleteBehavior.Restrict`.
5. **Enum Storage**: Convert enums to strings using `.HasConversion<string>().HasMaxLength(30)` for human-readable SQLite storage and safe schema evolutions.
6. **Value Object Mapping**: Map Value Objects using `.ComplexProperty(...)` (.NET 8/9/10 standard) or `.HasConversion(...)` for single-value wrappers.
7. **SQLite UTC Time Handling**: SQLite stores dates as text. Use explicit `ValueConverter` for `DateTime` and `DateTimeOffset` to guarantee consistent ISO 8601 UTC storage and retrieval.
8. **Scoped Lifetime**: `DbContext` must always be registered as `Scoped` in the DI container.
9. **Async & CancellationTokens**: Every database query must use asynchronous methods (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`) with `CancellationToken`.
10. **Read Optimization**: Apply `.AsNoTracking()` to all query read operations.
11. **Existence Verification**: Use `.AnyAsync(predicate, cancellationToken)` rather than `CountAsync` or loading full entities when checking existence.
12. **Migration Discipline**:
    - Never use `EnsureCreated()` in a project that uses EF Core migrations.
    - Keep migrations strictly additive or backward-compatible.
    - Check .NET and EF Core package versions before adding any new package.
13. **Idempotent Seeding**: Data seeding must be idempotent (using `AnyAsync` before inserting) and restricted to the `Development` environment.

---

## 2. Model Configuration Checklist

| Element | Rule / Best Practice | SQLite Syntax / Example |
| :--- | :--- | :--- |
| **Primary Key** | Domain-generated GUID | `builder.Property(e => e.Id).ValueGeneratedNever();` |
| **Enum** | String conversion | `builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);` |
| **Value Object (Multi-field)** | ComplexProperty | `builder.ComplexProperty(e => e.Schedule, b => { ... });` |
| **Value Object (Single-field)** | HasConversion | `builder.Property(e => e.Email).HasConversion(e => e.Value, s => Email.Create(s).Value);` |
| **Backing Field Navigation** | Maintain encapsulation | `builder.Navigation(e => e.Registrations).UsePropertyAccessMode(PropertyAccessMode.Field);` |
| **Composite Unique Index** | Prevent duplicate relationships | `builder.HasIndex(r => new { r.EventId, r.AttendeeId }).IsUnique();` |
| **Foreign Key Delete** | Protect parent/child relationships | `builder.HasOne<Participant>().WithMany().OnDelete(DeleteBehavior.Restrict);` |

---

## 3. Reference Implementation

### A. Dedicated DbContext (`CampusHubDbContext.cs`)
```csharp
namespace MyApp.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Configurations;

public sealed class CampusHubDbContext(DbContextOptions<CampusHubDbContext> options)
    : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusHubDbContext).Assembly);
    }
}
```

---

### B. Entity Configuration with ComplexProperty & Backing Field (`EventConfiguration.cs`)
```csharp
namespace MyApp.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Converters;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.MaxCapacity).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // ComplexProperty for DateRange Value Object
        builder.ComplexProperty(e => e.Schedule, scheduleBuilder =>
        {
            scheduleBuilder.Property(s => s.StartUtc)
                .HasColumnName("ScheduleStartUtc")
                .HasConversion(new UtcDateTimeOffsetConverter())
                .IsRequired();

            scheduleBuilder.Property(s => s.EndUtc)
                .HasColumnName("ScheduleEndUtc")
                .HasConversion(new UtcDateTimeOffsetConverter())
                .IsRequired();
        });

        builder.Property(e => e.CreatedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired();

        // Encapsulation: Bind navigation to private backing field _registrations
        builder.HasMany(e => e.Registrations)
            .WithOne()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Registrations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.Title);
        builder.HasIndex(e => e.Status);
    }
}
```

---

### C. Unique Composite Index & Foreign Keys (`EventRegistrationConfiguration.cs`)
```csharp
public sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> builder)
    {
        builder.ToTable("EventRegistrations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AttendeeEmail).HasMaxLength(256).IsRequired();
        builder.Property(r => r.RegisteredAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired();

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Unique Index
        builder.HasIndex(r => new { r.EventId, r.AttendeeId }).IsUnique();
    }
}
```

---

### D. UTC DateTime Converters for SQLite
```csharp
namespace MyApp.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(v => v.ToUniversalTime(), v => v.ToUniversalTime()) { }
}
```

---

### E. SQLite Configuration, Migrations & Idempotent Seeding in `Program.cs`
```csharp
// 1. Service Registration (Scoped)
builder.Services.AddDbContext<CampusHubDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=campushub.db");
});

// 2. Programmatic Migration Execution at Startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CampusHubDbContext>();
    await db.Database.MigrateAsync();
}

// 3. Idempotent Development Seed
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CampusHubDbContext>();

    // Idempotent guard clause using AnyAsync
    if (!await db.Participants.AnyAsync())
    {
        var participant = Participant.Create("Alex Popescu", "alex@campus.edu", DateTimeOffset.UtcNow);
        await db.Participants.AddAsync(participant);
        await db.SaveChangesAsync();
    }
}
```

---

## 4. EF Core Migration Commands

Execute migrations using the .NET CLI:

```powershell
# Add a new migration
dotnet ef migrations add InitialCampusHubSchema -p src/MyApp.Infrastructure -s src/MyApp.Web

# Update database
dotnet ef database update -p src/MyApp.Infrastructure -s src/MyApp.Web

# Generate SQL script for verification
dotnet ef migrations script -p src/MyApp.Infrastructure -s src/MyApp.Web
```

---

## 5. Anti-Patterns to Reject

- ❌ **Bloated DbContext**: Writing dozens of `builder.Entity<Event>(...)` configurations directly inside `OnModelCreating`.
- ❌ **`EnsureCreated` with Migrations**: Calling `EnsureCreatedAsync` in a solution containing a `Migrations/` directory.
- ❌ **Unbounded Strings**: Omitting `HasMaxLength` leading to arbitrary text storage in SQLite.
- ❌ **Missing UTC Converters**: Storing local timestamps in SQLite that cause comparison and parsing bugs in queries.
- ❌ **Synchronous DB Calls**: Using `ToList()`, `Find()`, or `SaveChanges()` instead of their async equivalents with `CancellationToken`.
- ❌ **Weakening Encapsulation for EF**: Making private setters `public` because of a mistaken assumption that EF Core requires them.

---

## 6. Examples & References
- Complete DbContext: [CampusHubDbContext.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-efcore-persistence/examples/CampusHubDbContext.cs)
- Event configuration with ComplexProperty: [EventConfiguration.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-efcore-persistence/examples/EventConfiguration.cs)
- Participant configuration: [ParticipantConfiguration.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-efcore-persistence/examples/ParticipantConfiguration.cs)
- UTC Converters: [UtcDateTimeConverters.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-efcore-persistence/examples/UtcDateTimeConverters.cs)
- Startup registration, migrations & idempotent seed: [ProgramSqliteConfiguration.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-efcore-persistence/examples/ProgramSqliteConfiguration.cs)
