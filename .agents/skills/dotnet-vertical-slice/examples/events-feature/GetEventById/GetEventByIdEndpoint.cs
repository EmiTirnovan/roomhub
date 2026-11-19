using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using MyApp.Features.Common;

namespace MyApp.Features.Events.GetEventById;

public static class GetEventByIdEndpoint
{
    public static RouteGroupBuilder MapGetEventByIdEndpoint(this RouteGroupBuilder group)
    {
        // Notice route constraint {id:guid}
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetEventById")
            .WithSummary("Retrieves event details by its identifier")
            .Produces<GetEventByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<Results<Ok<GetEventByIdResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        GetEventByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetEventByIdQuery(id);
        var result = await handler.HandleAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
