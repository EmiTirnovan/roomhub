namespace MyApp.Domain.Events;

using MyApp.Domain.Common;

public enum EventStatus
{
    Draft = 1,
    Published = 2,
    Cancelled = 3,
    Completed = 4
}

public sealed class EventRegistration
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid AttendeeId { get; private set; }
    public string AttendeeEmail { get; private set; } = null!;
    public DateTimeOffset RegisteredAtUtc { get; private set; }

    private EventRegistration() { }

    internal static Result<EventRegistration> Create(
        Guid eventId,
        Guid attendeeId,
        string attendeeEmail,
        DateTimeOffset registeredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(attendeeEmail) || !attendeeEmail.Contains('@'))
        {
            return Result<EventRegistration>.Failure(
                Error.Validation("Registration.InvalidEmail", "Attendee email is invalid."));
        }

        return Result<EventRegistration>.Success(new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            AttendeeId = attendeeId,
            AttendeeEmail = attendeeEmail.Trim().ToLowerInvariant(),
            RegisteredAtUtc = registeredAtUtc
        });
    }
}

/// <summary>
/// Rich Domain Aggregate Root for Event management.
/// Invariants are strictly guarded via private setters, backing collections,
/// and business methods using ubiquitous domain terms.
/// </summary>
public sealed class Event
{
    // Backing field for registrations collection to prevent external tampering
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

    // Read-only access to encapsulated registrations
    public IReadOnlyCollection<EventRegistration> Registrations => _registrations.AsReadOnly();

    // Required for EF Core materialization without violating encapsulation
    private Event() { }

    /// <summary>
    /// Factory Method: Encapsulates initial creation invariants.
    /// </summary>
    public static Result<Event> Create(
        string title,
        string description,
        DateRange schedule,
        int maxCapacity,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<Event>.Failure(
                Error.Validation("Event.TitleEmpty", "Event title cannot be empty."));
        }

        if (maxCapacity <= 0)
        {
            return Result<Event>.Failure(
                Error.Validation("Event.InvalidCapacity", "Event capacity must be greater than zero."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        if (schedule.StartUtc <= nowUtc)
        {
            return Result<Event>.Failure(
                Error.Validation("Event.PastSchedule", "Cannot schedule an event in the past."));
        }

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

    /// <summary>
    /// Business Method: Publishes a draft event, making it open for registrations.
    /// Invariant: Only Draft events with a future start time can be published.
    /// </summary>
    public Result Publish(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == EventStatus.Published)
        {
            return Result.Failure(
                Error.Conflict("Event.AlreadyPublished", "The event is already published."));
        }

        if (Status == EventStatus.Cancelled)
        {
            return Result.Failure(
                Error.Conflict("Event.CancelledCannotPublish", "Cancelled events cannot be published."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        if (Schedule.StartUtc <= nowUtc)
        {
            return Result.Failure(
                Error.Conflict("Event.StartInPast", "Cannot publish an event whose start date has passed."));
        }

        Status = EventStatus.Published;
        PublishedAtUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>
    /// Business Method: Registers an attendee to the event.
    /// Invariants:
    /// - Event must be in Published status.
    /// - Cannot register after the event has started.
    /// - Cannot exceed MaxCapacity.
    /// - Cannot register the same attendee twice.
    /// </summary>
    public Result<EventRegistration> Register(
        Guid attendeeId,
        string attendeeEmail,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != EventStatus.Published)
        {
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.NotOpenForRegistration", $"Cannot register for an event with status '{Status}'."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        if (Schedule.StartUtc <= nowUtc)
        {
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.AlreadyStarted", "Registration has closed because the event already started."));
        }

        if (_registrations.Count >= MaxCapacity)
        {
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.SoldOut", "Event capacity has been reached."));
        }

        if (_registrations.Any(r => r.AttendeeId == attendeeId || r.AttendeeEmail.Equals(attendeeEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result<EventRegistration>.Failure(
                Error.Conflict("Event.AlreadyRegistered", "Attendee is already registered for this event."));
        }

        var registrationResult = EventRegistration.Create(Id, attendeeId, attendeeEmail, nowUtc);
        if (registrationResult.IsFailure)
        {
            return registrationResult;
        }

        _registrations.Add(registrationResult.Value);

        return registrationResult;
    }

    /// <summary>
    /// Business Method: Cancels an active or draft event.
    /// Invariant: Completed or already cancelled events cannot be cancelled.
    /// </summary>
    public Result Cancel(string reason, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == EventStatus.Cancelled)
        {
            return Result.Failure(
                Error.Conflict("Event.AlreadyCancelled", "The event is already cancelled."));
        }

        if (Status == EventStatus.Completed)
        {
            return Result.Failure(
                Error.Conflict("Event.AlreadyCompleted", "Completed events cannot be cancelled."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(
                Error.Validation("Event.CancelReasonRequired", "A reason must be provided when cancelling an event."));
        }

        Status = EventStatus.Cancelled;
        CancelledAtUtc = timeProvider.GetUtcNow();
        CancellationReason = reason.Trim();

        return Result.Success();
    }
}
