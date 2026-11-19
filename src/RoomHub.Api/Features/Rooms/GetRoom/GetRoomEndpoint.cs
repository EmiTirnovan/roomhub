using Microsoft.AspNetCore.Http;
using RoomHub.Api.Common;

namespace RoomHub.Api.Features.Rooms.GetRoom;

public static class GetRoomEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetRoomQuery, GetRoomResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRoomQuery(id);
            var result = await handler.Handle(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetRoom")
        .WithSummary("Gets room by id");
    }
}
