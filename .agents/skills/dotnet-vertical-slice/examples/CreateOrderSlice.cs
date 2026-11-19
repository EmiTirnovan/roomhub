using Microsoft.AspNetCore.Http.HttpResults;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Features.Orders.CreateOrder;

// 1. Request Contract (Public API DTO)
public sealed record CreateOrderRequest(
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

// 2. Response Contract
public sealed record OrderResponse(
    Guid OrderId,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc);

// 3. Command (Internal CQRS Message)
public sealed record CreateOrderCommand(
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

// 4. Custom Command Handler (No MediatR)
public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<OrderResponse>> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Value Objects and Domain Factory
        var emailResult = CustomerEmail.Create(command.CustomerEmail);
        if (emailResult.IsFailure)
        {
            return Result<OrderResponse>.Failure(emailResult.Error);
        }

        var moneyResult = Money.Create(command.UnitPrice);
        if (moneyResult.IsFailure)
        {
            return Result<OrderResponse>.Failure(moneyResult.Error);
        }

        // Domain entity execution
        var orderResult = Order.Create(
            emailResult.Value,
            command.ProductName,
            command.Quantity,
            moneyResult.Value,
            timeProvider.GetUtcNow());

        if (orderResult.IsFailure)
        {
            return Result<OrderResponse>.Failure(orderResult.Error);
        }

        var order = orderResult.Value;

        await orderRepository.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(
            order.Id,
            order.CustomerEmail.Value,
            order.ProductName,
            order.Quantity,
            order.TotalAmount.Amount,
            order.CreatedAtUtc);

        return Result<OrderResponse>.Success(response);
    }
}

// 5. Minimal API Endpoint Definition with TypedResults
public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrderEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates a new customer order")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return group;
    }

    public static async Task<Results<CreatedAtRoute<OrderResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> HandleAsync(
        CreateOrderRequest request,
        CreateOrderCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerEmail,
            request.ProductName,
            request.Quantity,
            request.UnitPrice);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.CreatedAtRoute(
                result.Value,
                routeName: "GetOrderById",
                routeValues: new { id = result.Value.OrderId });
        }

        return result.Error.Type switch
        {
            ErrorType.Conflict => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = result.Error.Message,
                Status = StatusCodes.Status409Conflict
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status400BadRequest
            })
        };
    }
}
