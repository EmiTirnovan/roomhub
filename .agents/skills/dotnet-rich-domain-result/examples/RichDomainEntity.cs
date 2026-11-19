namespace MyApp.Domain.Orders;

using MyApp.Domain.Common;

// 1. Value Object: CustomerEmail
public sealed record CustomerEmail
{
    public string Value { get; }

    private CustomerEmail(string value) => Value = value;

    public static Result<CustomerEmail> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<CustomerEmail>.Failure(Error.Validation("Email.Empty", "Email cannot be empty."));
        }

        var trimmed = email.Trim();
        if (!trimmed.Contains('@') || !trimmed.Contains('.'))
        {
            return Result<CustomerEmail>.Failure(Error.Validation("Email.InvalidFormat", "Email format is invalid."));
        }

        return Result<CustomerEmail>.Success(new CustomerEmail(trimmed.ToLowerInvariant()));
    }
}

// 2. Value Object: Money
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency = "EUR")
    {
        if (amount < 0)
        {
            return Result<Money>.Failure(Error.Validation("Money.Negative", "Money amount cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<Money>.Failure(Error.Validation("Money.InvalidCurrency", "Currency must be specified."));
        }

        return Result<Money>.Success(new Money(amount, currency.ToUpperInvariant()));
    }

    public Money Multiply(int quantity) => new(Amount * quantity, Currency);
}

// 3. Domain Entity: Rich Domain Model with Invariant Protection
public sealed class Order
{
    public Guid Id { get; private set; }
    public CustomerEmail CustomerEmail { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    // Required for EF Core materialization
    private Order() { }

    // Factory method encapsulating construction invariants
    public static Result<Order> Create(
        CustomerEmail email,
        string productName,
        int quantity,
        Money unitPrice,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return Result<Order>.Failure(Error.Validation("Order.ProductNameEmpty", "Product name cannot be empty."));
        }

        if (quantity <= 0)
        {
            return Result<Order>.Failure(Error.Validation("Order.InvalidQuantity", "Quantity must be greater than zero."));
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = email,
            ProductName = productName.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = unitPrice.Multiply(quantity),
            Status = OrderStatus.Pending,
            CreatedAtUtc = nowUtc
        };

        return Result<Order>.Success(order);
    }

    // Business method protecting the cancellation invariant
    public Result Cancel(TimeProvider timeProvider, TimeSpan cancellationWindow)
    {
        if (Status == OrderStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("Order.AlreadyCancelled", "The order is already cancelled."));
        }

        if (Status == OrderStatus.Completed)
        {
            return Result.Failure(Error.Conflict("Order.AlreadyCompleted", "Completed orders cannot be cancelled."));
        }

        var currentUtc = timeProvider.GetUtcNow();
        if (currentUtc - CreatedAtUtc > cancellationWindow)
        {
            return Result.Failure(Error.Conflict(
                "Order.CancellationWindowExpired",
                $"Orders cannot be cancelled after {cancellationWindow.TotalMinutes} minutes."));
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = currentUtc;

        return Result.Success();
    }
}

public enum OrderStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3
}
