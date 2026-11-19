using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Features.Rooms.CreateRoom;
using RoomHub.Api.Features.Rooms.GetRoom;

namespace RoomHub.Api.Features;

public static class RoomsEndpoints
{
    public static void MapRoomsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").WithTags("Rooms");

        CreateRoomEndpoint.Map(group);
        GetRoomEndpoint.Map(group);
        CreateBookingEndpoint.Map(group);
    }
}
