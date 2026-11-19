using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using RoomHub.Api.Common;

namespace RoomHub.Api.Features.Rooms.CreateRoom;

public static class CreateRoomEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            CreateRoomRequest request,
            ICommandHandler<CreateRoomCommand, CreateRoomResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateRoomCommand(request.Name, request.Capacity);
            var result = await handler.Handle(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/rooms/{result.Value.Id}", result.Value)
                : result.ToProblemDetails();
        })
        .WithName("CreateRoom")
        .WithSummary("Creates a new room");
    }
}
