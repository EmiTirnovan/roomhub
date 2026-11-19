using RoomHub.Api.Common;
using RoomHub.Api.Features.Bookings.CancelBooking;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Models;
using RoomHub.Api.Tests.Fakes;

namespace RoomHub.Api.Tests;

public class CancellationR11Tests
{
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task R11_CancellationMoreThan60MinutesBeforeStart_IsAllowed()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo, uow, _timeProvider);

        // Booking starts at 12:00 (120 minutes from 10:00)
        var booking = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            4));

        var result = await cancelHandler.Handle(new CancelBookingCommand(booking.Value.Id));

        Assert.True(result.IsSuccess);
        var persisted = await bookingRepo.GetByIdAsync(booking.Value.Id);
        Assert.Equal(BookingStatus.Cancelled, persisted!.Status);
    }

    [Fact]
    public async Task R11_CancellationExactly60MinutesBeforeStart_IsAllowed()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo, uow, _timeProvider);

        // Booking starts at 11:00 (exactly 60 minutes from 10:00)
        var booking = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            4));

        var result = await cancelHandler.Handle(new CancelBookingCommand(booking.Value.Id));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task R11_CancellationLessThan60MinutesBeforeStart_Returns409Conflict()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo, uow, _timeProvider);

        // Booking starts at 10:59 (59 minutes from 10:00)
        var booking = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 10, 59, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));

        var result = await cancelHandler.Handle(new CancelBookingCommand(booking.Value.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Booking.CancellationTooLate", result.Error.Code);
    }
}
