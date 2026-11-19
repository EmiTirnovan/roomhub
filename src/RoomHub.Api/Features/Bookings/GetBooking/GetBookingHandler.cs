using RoomHub.Api.Common;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Features.Bookings.GetBooking;

public sealed class GetBookingHandler(BookingRepository bookings) : IQueryHandler<GetBookingQuery, GetBookingResponse>
{
    public Task<Result<GetBookingResponse>> Handle(GetBookingQuery query, CancellationToken cancellationToken = default)
    {
        var booking = bookings.Get(query.Id);
        if (booking is null)
        {
            return Task.FromResult(Result.Failure<GetBookingResponse>(
                Error.NotFound("Booking.NotFound", "The booking does not exist.")));
        }

        return Task.FromResult(Result.Success(new GetBookingResponse(
            booking.Id,
            booking.RoomId,
            booking.BookedBy,
            booking.StartUtc,
            booking.EndUtc,
            booking.Attendees,
            booking.Status.ToString())));
    }
}
