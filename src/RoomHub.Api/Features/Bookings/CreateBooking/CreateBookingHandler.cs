using RoomHub.Api.Common;
using RoomHub.Api.Models;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Features.Bookings.CreateBooking;

public sealed class CreateBookingHandler(
    RoomRepository rooms,
    BookingRepository bookings,
    TimeProvider timeProvider) : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
{
    public Task<Result<CreateBookingResponse>> Handle(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Verify that the room exists. Return 404 BEFORE validating the booking body.
        var room = rooms.Get(command.RoomId);
        if (room is null)
        {
            return Task.FromResult(Result.Failure<CreateBookingResponse>(
                Error.NotFound("Room.NotFound", "The room does not exist.")));
        }

        // 2-4. Validate booking inputs, time boundaries, capacity & attendees via Rich Domain.
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
            return Task.FromResult(Result.Failure<CreateBookingResponse>(bookingResult.Error));
        }

        // 5. Check room overlap (R7) - active bookings only, touching boundaries allowed.
        var existingBookings = bookings.ListForRoom(command.RoomId);
        foreach (var existing in existingBookings)
        {
            if (existing.Status == BookingStatus.Active &&
                existing.StartUtc < command.EndUtc &&
                command.StartUtc < existing.EndUtc)
            {
                return Task.FromResult(Result.Failure<CreateBookingResponse>(
                    Error.Conflict("Booking.RoomOccupied", "The room is occupied in this interval.")));
            }
        }

        // 6. Check participant daily booking limit (R9) - max 2 active bookings across all rooms per UTC day.
        var requestedUtcDate = command.StartUtc.Date;
        var activeForParticipant = bookings.CountActiveForParticipantOnDay(command.BookedBy!, requestedUtcDate);
        if (activeForParticipant >= 2)
        {
            return Task.FromResult(Result.Failure<CreateBookingResponse>(
                Error.Conflict("Booking.DailyLimitExceeded", "Participant cannot have more than 2 active bookings per day.")));
        }

        // 7. Persist booking.
        var booking = bookingResult.Value;
        bookings.Add(booking);

        return Task.FromResult(Result.Success(new CreateBookingResponse(
            booking.Id,
            booking.RoomId,
            booking.Status.ToString())));
    }
}
