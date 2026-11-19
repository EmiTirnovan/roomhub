using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Common;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Features.Events.PublishEvent;

public sealed class PublishEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<PublishEventResponse>> HandleAsync(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load tracked entity from repository
        var ev = await eventRepository.GetByIdAsync(command.Id, cancellationToken);
        if (ev is null)
        {
            return Result<PublishEventResponse>.Failure(
                Error.NotFound("Event.NotFound", $"Event with ID '{command.Id}' was not found."));
        }

        // 2. Delegate state transition and business invariant check to the Rich Domain model
        var publishResult = ev.Publish(timeProvider);
        if (publishResult.IsFailure)
        {
            return Result<PublishEventResponse>.Failure(publishResult.Error);
        }

        // 3. Atomically persist changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Return isolated Response contract
        var response = new PublishEventResponse(
            ev.Id,
            ev.Status.ToString(),
            ev.PublishedAtUtc!.Value);

        return Result<PublishEventResponse>.Success(response);
    }
}
