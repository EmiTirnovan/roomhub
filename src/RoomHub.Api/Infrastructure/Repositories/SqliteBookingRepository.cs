using Microsoft.EntityFrameworkCore;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Infrastructure.Persistence;
using RoomHub.Api.Models;

namespace RoomHub.Api.Infrastructure.Repositories;

public class SqliteBookingRepository(RoomHubDbContext dbContext) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<List<Booking>> ListForRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .Where(b => b.RoomId == roomId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveForParticipantOnDayAsync(string bookedBy, DateTime utcDate, CancellationToken cancellationToken = default)
    {
        var startOfDay = utcDate.Date;
        var endOfDay = startOfDay.AddDays(1);
        var normalizedBookedBy = bookedBy.Trim().ToLower();

        return await dbContext.Bookings
            .CountAsync(b =>
                b.Status == BookingStatus.Active &&
                b.BookedBy.ToLower() == normalizedBookedBy &&
                b.StartUtc >= startOfDay &&
                b.StartUtc < endOfDay,
                cancellationToken);
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await dbContext.Bookings.AddAsync(booking, cancellationToken);
    }
}
