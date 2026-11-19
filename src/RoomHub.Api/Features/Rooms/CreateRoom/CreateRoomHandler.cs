using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Models;

namespace RoomHub.Api.Features.Rooms.CreateRoom;

public sealed class CreateRoomHandler(
    IRoomRepository rooms,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateRoomCommand, CreateRoomResponse>
{
    public async Task<Result<CreateRoomResponse>> Handle(CreateRoomCommand command, CancellationToken cancellationToken = default)
    {
        var roomResult = Room.Create(command.Name, command.Capacity);
        if (roomResult.IsFailure)
        {
            return Result.Failure<CreateRoomResponse>(roomResult.Error);
        }

        var room = roomResult.Value;
        await rooms.AddAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateRoomResponse(room.Id, room.Name, room.Capacity));
    }
}
