using RoomHub.Api.Common;
using RoomHub.Api.Features.Bookings.CancelBooking;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Features.Bookings.GetBooking;
using RoomHub.Api.Models;
using RoomHub.Api.Repositories;

namespace RoomHub.Api.Tests;

public class BookingTests
{
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidParameters_ReturnsSuccess()
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, 4, 10, _timeProvider);

        Assert.True(result.IsSuccess);
        Assert.Equal("Emi", result.Value.BookedBy);
        Assert.Equal(BookingStatus.Active, result.Value.Status);
        Assert.Equal(4, result.Value.Attendees);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenBookedByMissing_ReturnsValidationError(string? bookedBy)
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), bookedBy, start, end, 4, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.InvalidBookedBy", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_WhenEndNotAfterStart_ReturnsValidationError()
    {
        var start = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, 4, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.InvalidRange", result.Error.Code);
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ReturnsValidationError()
    {
        var time = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", time, time, 4, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.InvalidRange", result.Error.Code);
    }

    [Fact]
    public void Create_WhenStartIsInThePast_ReturnsValidationError()
    {
        // Clock is at 2026-11-20T08:00:00Z
        var pastStart = new DateTime(2026, 11, 20, 7, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", pastStart, end, 4, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.PastStart", result.Error.Code);
    }

    [Fact]
    public void Create_WhenDurationIsExactlyFourHours_IsAllowed()
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, 4, 10, _timeProvider);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_WhenDurationExceedsFourHours_ReturnsValidationError()
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 14, 0, 1, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, 4, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.ExceedsMaxDuration", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenAttendeesZeroOrNegative_ReturnsValidationError(int attendees)
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, attendees, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.InvalidAttendees", result.Error.Code);
    }

    [Fact]
    public void Create_WhenAttendeesExceedRoomCapacity_ReturnsValidationError()
    {
        var start = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var result = Booking.Create(Guid.NewGuid(), "Emi", start, end, 11, 10, _timeProvider);

        Assert.True(result.IsFailure);
        Assert.Equal("Booking.AttendeesExceedCapacity", result.Error.Code);
    }

    [Fact]
    public async Task CreateBookingHandler_WhenRoomMissing_ReturnsNotFoundBeforeValidatingBody()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var handler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);

        // Entire body is completely invalid (empty name, past start, negative attendees)
        var invalidCommand = new CreateBookingCommand(
            Guid.NewGuid(),
            "",
            DateTime.MinValue,
            DateTime.MinValue,
            -10);

        var result = await handler.Handle(invalidCommand);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Room.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateBookingHandler_WhenOverlappingActiveBookingExists_ReturnsConflict()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var room = Room.Create("Room 1", 10).Value;
        roomRepo.Add(room);

        var handler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);

        // First booking: 10:00 - 12:00
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Alice",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));
        Assert.True(b1.IsSuccess);

        // Overlapping booking: 11:00 - 13:00
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Bob",
            new DateTime(2026, 11, 20, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Utc),
            4));

        Assert.True(b2.IsFailure);
        Assert.Equal(ErrorType.Conflict, b2.Error.Type);
        Assert.Equal("Booking.RoomOccupied", b2.Error.Code);
    }

    [Fact]
    public async Task CreateBookingHandler_WhenIntervalsOnlyTouch_IsAllowed()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var room = Room.Create("Room 1", 10).Value;
        roomRepo.Add(room);

        var handler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);

        // Booking 1: 10:00 - 12:00
        var b1 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Alice",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));
        Assert.True(b1.IsSuccess);

        // Booking 2: 12:00 - 14:00 (starts exactly when b1 ends)
        var b2 = await handler.Handle(new CreateBookingCommand(
            room.Id,
            "Bob",
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc),
            4));

        Assert.True(b2.IsSuccess);
    }

    [Fact]
    public async Task CreateBookingHandler_WhenExistingBookingCancelled_FreesInterval()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var room = Room.Create("Room 1", 10).Value;
        roomRepo.Add(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo);

        // Booking 1: 10:00 - 12:00
        var b1 = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Alice",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));
        Assert.True(b1.IsSuccess);

        // Cancel b1
        var cancelResult = await cancelHandler.Handle(new CancelBookingCommand(b1.Value.Id));
        Assert.True(cancelResult.IsSuccess);

        // Booking 2 overlapping cancelled b1: 10:00 - 12:00 -> Allowed!
        var b2 = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Bob",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));

        Assert.True(b2.IsSuccess);
    }

    [Fact]
    public async Task CancelBooking_WhenAlreadyCancelled_ReturnsConflict()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var room = Room.Create("Room 1", 10).Value;
        roomRepo.Add(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);
        var cancelHandler = new CancelBookingHandler(bookingRepo);

        var b1 = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Alice",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));

        // First cancellation -> Success
        var firstCancel = await cancelHandler.Handle(new CancelBookingCommand(b1.Value.Id));
        Assert.True(firstCancel.IsSuccess);

        // Repeated cancellation -> 409 Conflict
        var secondCancel = await cancelHandler.Handle(new CancelBookingCommand(b1.Value.Id));
        Assert.True(secondCancel.IsFailure);
        Assert.Equal(ErrorType.Conflict, secondCancel.Error.Type);
        Assert.Equal("Booking.AlreadyCancelled", secondCancel.Error.Code);
    }

    [Fact]
    public async Task CancelBooking_WhenBookingNotFound_ReturnsNotFound()
    {
        var bookingRepo = new BookingRepository();
        var cancelHandler = new CancelBookingHandler(bookingRepo);

        var result = await cancelHandler.Handle(new CancelBookingCommand(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Booking.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetBooking_WhenExists_ReturnsBookingDetails()
    {
        var roomRepo = new RoomRepository();
        var bookingRepo = new BookingRepository();
        var room = Room.Create("Room 1", 10).Value;
        roomRepo.Add(room);

        var createHandler = new CreateBookingHandler(roomRepo, bookingRepo, _timeProvider);
        var getHandler = new GetBookingHandler(bookingRepo);

        var created = await createHandler.Handle(new CreateBookingCommand(
            room.Id,
            "Alice",
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
            4));

        var fetched = await getHandler.Handle(new GetBookingQuery(created.Value.Id));

        Assert.True(fetched.IsSuccess);
        Assert.Equal(created.Value.Id, fetched.Value.Id);
        Assert.Equal(room.Id, fetched.Value.RoomId);
        Assert.Equal("Alice", fetched.Value.BookedBy);
        Assert.Equal("Active", fetched.Value.Status);
    }
}
