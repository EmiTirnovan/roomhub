using RoomHub.Api.Common;
using RoomHub.Api.Features.Rooms.CreateRoom;
using RoomHub.Api.Features.Rooms.GetRoom;
using RoomHub.Api.Models;
using RoomHub.Api.Tests.Fakes;

namespace RoomHub.Api.Tests;

public class RoomTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsSuccess()
    {
        var result = Room.Create("Room 101", 10);

        Assert.True(result.IsSuccess);
        Assert.Equal("Room 101", result.Value.Name);
        Assert.Equal(10, result.Value.Capacity);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceName_ReturnsValidationError(string? name)
    {
        var result = Room.Create(name, 10);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Room.InvalidName", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithZeroOrNegativeCapacity_ReturnsValidationError(int capacity)
    {
        var result = Room.Create("Room 101", capacity);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Room.InvalidCapacity", result.Error.Code);
    }

    [Fact]
    public async Task CreateRoomHandler_WithValidCommand_PersistsAndReturnsRoom()
    {
        var repository = new TestRoomRepository();
        var uow = new TestUnitOfWork();
        var handler = new CreateRoomHandler(repository, uow);

        var result = await handler.Handle(new CreateRoomCommand("Study Room", 8));

        Assert.True(result.IsSuccess);
        Assert.Equal("Study Room", result.Value.Name);
        Assert.Equal(8, result.Value.Capacity);

        var persisted = await repository.GetByIdAsync(result.Value.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Study Room", persisted.Name);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task GetRoomHandler_WhenRoomExists_ReturnsRoom()
    {
        var repository = new TestRoomRepository();
        var room = Room.Create("Conference A", 20).Value;
        await repository.AddAsync(room);

        var handler = new GetRoomHandler(repository);
        var result = await handler.Handle(new GetRoomQuery(room.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(room.Id, result.Value.Id);
        Assert.Equal("Conference A", result.Value.Name);
        Assert.Equal(20, result.Value.Capacity);
    }

    [Fact]
    public async Task GetRoomHandler_WhenRoomMissing_ReturnsNotFound()
    {
        var repository = new TestRoomRepository();
        var handler = new GetRoomHandler(repository);

        var result = await handler.Handle(new GetRoomQuery(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Room.NotFound", result.Error.Code);
    }
}
