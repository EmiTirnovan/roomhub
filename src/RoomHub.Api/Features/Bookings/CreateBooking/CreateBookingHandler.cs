using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Models;

namespace RoomHub.Api.Features.Bookings.CreateBooking;

public sealed class CreateBookingHandler(
    IRoomRepository rooms,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
{
    public async Task<Result<CreateBookingResponse>> Handle(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Verify that the room exists. Return 404 BEFORE validating the booking body.
        var room = await rooms.GetByIdAsync(command.RoomId, cancellationToken);
        if (room is null)
        {
            return Result.Failure<CreateBookingResponse>(
                Error.NotFound("Room.NotFound", "The room does not exist."));
        }

        // 2-4. Validate booking inputs, time boundaries, capacity & attendees via Rich Domain.
        // R10 is checked inside Booking.Create with room.Capacity.
        var bookingResult = Booking.Create(
            command.RoomId,
            command.BookedBy,
            command.StartUtc,
            command.EndUtc,
            command.Attendees,
            room.Capacity,
            timeProvider);

        if (bookingResult.IsFailure)
        {
            return Result.Failure<CreateBookingResponse>(bookingResult.Error);
        }

        // 5. Check room overlap (R7) - active bookings only, touching boundaries allowed.
        var existingBookings = await bookings.ListForRoomAsync(command.RoomId, cancellationToken);
        foreach (var existing in existingBookings)
        {
            if (existing.Status == BookingStatus.Active &&
                existing.StartUtc < command.EndUtc &&
                command.StartUtc < existing.EndUtc)
            {
                return Result.Failure<CreateBookingResponse>(
                    Error.Conflict("Booking.RoomOccupied", "The room is occupied in this interval."));
            }
        }

        // 6. Check participant daily booking limit (R9) - max 2 active bookings across all rooms per UTC day.
        var activeForParticipant = await bookings.CountActiveForParticipantOnDayAsync(
            command.BookedBy!, command.StartUtc, cancellationToken);

        if (activeForParticipant >= 2)
        {
            return Result.Failure<CreateBookingResponse>(
                Error.Conflict("Booking.DailyLimitExceeded", "Participant cannot have more than 2 active bookings per day."));
        }

        // 7. Persist booking.
        var booking = bookingResult.Value;
        await bookings.AddAsync(booking, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateBookingResponse(
            booking.Id,
            booking.RoomId,
            booking.Status.ToString()));
    }
}
