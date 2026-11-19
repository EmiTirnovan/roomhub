using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using MyApp.Features.Common;

namespace MyApp.Features.Events.PublishEvent;

public static class PublishEventEndpoint
{
    public static RouteGroupBuilder MapPublishEventEndpoint(this RouteGroupBuilder group)
    {
        // Notice route constraint {id:guid}
        group.MapPost("/{id:guid}/publish", HandleAsync)
            .WithName("PublishEvent")
            .WithSummary("Publishes an event making it active")
            .Produces<PublishEventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    public static async Task<Results<Ok<PublishEventResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        PublishEventHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new PublishEventCommand(id);
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
