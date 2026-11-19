using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;

namespace RoomHub.Api.Features.Bookings.CancelBooking;

public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelBookingCommand>
{
    public async Task<Result<Unit>> Handle(CancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetByIdAsync(command.Id, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<Unit>(
                Error.NotFound("Booking.NotFound", "The booking does not exist."));
        }

        // R8 & R11 checked in domain method Cancel(timeProvider)
        var cancelResult = booking.Cancel(timeProvider);
        if (cancelResult.IsFailure)
        {
            return Result.Failure<Unit>(cancelResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(Unit.Value);
    }
}
