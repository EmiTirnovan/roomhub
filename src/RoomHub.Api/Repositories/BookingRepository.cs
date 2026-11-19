using RoomHub.Api.Models;

namespace RoomHub.Api.Repositories;

public class BookingRepository
{
    private readonly List<Booking> _bookings = new();
    private readonly object _lock = new();

    public void Add(Booking booking)
    {
        lock (_lock)
        {
            _bookings.Add(booking);
        }
    }

    public Booking? Get(Guid id)
    {
        lock (_lock)
        {
            return _bookings.FirstOrDefault(b => b.Id == id);
        }
    }

    public List<Booking> ListForRoom(Guid roomId)
    {
        lock (_lock)
        {
            return _bookings.Where(b => b.RoomId == roomId).ToList();
        }
    }

    public int CountActiveForParticipantOnDay(string bookedBy, DateTime utcDate)
    {
        var targetDate = utcDate.Date;
        lock (_lock)
        {
            return _bookings.Count(b =>
                b.Status == BookingStatus.Active &&
                string.Equals(b.BookedBy, bookedBy.Trim(), StringComparison.OrdinalIgnoreCase) &&
                b.StartUtc.Date == targetDate);
        }
    }
}
