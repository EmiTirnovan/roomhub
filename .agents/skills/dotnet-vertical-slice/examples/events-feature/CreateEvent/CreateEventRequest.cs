namespace MyApp.Features.Events.CreateEvent;

public sealed record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity);
