using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Common;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Features.Events.GetEventById;

public sealed class GetEventByIdHandler(IEventRepository eventRepository)
{
    public async Task<Result<GetEventByIdResponse>> HandleAsync(
        GetEventByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Pure read-only operation: uses AsNoTracking in repository
        var ev = await eventRepository.GetByIdAsNoTrackingAsync(query.Id, cancellationToken);
        if (ev is null)
        {
            return Result<GetEventByIdResponse>.Failure(
                Error.NotFound("Event.NotFound", $"Event with ID '{query.Id}' was not found."));
        }

        var response = new GetEventByIdResponse(
            ev.Id,
            ev.Title.Value,
            ev.Description,
            ev.ScheduledAtUtc,
            ev.Price.Amount,
            ev.Price.Currency,
            ev.Capacity.Value,
            ev.Status.ToString(),
            ev.CreatedAtUtc,
            ev.PublishedAtUtc);

        return Result<GetEventByIdResponse>.Success(response);
    }
}
