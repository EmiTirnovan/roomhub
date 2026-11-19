using RoomHub.Api.Common;
using RoomHub.Api.Features.Bookings.CancelBooking;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Models;
using RoomHub.Api.Tests.Fakes;

namespace RoomHub.Api.Tests;

public class DailyLimitR9Tests
{
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Scenario1_FirstActiveBookingOfUtcDay_IsAccepted()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        var result = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Scenario2_SecondActiveBookingOfSameUtcDay_IsAccepted()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // Booking 1: 08:00 - 10:00
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b1.IsSuccess);

        // Booking 2: 12:00 - 14:00 (same day, same participant)
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b2.IsSuccess);
    }

    [Fact]
    public async Task Scenario3_ThirdActiveBookingOnSameUtcDay_IsRejectedWith409()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // 1st booking
        await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));

        // 2nd booking
        await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            2));

        // 3rd booking on the same day -> Must fail with 409 Conflict
        var b3 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 16, 0, 0, DateTimeKind.Utc),
            2));

        Assert.True(b3.IsFailure);
        Assert.Equal(ErrorType.Conflict, b3.Error.Type);
        Assert.Equal("Booking.DailyLimitExceeded", b3.Error.Code);
    }

    [Fact]
    public async Task Scenario4_CanBookDifferentRoomOnSameDay_AsLongAsTotalRemainsAtMostTwo()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var roomA = Room.Create("Room A", 10).Value;
        var roomB = Room.Create("Room B", 10).Value;
        var roomC = Room.Create("Room C", 10).Value;
        await roomRepo.AddAsync(roomA);
        await roomRepo.AddAsync(roomB);
        await roomRepo.AddAsync(roomC);

        // 1st booking in Room A
        var b1 = await handler.Handle(new CreateBookingCommand(
            roomA.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b1.IsSuccess);

        // 2nd booking in Room B (different room) -> Success
        var b2 = await handler.Handle(new CreateBookingCommand(
            roomB.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b2.IsSuccess);

        // 3rd booking in Room C -> Must be rejected because across ALL rooms total is 2
        var b3 = await handler.Handle(new CreateBookingCommand(
            roomC.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 16, 0, 0, DateTimeKind.Utc),
            2));

        Assert.True(b3.IsFailure);
        Assert.Equal(ErrorType.Conflict, b3.Error.Type);
        Assert.Equal("Booking.DailyLimitExceeded", b3.Error.Code);
    }

    [Fact]
    public async Task Scenario5_BookingOnDifferentUtcDate_IsNotAffected()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // Two bookings on 2026-11-20
        await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));

        await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            2));

        // Booking on next day: 2026-11-21 -> Allowed!
        var nextDayBooking = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc),
            2));

        Assert.True(nextDayBooking.IsSuccess);
    }

    [Fact]
    public async Task Scenario6_CancelledBooking_DoesNotCountTowardDailyLimit()
    {
        var (roomRepo, bookingRepo, handler, cancelHandler) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // Booking 1: 08:00 - 10:00 (more than 60m from 00:00)
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b1.IsSuccess);

        // Cancel booking 1
        var cancelResult = await cancelHandler.Handle(new CancelBookingCommand(b1.Value.Id));
        Assert.True(cancelResult.IsSuccess);

        // Booking 2: 10:00 - 12:00
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b2.IsSuccess);

        // Booking 3: 13:00 - 15:00 -> Allowed because b1 is cancelled, active count is currently 1
        var b3 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc),
            2));

        Assert.True(b3.IsSuccess);
    }

    [Fact]
    public async Task Scenario7_AfterCancellingOneOfTwoExisting_NewBookingOnThatDayIsAccepted()
    {
        var (roomRepo, bookingRepo, handler, cancelHandler) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // Booking 1
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            2));

        // Booking 2
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            2));

        // 3rd booking rejected initially
        var b3Rejected = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b3Rejected.IsFailure);

        // Cancel b2
        await cancelHandler.Handle(new CancelBookingCommand(b2.Value.Id));

        // Now new booking should succeed!
        var b3Retry = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b3Retry.IsSuccess);
    }

    [Fact]
    public async Task Scenario8_UtcDateBoundaries_AreHandledCorrectly()
    {
        var (roomRepo, bookingRepo, handler, _) = CreateSetup();
        var room = Room.Create("Room 1", 10).Value;
        await roomRepo.AddAsync(room);

        // Booking on 2026-11-20 at 22:00 UTC (finishing 23:30 UTC)
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 30, 0, DateTimeKind.Utc),
            2));
        Assert.True(b1.IsSuccess);

        // Booking on 2026-11-21 at 00:30 UTC -> This is 2026-11-21 UTC, different UTC calendar day!
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 21, 0, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 2, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b2.IsSuccess);

        // Another booking on 2026-11-21
        var b3 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 5, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b3.IsSuccess);

        // 3rd booking on 2026-11-21 -> Rejected!
        var b4 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Participant1",
            new DateTime(2026, 11, 21, 6, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc),
            2));
        Assert.True(b4.IsFailure);
        Assert.Equal("Booking.DailyLimitExceeded", b4.Error.Code);
    }

    private (TestRoomRepository, TestBookingRepository, CreateBookingHandler, CancelBookingHandler) CreateSetup()
    {
        var roomRepo = new TestRoomRepository();
        var bookingRepo = new TestBookingRepository();
        var uow = new TestUnitOfWork();
        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, uow, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo, uow, _timeProvider);
        return (roomRepo, bookingRepo, createHandler, cancelHandler);
    }
}
