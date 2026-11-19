using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoomHub.Api.Common;

namespace RoomHub.Api.Features.Bookings.CancelBooking;

public static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup.MapPost("/{id:guid}/cancel", async (
            Guid id,
            ICommandHandler<CancelBookingCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CancelBookingCommand(id);
            var result = await handler.Handle(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("CancelBooking")
        .WithSummary("Cancels an existing booking");
    }
}
