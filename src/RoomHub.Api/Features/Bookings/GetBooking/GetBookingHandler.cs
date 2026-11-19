using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;

namespace RoomHub.Api.Features.Bookings.GetBooking;

public sealed class GetBookingHandler(IBookingRepository bookings) : IQueryHandler<GetBookingQuery, GetBookingResponse>
{
    public async Task<Result<GetBookingResponse>> Handle(GetBookingQuery query, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetByIdAsync(query.Id, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<GetBookingResponse>(
                Error.NotFound("Booking.NotFound", "The booking does not exist."));
        }

        return Result.Success(new GetBookingResponse(
            booking.Id,
            booking.RoomId,
            booking.BookedBy,
            booking.StartUtc,
            booking.EndUtc,
            booking.Attendees,
            booking.Status.ToString()));
    }
}
