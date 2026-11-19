using Microsoft.Extensions.Time.Testing;
using MyApp.Application.Common.Cqrs;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Features.Events;
using MyApp.Infrastructure.Persistence.Repositories;
using NSubstitute;
using Xunit;

namespace MyApp.Tests.Unit.Features;

public sealed record Participant(Guid Id, string Email);

public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class RegisterForEventHandlerTests
{
    private readonly IEventRepository _eventRepository = Substitute.For<IEventRepository>();
    private readonly IParticipantRepository _participantRepository = Substitute.For<IParticipantRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider;
    private readonly RegisterForEventHandler _handler;

    private readonly DateTimeOffset _initialTime = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    public RegisterForEventHandlerTests()
    {
        _timeProvider = new FakeTimeProvider(_initialTime);
        _handler = new RegisterForEventHandler(
            _eventRepository,
            _participantRepository,
            _unitOfWork,
            _timeProvider);
    }

    private Event CreatePublishedEvent(int capacity = 10, TimeSpan? startOffset = null)
    {
        var offset = startOffset ?? TimeSpan.FromDays(2);
        var schedule = DateRange.Create(_initialTime.Add(offset), _initialTime.Add(offset).AddHours(2)).Value;
        var ev = Event.Create("Tech Conference", "Description", schedule, capacity, _timeProvider).Value;
        ev.Publish(_timeProvider);
        return ev;
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var command = new RegisterForEventCommand(Guid.NewGuid(), Guid.NewGuid());
        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns((Event?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Event.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenParticipantDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var ev = CreatePublishedEvent();
        var command = new RegisterForEventCommand(ev.Id, Guid.NewGuid());

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(ev);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns((Participant?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Participant.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEventNotPublished_ReturnsConflictError()
    {
        // Arrange: Event is in Draft status
        var schedule = DateRange.Create(_initialTime.AddDays(1), _initialTime.AddDays(1).AddHours(2)).Value;
        var draftEvent = Event.Create("Draft Meetup", "Desc", schedule, 50, _timeProvider).Value;

        var participant = new Participant(Guid.NewGuid(), "dev@example.com");
        var command = new RegisterForEventCommand(draftEvent.Id, participant.Id);

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(draftEvent);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns(participant);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Event.NotOpen", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEventAlreadyStarted_ReturnsConflictError()
    {
        // Arrange
        var ev = CreatePublishedEvent(capacity: 20, startOffset: TimeSpan.FromHours(1));
        var participant = new Participant(Guid.NewGuid(), "late@example.com");
        var command = new RegisterForEventCommand(ev.Id, participant.Id);

        // Advance time by 2 hours so event start is now in the past
        _timeProvider.Advance(TimeSpan.FromHours(2));

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(ev);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns(participant);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Event.AlreadyStarted", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCapacityExhausted_ReturnsConflictError()
    {
        // Arrange: Capacity is 1
        var ev = CreatePublishedEvent(capacity: 1);
        var firstParticipantId = Guid.NewGuid();
        ev.Register(firstParticipantId, "first@example.com", _timeProvider);

        var secondParticipant = new Participant(Guid.NewGuid(), "second@example.com");
        var command = new RegisterForEventCommand(ev.Id, secondParticipant.Id);

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(ev);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns(secondParticipant);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Event.SoldOut", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDuplicateRegistration_ReturnsConflictError()
    {
        // Arrange
        var ev = CreatePublishedEvent(capacity: 100);
        var participant = new Participant(Guid.NewGuid(), "member@example.com");
        ev.Register(participant.Id, participant.Email, _timeProvider); // Already registered once

        var command = new RegisterForEventCommand(ev.Id, participant.Id);

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(ev);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns(participant);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Event.AlreadyRegistered", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_SavesChangesAndReturnsSuccessResult()
    {
        // Arrange
        var ev = CreatePublishedEvent(capacity: 50);
        var participant = new Participant(Guid.NewGuid(), "newattendee@example.com");
        var command = new RegisterForEventCommand(ev.Id, participant.Id);

        _eventRepository.GetByIdAsync(command.EventId, Arg.Any<CancellationToken>()).Returns(ev);
        _participantRepository.GetByIdAsync(command.ParticipantId, Arg.Any<CancellationToken>()).Returns(participant);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ev.Id, result.Value.EventId);
        Assert.Equal(participant.Id, result.Value.ParticipantId);
        Assert.Single(ev.Registrations);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
