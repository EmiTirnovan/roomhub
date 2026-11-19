namespace MyApp.Features.Events.CreateEvent;

public sealed record CreateEventResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity,
    string Status,
    DateTimeOffset CreatedAtUtc);
