namespace MyApp.Features.Events.PublishEvent;

public sealed record PublishEventResponse(
    Guid Id,
    string Status,
    DateTimeOffset PublishedAtUtc);
