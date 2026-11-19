using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Common;

namespace MyApp.Application.Common.Cqrs;

// 1. Core CQRS Handler Interfaces
public interface ICommand<TResponse>;
public interface ICommand;

public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

// 2. Concrete Command and Handler Example
public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelOrderCommand>
{
    private static readonly TimeSpan AllowedCancellationWindow = TimeSpan.FromMinutes(30);

    public async Task<Result> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("Order.NotFound", $"Order with id '{command.OrderId}' was not found."));
        }

        // Orchestrate: delegate all invariant checks to the Rich Domain model
        var cancelResult = order.Cancel(timeProvider, AllowedCancellationWindow);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// 3. DI Registration Extension Method (Manual, zero-reflection or clean typed registration)
public static class CqrsDependencyInjection
{
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services)
    {
        // Explicit Scoped registration ensures no hidden magic or runtime assembly scanning bugs
        services.AddScoped<ICommandHandler<CancelOrderCommand>, CancelOrderCommandHandler>();
        
        // Alternatively, register handler class directly for Minimal API endpoint parameter injection:
        services.AddScoped<CancelOrderCommandHandler>();

        return services;
    }
}
