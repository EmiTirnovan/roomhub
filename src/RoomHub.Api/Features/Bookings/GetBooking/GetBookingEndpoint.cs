using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoomHub.Api.Common;

namespace RoomHub.Api.Features.Bookings.GetBooking;

public static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetBookingQuery, GetBookingResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetBookingQuery(id);
            var result = await handler.Handle(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetBooking")
        .WithSummary("Gets booking by id");
    }
}
