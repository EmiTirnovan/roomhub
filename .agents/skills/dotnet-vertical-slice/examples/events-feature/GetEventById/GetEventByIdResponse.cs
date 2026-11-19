namespace MyApp.Features.Events.GetEventById;

public sealed record GetEventByIdResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc);
