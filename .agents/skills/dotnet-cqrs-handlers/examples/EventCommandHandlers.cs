using MyApp.Application.Common.Cqrs;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Features.Events;

// =========================================================================
// 1. Publish Event
// =========================================================================

public sealed record PublishEventCommand(Guid EventId) : ICommand;

public sealed class PublishEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PublishEventCommand>
{
    public async Task<Result<Unit>> Handle(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<Unit>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        // 2. Delegate state transition and business invariants to the Rich Domain model
        var publishResult = ev.Publish(timeProvider);
        if (publishResult.IsFailure)
        {
            return Result<Unit>.Failure(publishResult.Error);
        }

        // 3. Atomically persist changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}

// =========================================================================
// 2. Cancel Event
// =========================================================================

public sealed record CancelEventCommand(Guid EventId, string Reason) : ICommand;

public sealed class CancelEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelEventCommand>
{
    public async Task<Result<Unit>> Handle(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<Unit>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        var cancelResult = ev.Cancel(command.Reason, timeProvider);
        if (cancelResult.IsFailure)
        {
            return Result<Unit>.Failure(cancelResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}

// =========================================================================
// 3. Register For Event
// =========================================================================

public sealed record RegisterForEventCommand(
    Guid EventId,
    Guid ParticipantId) : ICommand<RegistrationResultDto>;

public sealed record RegistrationResultDto(
    Guid RegistrationId,
    Guid EventId,
    Guid ParticipantId,
    DateTimeOffset RegisteredAtUtc);

public sealed class RegisterForEventHandler(
    IEventRepository eventRepository,
    IParticipantRepository participantRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RegisterForEventCommand, RegistrationResultDto>
{
    public async Task<Result<RegistrationResultDto>> Handle(
        RegisterForEventCommand command,
        CancellationToken cancellationToken)
    {
        // Step 1: Load Event
        var ev = await eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (ev is null)
        {
            return Result<RegistrationResultDto>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.EventId}' was not found."));
        }

        // Step 2: Verify Participant existence
        var participant = await participantRepository.GetByIdAsync(command.ParticipantId, cancellationToken);
        if (participant is null)
        {
            return Result<RegistrationResultDto>.Failure(
                Error.NotFound("Participant.NotFound", $"Participant '{command.ParticipantId}' was not found."));
        }

        // Step 3: Call Event.Register (all invariant rules are encapsulated inside the Domain!)
        var registrationResult = ev.Register(participant.Id, participant.Email, timeProvider);

        // Step 4: Verify Result from Domain
        if (registrationResult.IsFailure)
        {
            return Result<RegistrationResultDto>.Failure(registrationResult.Error);
        }

        // Step 5: Save via IUnitOfWork
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 6: Return result DTO
        var registration = registrationResult.Value;
        var response = new RegistrationResultDto(
            registration.Id,
            registration.EventId,
            registration.AttendeeId,
            registration.RegisteredAtUtc);

        return Result<RegistrationResultDto>.Success(response);
    }
}
