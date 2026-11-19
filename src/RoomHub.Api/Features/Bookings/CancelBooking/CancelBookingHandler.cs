using RoomHub.Api.Common;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Features.Bookings.CancelBooking;

public sealed class CancelBookingHandler(BookingRepository bookings) : ICommandHandler<CancelBookingCommand>
{
    public Task<Result<Unit>> Handle(CancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = bookings.Get(command.Id);
        if (booking is null)
        {
            return Task.FromResult(Result.Failure<Unit>(
                Error.NotFound("Booking.NotFound", "The booking does not exist.")));
        }

        var cancelResult = booking.Cancel();
        if (cancelResult.IsFailure)
        {
            return Task.FromResult(Result.Failure<Unit>(cancelResult.Error));
        }

        return Task.FromResult(Result.Success(Unit.Value));
    }
}
