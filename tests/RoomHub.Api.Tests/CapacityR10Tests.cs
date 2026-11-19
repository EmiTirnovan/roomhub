using RoomHub.Api.Common;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Models;
using RoomHub.Api.Tests.Fakes;

namespace RoomHub.Api.Tests;

public class CapacityR10Tests
{
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task R10_Capacity10_ExactlyFourHours_IsAllowed()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Room 10", 10).Value;
        await roomRepo.AddAsync(room);

        var handler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);

        var result = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc), // Exactly 4h
            5));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task R10_Capacity11_ExactlyTwoHours_IsAllowed()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Large Room 11", 11).Value;
        await roomRepo.AddAsync(room);

        var handler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);

        var result = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc), // Exactly 2h
            5));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task R10_Capacity11_MoreThanTwoHours_Returns400BadRequest()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var room = Room.Create("Large Room 11", 11).Value;
        await roomRepo.AddAsync(room);

        var handler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);

        var result = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 1, DateTimeKind.Utc), // 2h and 1 second
            5));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Booking.ExceedsMaxDuration", result.Error.Code);
    }
}
