using Microsoft.Extensions.Time.Testing;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;
using Xunit;

namespace MyApp.Tests.Unit.Domain;

public sealed class OrderDomainTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccessOrder()
    {
        // Arrange
        var email = CustomerEmail.Create("customer@example.com").Value;
        var unitPrice = Money.Create(25.50m, "EUR").Value;
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = Order.Create(email, "Mechanical Keyboard", 2, unitPrice, now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Mechanical Keyboard", result.Value.ProductName);
        Assert.Equal(2, result.Value.Quantity);
        Assert.Equal(51.00m, result.Value.TotalAmount.Amount);
        Assert.Equal(OrderStatus.Pending, result.Value.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidProductName_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var email = CustomerEmail.Create("customer@example.com").Value;
        var unitPrice = Money.Create(25.50m, "EUR").Value;

        // Act
        var result = Order.Create(email, invalidName!, 1, unitPrice, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Order.ProductNameEmpty", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Cancel_WithinCancellationWindow_Succeeds()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startTime);

        var email = CustomerEmail.Create("customer@example.com").Value;
        var unitPrice = Money.Create(10m).Value;
        var order = Order.Create(email, "Mousepad", 1, unitPrice, timeProvider.GetUtcNow()).Value;

        // Advance time by 15 minutes (within 30 minute window)
        timeProvider.Advance(TimeSpan.FromMinutes(15));

        // Act
        var result = order.Cancel(timeProvider, TimeSpan.FromMinutes(30));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(timeProvider.GetUtcNow(), order.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_AfterCancellationWindow_ReturnsConflictError()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startTime);

        var email = CustomerEmail.Create("customer@example.com").Value;
        var unitPrice = Money.Create(10m).Value;
        var order = Order.Create(email, "Mousepad", 1, unitPrice, timeProvider.GetUtcNow()).Value;

        // Advance time by 31 minutes (exceeding 30 minute window)
        timeProvider.Advance(TimeSpan.FromMinutes(31));

        // Act
        var result = order.Cancel(timeProvider, TimeSpan.FromMinutes(30));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Order.CancellationWindowExpired", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
}
