using RoomHub.Api.Common;
using RoomHub.Api.Models;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Features.Rooms.CreateRoom;

public sealed record CreateRoomCommand(string? Name, int Capacity) : ICommand<CreateRoomResponse>;

public sealed class CreateRoomHandler(RoomRepository rooms) : ICommandHandler<CreateRoomCommand, CreateRoomResponse>
{
    public Task<Result<CreateRoomResponse>> Handle(CreateRoomCommand command, CancellationToken cancellationToken = default)
    {
        var roomResult = Room.Create(command.Name, command.Capacity);
        if (roomResult.IsFailure)
        {
            return Task.FromResult(Result.Failure<CreateRoomResponse>(roomResult.Error));
        }

        var room = roomResult.Value;
        rooms.Add(room);

        return Task.FromResult(Result.Success(new CreateRoomResponse(room.Id, room.Name, room.Capacity)));
    }
}
