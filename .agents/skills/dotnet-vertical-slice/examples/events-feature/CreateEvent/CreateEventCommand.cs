namespace MyApp.Features.Events.CreateEvent;

public sealed record CreateEventCommand(
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity);
