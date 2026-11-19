using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Models;

namespace RoomHub.Api.Tests.Fakes;

public class TestUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

public class TestRoomRepository : IRoomRepository
{
    private readonly List<Room> _rooms = [];

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));
    }

    public Task AddAsync(Room room, CancellationToken cancellationToken = default)
    {
        _rooms.Add(room);
        return Task.CompletedTask;
    }
}

public class TestBookingRepository : IBookingRepository
{
    private readonly List<Booking> _bookings = [];

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bookings.FirstOrDefault(b => b.Id == id));
    }

    public Task<List<Booking>> ListForRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bookings.Where(b => b.RoomId == roomId).ToList());
    }

    public Task<int> CountActiveForParticipantOnDayAsync(string bookedBy, DateTime utcDate, CancellationToken cancellationToken = default)
    {
        var targetDate = utcDate.Date;
        var count = _bookings.Count(b =>
            b.Status == BookingStatus.Active &&
            string.Equals(b.BookedBy, bookedBy.Trim(), StringComparison.OrdinalIgnoreCase) &&
            b.StartUtc.Date == targetDate);

        return Task.FromResult(count);
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        _bookings.Add(booking);
        return Task.CompletedTask;
    }
}
