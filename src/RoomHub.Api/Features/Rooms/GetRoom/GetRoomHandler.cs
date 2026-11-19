using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;

namespace RoomHub.Api.Features.Rooms.GetRoom;

public sealed class GetRoomHandler(IRoomRepository rooms) : IQueryHandler<GetRoomQuery, GetRoomResponse>
{
    public async Task<Result<GetRoomResponse>> Handle(GetRoomQuery query, CancellationToken cancellationToken = default)
    {
        var room = await rooms.GetByIdAsync(query.Id, cancellationToken);
        if (room is null)
        {
            return Result.Failure<GetRoomResponse>(
                Error.NotFound("Room.NotFound", "The room does not exist."));
        }

        return Result.Success(new GetRoomResponse(room.Id, room.Name, room.Capacity));
    }
}
