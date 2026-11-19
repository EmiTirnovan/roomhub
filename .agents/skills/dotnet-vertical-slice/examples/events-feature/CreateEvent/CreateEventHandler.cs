using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Features.Events.CreateEvent;

public sealed class CreateEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<CreateEventResponse>> HandleAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Invariant checks via Domain Value Objects
        var titleResult = EventTitle.Create(command.Title);
        if (titleResult.IsFailure)
        {
            return Result<CreateEventResponse>.Failure(titleResult.Error);
        }

        var priceResult = Money.Create(command.TicketPrice, command.Currency);
        if (priceResult.IsFailure)
        {
            return Result<CreateEventResponse>.Failure(priceResult.Error);
        }

        var capacityResult = Capacity.Create(command.MaxCapacity);
        if (capacityResult.IsFailure)
        {
            return Result<CreateEventResponse>.Failure(capacityResult.Error);
        }

        // 2. Invoke Rich Domain factory method
        var eventResult = Event.Create(
            titleResult.Value,
            command.Description,
            command.ScheduledAtUtc,
            priceResult.Value,
            capacityResult.Value,
            timeProvider.GetUtcNow());

        if (eventResult.IsFailure)
        {
            return Result<CreateEventResponse>.Failure(eventResult.Error);
        }

        var ev = eventResult.Value;

        // 3. Persist through Repository & Unit of Work
        await eventRepository.AddAsync(ev, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Map to isolated Response DTO (never leak Domain entity directly)
        var response = new CreateEventResponse(
            ev.Id,
            ev.Title.Value,
            ev.Description,
            ev.ScheduledAtUtc,
            ev.Price.Amount,
            ev.Price.Currency,
            ev.Capacity.Value,
            ev.Status.ToString(),
            ev.CreatedAtUtc);

        return Result<CreateEventResponse>.Success(response);
    }
}
