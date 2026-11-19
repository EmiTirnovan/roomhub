namespace RoomHub.Api.Features.Rooms.GetRoom;

public sealed record GetRoomResponse(Guid Id, string Name, int Capacity);

public sealed record GetRoomQuery(Guid Id) : Common.IQuery<GetRoomResponse>;
