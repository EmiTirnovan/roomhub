using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoomHub.Api.Common;

namespace RoomHub.Api.Features.Bookings.CreateBooking;

public static class CreateBookingEndpoint
{
    public static void Map(RouteGroupBuilder roomsGroup)
    {
        roomsGroup.MapPost("/{roomId:guid}/bookings", async (
            Guid roomId,
            CreateBookingRequest request,
            ICommandHandler<CreateBookingCommand, CreateBookingResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateBookingCommand(
                roomId,
                request.BookedBy,
                request.StartUtc,
                request.EndUtc,
                request.Attendees);

            var result = await handler.Handle(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/bookings/{result.Value.Id}", result.Value)
                : result.ToProblemDetails();
        })
        .WithName("CreateBooking")
        .WithSummary("Creates a booking for a specific room");
    }
}
