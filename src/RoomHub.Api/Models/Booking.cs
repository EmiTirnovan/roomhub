using RoomHub.Api.Common;

namespace RoomHub.Api.Models;

public enum BookingStatus
{
    Active,
    Cancelled
}

public class Booking
{
    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public string BookedBy { get; private set; } = string.Empty;
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public int Attendees { get; private set; }
    public BookingStatus Status { get; private set; }

    private Booking() { }

    private Booking(
        Guid id,
        Guid roomId,
        string bookedBy,
        DateTime startUtc,
        DateTime endUtc,
        int attendees,
        BookingStatus status)
    {
        Id = id;
        RoomId = roomId;
        BookedBy = bookedBy;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Attendees = attendees;
        Status = status;
    }

    public static Result<Booking> Create(
        Guid roomId,
        string? bookedBy,
        DateTime startUtc,
        DateTime endUtc,
        int attendees,
        int roomCapacity,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(bookedBy))
        {
            return Result.Failure<Booking>(Error.Validation("Booking.InvalidBookedBy", "BookedBy is required."));
        }

        if (endUtc <= startUtc)
        {
            return Result.Failure<Booking>(Error.Validation("Booking.InvalidRange", "The end must be after the start."));
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (startUtc < nowUtc)
        {
            return Result.Failure<Booking>(Error.Validation("Booking.PastStart", "A booking cannot start in the past."));
        }

        if ((endUtc - startUtc) > TimeSpan.FromHours(4))
        {
            return Result.Failure<Booking>(Error.Validation("Booking.ExceedsMaxDuration", "Maximum booking duration is four hours."));
        }

        if (attendees <= 0)
        {
            return Result.Failure<Booking>(Error.Validation("Booking.InvalidAttendees", "Attendees must be positive."));
        }

        if (attendees > roomCapacity)
        {
            return Result.Failure<Booking>(Error.Validation("Booking.AttendeesExceedCapacity", "The room does not have enough seats."));
        }

        var booking = new Booking(
            Guid.NewGuid(),
            roomId,
            bookedBy.Trim(),
            startUtc,
            endUtc,
            attendees,
            BookingStatus.Active);

        return Result.Success(booking);
    }

    public Result Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("Booking.AlreadyCancelled", "The booking is already cancelled."));
        }

        Status = BookingStatus.Cancelled;
        return Result.Success();
    }
}
