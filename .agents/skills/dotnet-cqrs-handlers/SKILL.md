---
name: dotnet-cqrs-handlers
description: >-
  Implements clean Command Query Responsibility Segregation (CQRS) without external libraries like MediatR.
  Guides definition of Command/Query records, custom handler interfaces (ICommandHandler, IQueryHandler),
  dependency injection registration with assembly scanning, CancellationToken propagation, and orchestration without duplicating business logic.
---

# Custom CQRS Commands & Handlers (Without MediatR) in .NET 10

This skill provides comprehensive instructions for implementing the **Command Query Responsibility Segregation (CQRS)** pattern for state-mutating operations in ASP.NET Core .NET 10 Minimal APIs using pure C# constructs, custom handler contracts, and built-in Dependency Injection, strictly without MediatR.

---

## 1. Core Architectural Contracts

State-mutating operations are modeled through distinct message contracts (`ICommand`) and dedicated handlers (`ICommandHandler`). Operations without a value result use the `Unit` type:

```csharp
namespace MyApp.Application.Common.Cqrs;

using MyApp.Domain.Common;

/// <summary>
/// Unit type representing the absence of a value result in generic signatures.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

/// <summary>
/// Marker contract for a Command that returns a value of type TResult.
/// </summary>
public interface ICommand<TResult>;

/// <summary>
/// Marker contract for a Command returning Unit (no payload).
/// </summary>
public interface ICommand : ICommand<Unit>;

/// <summary>
/// Custom Handler interface executing a Command.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Shortcut contract for Handlers returning Unit.
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand;
```

---

## 2. Mandatory Rules of Handler Orchestration

1. **Business Intent**: Each `Command` is an immutable record capturing the intent and parameters required for that specific use case.
2. **Dedicated Handler per Command**: Every `Command` has exactly one dedicated `Handler`. Never create a generic mega-handler that handles multiple unrelated commands.
3. **Orchestration Only**:
   - Loads aggregate root(s) via Repository.
   - Verifies the existence of required resources (returns `Error.NotFound` if missing).
   - Invokes Rich Domain methods on the aggregate.
   - Saves changes via `IUnitOfWork.SaveChangesAsync(cancellationToken)`.
   - Returns a `Result<TResult>` or `Result<Unit>`.
4. **Domain Invariant Boundary**: Business validation rules (e.g., status checks, capacity limits, date validity) **remain exclusively in the Domain**. Do NOT duplicate these checks in the Handler.
5. **Zero HTTP Coupling**: Handlers know nothing about HTTP, query strings, headers, route values, status codes, or `HttpContext`.
6. **No TypedResults in Handlers**: Handlers must never return `IResult`, `TypedResults`, or `IActionResult`. They return `Result<T>` or `Result`.
7. **Result over Exceptions**: Business rejections return `Result.Failure(Error)`. Never throw business exceptions.
8. **Repository & Unit of Work**: Use repository contracts for loading/adding and `IUnitOfWork` for committing transactions.
9. **CancellationToken Propagation**: Every asynchronous method call must receive and forward `CancellationToken`.
10. **Single Save Boundary**: Call `unitOfWork.SaveChangesAsync` once at the end of the operation. Never call save multiple times or prematurely before domain operations finish.
11. **No MediatR**: Never add `MediatR` NuGet packages or use `IMediator`. Handlers are invoked directly or through typed DI resolution.
12. **Pragmatic Read Side**: Do not create Query handlers unless requested; queries can project directly to DTOs using `.AsNoTracking()`.

---

## 3. Dynamic Assembly Scanning Registration (`AddHandlers`)

To avoid manual `services.AddScoped` calls for every handler, implement reflection-based registration scanning assemblies at startup:

```csharp
namespace MyApp.Application.Common.DependencyInjection;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Application.Common.Cqrs;

public static class CqrsRegistrationExtensions
{
    public static IServiceCollection AddHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetExecutingAssembly()];
        }

        var handlerInterfaceType = typeof(ICommandHandler<,>);

        foreach (var assembly in assemblies)
        {
            // 1. Filter: concrete classes only (not abstract, not open generic)
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

            foreach (var type in handlerTypes)
            {
                var interfaces = type.GetInterfaces();

                foreach (var iface in interfaces)
                {
                    // 2. Match generic interface definition ICommandHandler<,>
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == handlerInterfaceType)
                    {
                        // 3. Register under interface with Scoped lifetime
                        services.TryAddScoped(iface, type);

                        // 4. Also register concrete type for direct parameter injection in Minimal APIs
                        services.TryAddScoped(type);
                    }
                }
            }
        }

        return services;
    }
}
```

In `Program.cs`:
```csharp
builder.Services.AddHandlers(typeof(PublishEventHandler).Assembly);
```

---

## 4. Mandatory Concrete Implementations

### A. Publish Event (`PublishEventCommand` + `PublishEventHandler`)
```csharp
public sealed record PublishEventCommand(Guid EventId) : ICommand;

public sealed class PublishEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PublishEventCommand>
{
    public async Task<Result<Unit>> Handle(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<Unit>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        var publishResult = ev.Publish(timeProvider);
        if (publishResult.IsFailure)
        {
            return Result<Unit>.Failure(publishResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

---

### B. Cancel Event (`CancelEventCommand` + `CancelEventHandler`)
```csharp
public sealed record CancelEventCommand(Guid EventId, string Reason) : ICommand;

public sealed class CancelEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelEventCommand>
{
    public async Task<Result<Unit>> Handle(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<Unit>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        var cancelResult = ev.Cancel(command.Reason, timeProvider);
        if (cancelResult.IsFailure)
        {
            return Result<Unit>.Failure(cancelResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

---

### C. Register For Event (`RegisterForEventCommand` + `RegisterForEventHandler`)

Follow the strict 6-step flow without duplicating domain invariants:

```csharp
public sealed record RegisterForEventCommand(
    Guid EventId,
    Guid ParticipantId) : ICommand<RegistrationResultDto>;

public sealed record RegistrationResultDto(
    Guid RegistrationId,
    Guid EventId,
    Guid ParticipantId,
    DateTimeOffset RegisteredAtUtc);

public sealed class RegisterForEventHandler(
    IEventRepository eventRepository,
    IParticipantRepository participantRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RegisterForEventCommand, RegistrationResultDto>
{
    public async Task<Result<RegistrationResultDto>> Handle(
        RegisterForEventCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load Event
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<RegistrationResultDto>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        // 2. Verify Participant existence
        var participant = await participantRepository.GetByIdAsync(command.ParticipantId, cancellationToken);
        if (participant is null)
        {
            return Result<RegistrationResultDto>.Failure(
                Error.NotFound("Participant.NotFound", $"Participant '{command.ParticipantId}' was not found."));
        }

        // 3. Delegate execution to Domain (never check capacity or status here!)
        var registrationResult = ev.Register(participant.Id, participant.Email, timeProvider);

        // 4. Check domain Result
        if (registrationResult.IsFailure)
        {
            return Result<RegistrationResultDto>.Failure(registrationResult.Error);
        }

        // 5. Persist atomically
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Return response contract
        var reg = registrationResult.Value;
        return Result<RegistrationResultDto>.Success(new RegistrationResultDto(
            reg.Id,
            reg.EventId,
            reg.AttendeeId,
            reg.RegisteredAtUtc));
    }
}
```

---

## 5. Comprehensive Unit Testing Strategy

For every command handler, test all execution paths using mocks for repositories/UoW and `FakeTimeProvider`:

| Test Scenario | Condition / Mock Setup | Expected Result | `SaveChangesAsync` Called? |
| :--- | :--- | :--- | :--- |
| **Event Not Found** | `eventRepo.GetByIdAsync` returns `null` | `ErrorType.NotFound` | ❌ No |
| **Participant Not Found** | `participantRepo.GetByIdAsync` returns `null` | `ErrorType.NotFound` | ❌ No |
| **Event Not Published** | `ev.Status == EventStatus.Draft` | `ErrorType.Conflict` | ❌ No |
| **Event Already Started** | `nowUtc >= ev.Schedule.StartUtc` | `ErrorType.Conflict` | ❌ No |
| **Capacity Exhausted** | `ev.Registrations.Count >= ev.MaxCapacity` | `ErrorType.Conflict` | ❌ No |
| **Duplicate Registration** | Same `participantId` already registered | `ErrorType.Conflict` | ❌ No |
| **Happy Path Success** | Valid event, valid participant, capacity available | `Result.IsSuccess == true` | ✅ Yes (1 time) |

---

## 6. Anti-Patterns to Reject

- ❌ **Introducing MediatR**: Adding `MediatR` package. (Forbidden in this architecture).
- ❌ **Duplicate Domain Logic in Handlers**: Writing `if (ev.Registrations.Count >= ev.MaxCapacity)` inside `RegisterForEventHandler`.
- ❌ **HTTP Elements in Handler**: Injecting `IHttpContextAccessor`, reading headers, or returning `TypedResults`.
- ❌ **Premature Saves**: Calling `unitOfWork.SaveChangesAsync()` before calling domain methods.
- ❌ **Generic Mega Handlers**: Creating a single `CommandHandler<T>` with reflection or switch statements handling multiple commands.

---

## 7. Examples & References
- Complete contracts: [CqrsCoreContracts.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-cqrs-handlers/examples/CqrsCoreContracts.cs)
- Dynamic assembly scanning registration: [AssemblyScannerRegistration.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-cqrs-handlers/examples/AssemblyScannerRegistration.cs)
- Concrete Command Handlers: [EventCommandHandlers.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-cqrs-handlers/examples/EventCommandHandlers.cs)
- Complete Handler Unit Tests (7 scenarios): [RegisterForEventHandlerTests.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-cqrs-handlers/examples/RegisterForEventHandlerTests.cs)
