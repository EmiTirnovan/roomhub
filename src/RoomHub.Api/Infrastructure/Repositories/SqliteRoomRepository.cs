using Microsoft.EntityFrameworkCore;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Infrastructure.Persistence;
using RoomHub.Api.Models;

namespace RoomHub.Api.Infrastructure.Repositories;

public class SqliteRoomRepository(RoomHubDbContext dbContext) : IRoomRepository
{
    public async Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(Room room, CancellationToken cancellationToken = default)
    {
        await dbContext.Rooms.AddAsync(room, cancellationToken);
    }
}
