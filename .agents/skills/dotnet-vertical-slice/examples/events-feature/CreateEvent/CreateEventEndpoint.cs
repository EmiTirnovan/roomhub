using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using MyApp.Features.Common;

namespace MyApp.Features.Events.CreateEvent;

public static class CreateEventEndpoint
{
    public static RouteGroupBuilder MapCreateEventEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateEvent")
            .WithSummary("Creates a new event")
            .Produces<CreateEventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    public static async Task<Results<Created<CreateEventResponse>, ProblemHttpResult>> HandleAsync(
        CreateEventRequest request,
        CreateEventHandler handler,
        CancellationToken cancellationToken)
    {
        // 1. Thin endpoint: Translate input request into CQRS Command
        var command = new CreateEventCommand(
            request.Title,
            request.Description,
            request.ScheduledAtUtc,
            request.TicketPrice,
            request.Currency,
            request.MaxCapacity);

        // 2. Delegate execution to custom Handler
        var result = await handler.HandleAsync(command, cancellationToken);

        // 3. Transform Result into explicit TypedResults
        if (result.IsSuccess)
        {
            return TypedResults.Created($"/api/events/{result.Value.Id}", result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
