using RoomHub.Api.Common;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Features.Rooms.GetRoom;

public sealed class GetRoomHandler(RoomRepository rooms) : IQueryHandler<GetRoomQuery, GetRoomResponse>
{
    public Task<Result<GetRoomResponse>> Handle(GetRoomQuery query, CancellationToken cancellationToken = default)
    {
        var room = rooms.Get(query.Id);
        if (room is null)
        {
            return Task.FromResult(Result.Failure<GetRoomResponse>(
                Error.NotFound("Room.NotFound", "The room does not exist.")));
        }

        return Task.FromResult(Result.Success(new GetRoomResponse(room.Id, room.Name, room.Capacity)));
    }
}
