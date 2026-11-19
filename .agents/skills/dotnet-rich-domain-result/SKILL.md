---
name: dotnet-rich-domain-result
description: >-
  Architects Rich Domain Models, encapsulates business invariants, designs Value Objects
  using records, and models domain failures with Result<T> and Error patterns in .NET 10.
  Integrates TimeProvider for deterministic time operations without throwing business exceptions.
---

# Rich Domain Model & Result<T> Pattern in .NET 10

This skill provides comprehensive instructions for designing a **Rich Domain Model** that protects invariants and encapsulates business logic in C# and .NET 10, replacing anemic models and business exceptions with expressive domain methods and the `Result<T>` / `Error` pattern.

---

## 1. Rich Domain vs. Anemic Domain Model

### What is an Anemic Domain Model?
An anemic model treats domain classes as passive data bags (DTOs) with public getters and setters. All business rules, validation checks, and state transitions are scattered across endpoints, services, or handlers.
- **Flaws**: Any caller can mutate state arbitrarily (e.g. `order.Status = OrderStatus.Completed` without paying), invariants are repeatedly checked or forgotten, and testing requires mocking extensive external service layers.

### What is a Rich Domain Model?
A rich model combines **state and behavior**. Entities strictly control their internal state:
- All property setters are `private set;` (or backed by private fields).
- External callers cannot place the entity into an invalid state.
- Invariants are verified upon creation via static factory methods (`Create`) returning `Result<T>`.
- State transitions are executed through explicit methods using domain vocabulary (`Publish()`, `Register()`, `Cancel()`).

```text
Anemic Domain Model (Anti-Pattern):
[Endpoint/Service] ──writes directly──> Order { public Status { get; set; } }

Rich Domain Model (Compliant):
[Endpoint] ──calls──> [Handler] ──calls──> Order.Cancel(timeProvider, reason)
                                                └── enforces invariants internally
```

---

## 2. Mandatory Rules of Invariant Encapsulation

1. **State Control**: Entities must have complete authority over their own state.
2. **Private Constructors**: Use `private` (or `protected`) parameterless constructors for EF Core materialization, preventing unchecked initialization via `new Entity()`.
3. **Private Setters and Backing Fields**:
   - Scalar properties: `public string Title { get; private set; }`
   - Collections: Use private backing fields with read-only exposure:
     ```csharp
     private readonly List<EventRegistration> _registrations = [];
     public IReadOnlyCollection<EventRegistration> Registrations => _registrations.AsReadOnly();
     ```
4. **Static Factory Methods**: Use static factory methods returning `Result<TEntity>` (e.g., `Event.Create(...)`).
5. **Domain-Centric Validation**: Invariants (e.g., capacity > 0, dates in the future) are validated inside the entity or Value Object, never in endpoints or handlers.
6. **Domain Language (Ubiquitous Language)**: Business methods must be named after business actions (`Publish`, `Cancel`, `Register`, `Complete`), not CRUD terms like `UpdateStatus` or `SetCancelled`.
7. **No Invalid Transitions**: Enforce strict status lifecycle checks (e.g., a cancelled event cannot be published).
8. **Value Objects**: Encapsulate concepts with their own validation rules into immutable records (e.g., `DateRange`, `Money`, `Email`).
9. **TimeProvider Integration**: Inject `TimeProvider` into domain methods or handlers. Never call `DateTime.UtcNow` or `DateTime.Now` directly.

> [!IMPORTANT]
> **Starter Project Adaptability**:
> Always inspect the starter project before introducing types. If the project already contains a compatible `Result<T>`, `Result`, or `Error` implementation, **reuse and adapt to it**. Do not introduce competing result abstractions.

---

## 3. The Result<T> and Error Pattern

### Error Hierarchy & Semantic ErrorTypes
Erorrs must define a stable `Code`, human-readable `Message`, and an `ErrorType` to allow the HTTP layer to automatically determine the proper status code:

```csharp
namespace MyApp.Domain.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,   // HTTP 400 Bad Request
    NotFound = 2,     // HTTP 404 Not Found
    Conflict = 3,     // HTTP 409 Conflict
    Unauthorized = 4  // HTTP 401 Unauthorized / 403 Forbidden
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
```

### Result Types (`Result` & `Result<T>`)
- Expose `IsSuccess` and `IsFailure`.
- Prevent unhandled access: accessing `.Value` when `IsFailure == true` must throw `InvalidOperationException`.
- Use early returns: check `if (result.IsFailure) return result;`.
- **Zero Business Exceptions**: Never throw exceptions for normal business rule rejections. Exceptions are reserved strictly for unrecoverable infrastructure faults (DB connection dead, out of memory).

```csharp
namespace MyApp.Domain.Common;

public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failure result.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public new static Result<TValue> Failure(Error error) => new(default, false, error);
}
```

---

## 4. Value Objects: Complete `DateRange` Reference

Value Objects have no identity and are defined by their attributes. They must be immutable records.

```csharp
namespace MyApp.Domain.Common;

public sealed record DateRange
{
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    private DateRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public static Result<DateRange> Create(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (startUtc == default)
            return Result<DateRange>.Failure(Error.Validation("DateRange.StartDefault", "Start date cannot be default."));

        if (endUtc == default)
            return Result<DateRange>.Failure(Error.Validation("DateRange.EndDefault", "End date cannot be default."));

        if (startUtc >= endUtc)
            return Result<DateRange>.Failure(Error.Validation("DateRange.InvalidInterval", "Start date must be strictly earlier than end date."));

        return Result<DateRange>.Success(new DateRange(startUtc, endUtc));
    }

    public bool Overlaps(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // Half-open interval [Start, End): Overlaps if Start < other.End && End > other.Start
        return StartUtc < other.EndUtc && EndUtc > other.StartUtc;
    }

    public bool Contains(DateTimeOffset timestampUtc) =>
        timestampUtc >= StartUtc && timestampUtc < EndUtc;
}
```

---

## 5. Complete Rich Aggregate Example: `Event`

Demonstrates `Create`, `Publish`, `Cancel`, `Register`, invariant protection, and `TimeProvider`:

```csharp
namespace MyApp.Domain.Events;

using MyApp.Domain.Common;

public enum EventStatus
{
    Draft = 1,
    Published = 2,
    Cancelled = 3,
    Completed = 4
}

public sealed class Event
{
    private readonly List<EventRegistration> _registrations = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DateRange Schedule { get; private set; } = null!;
    public int MaxCapacity { get; private set; }
    public EventStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<EventRegistration> Registrations => _registrations.AsReadOnly();

    private Event() { }

    // 1. Factory Method: Event.Create
    public static Result<Event> Create(
        string title,
        string description,
        DateRange schedule,
        int maxCapacity,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(title))
            return Result<Event>.Failure(Error.Validation("Event.TitleEmpty", "Event title cannot be empty."));

        if (maxCapacity <= 0)
            return Result<Event>.Failure(Error.Validation("Event.InvalidCapacity", "Capacity must be greater than zero."));

        var nowUtc = timeProvider.GetUtcNow();
        if (schedule.StartUtc <= nowUtc)
            return Result<Event>.Failure(Error.Validation("Event.PastSchedule", "Cannot schedule an event in the past."));

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Schedule = schedule,
            MaxCapacity = maxCapacity,
            Status = EventStatus.Draft,
            CreatedAtUtc = nowUtc
        };

        return Result<Event>.Success(ev);
    }

    // 2. Business Method: Event.Publish
    public Result Publish(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == EventStatus.Published)
            return Result.Failure(Error.Conflict("Event.AlreadyPublished", "The event is already published."));

        if (Status == EventStatus.Cancelled)
            return Result.Failure(Error.Conflict("Event.CancelledCannotPublish", "Cancelled events cannot be published."));

        var nowUtc = timeProvider.GetUtcNow();
        if (Schedule.StartUtc <= nowUtc)
            return Result.Failure(Error.Conflict("Event.StartInPast", "Cannot publish an event whose start date has passed."));

        Status = EventStatus.Published;
        PublishedAtUtc = nowUtc;
        return Result.Success();
    }

    // 3. Business Method: Event.Register
    public Result<EventRegistration> Register(
        Guid attendeeId,
        string attendeeEmail,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != EventStatus.Published)
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.NotOpen", $"Cannot register for event in status '{Status}'."));

        var nowUtc = timeProvider.GetUtcNow();
        if (Schedule.StartUtc <= nowUtc)
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.AlreadyStarted", "Registration closed because event has already started."));

        if (_registrations.Count >= MaxCapacity)
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.SoldOut", "Event capacity has been reached."));

        if (_registrations.Any(r => r.AttendeeId == attendeeId || r.AttendeeEmail.Equals(attendeeEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.AlreadyRegistered", "Attendee is already registered."));

        var registrationResult = EventRegistration.Create(Id, attendeeId, attendeeEmail, nowUtc);
        if (registrationResult.IsFailure) return registrationResult;

        _registrations.Add(registrationResult.Value);
        return registrationResult;
    }

    // 4. Business Method: Event.Cancel
    public Result Cancel(string reason, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == EventStatus.Cancelled)
            return Result.Failure(Error.Conflict("Event.AlreadyCancelled", "The event is already cancelled."));

        if (Status == EventStatus.Completed)
            return Result.Failure(Error.Conflict("Event.AlreadyCompleted", "Completed events cannot be cancelled."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("Event.CancelReasonRequired", "A cancellation reason must be provided."));

        Status = EventStatus.Cancelled;
        CancelledAtUtc = timeProvider.GetUtcNow();
        CancellationReason = reason.Trim();
        return Result.Success();
    }
}
```

---

## 6. Translating Errors to RFC 7807 ProblemDetails

Map domain errors cleanly to `ProblemHttpResult` in the presentation layer:

```csharp
namespace MyApp.Features.Common;

public static class ProblemDetailsExtensions
{
    public static ProblemHttpResult ToProblemDetails(this Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error")
        };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }
}
```

---

## 7. Anti-Patterns to Reject Immediately

| Anti-Pattern | Why It Breaks the Architecture | Correct Solution |
| :--- | :--- | :--- |
| `public string Status { get; set; }` | Allows any caller to corrupt entity lifecycle. | `public EventStatus Status { get; private set; }` + business methods (`Publish`, `Cancel`). |
| Invariant checks in Endpoint | Endpoints are easily bypassed; invariants are duplicated across routes. | Enforce all rules in `Event.Create` or Value Objects. |
| Business rules in Handler | Turns the domain model anemic; makes unit testing require full handler mocks. | Put rules on the Aggregate Root; handler only orchestrates. |
| `throw new Exception("Event sold out");` | Exceptions are expensive and indicate runtime failures, not business logic paths. | `return Result.Failure(Error.Conflict(...));` |
| `DateTime.UtcNow` hardcoded | Prevents deterministic testing of time-based domain rules. | Inject `TimeProvider` and call `timeProvider.GetUtcNow()`. |
| Modifying status directly outside entity | `ev.Status = EventStatus.Cancelled;` breaks invariants (e.g. timestamp not set). | `ev.Cancel(reason, timeProvider);` |

---

## 8. Verification Checklist

Before approving any domain changes:
- [ ] Are all entity property setters `private` or `init`?
- [ ] Are collections encapsulated with private backing fields and `IReadOnlyCollection`?
- [ ] Does `Create` return `Result<TEntity>` with invariant guard clauses?
- [ ] Are all state transitions handled by domain methods named with ubiquitous vocabulary?
- [ ] Are Value Objects immutable `record` types with validated factory methods?
- [ ] Is `DateRange` validated and equipped with `Overlaps`?
- [ ] Is `TimeProvider` injected for all time-dependent logic?
- [ ] Does the solution compile cleanly with zero warnings?

---

## 9. Reference Code
- [Complete Event Aggregate](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-rich-domain-result/examples/EventAggregate.cs)
- [DateRange Value Object](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-rich-domain-result/examples/DateRange.cs)
- [Result and Error Definitions](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-rich-domain-result/examples/ResultAndError.cs)
- [ProblemDetails Mapping](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-rich-domain-result/examples/ProblemDetailsMapping.cs)
