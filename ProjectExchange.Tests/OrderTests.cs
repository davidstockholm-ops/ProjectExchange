using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Order constructor validation: price, quantity, outcomeId.
/// </summary>
public class OrderTests
{
    [Fact]
    public void Constructor_PriceBelowZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), "outcome-x", OrderType.Bid, -0.01m, 10m));
    }

    [Fact]
    public void Constructor_PriceAboveOne_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), "outcome-x", OrderType.Bid, 1.01m, 10m));
    }

    [Fact]
    public void Constructor_QuantityZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), "outcome-x", OrderType.Bid, 0.50m, 0m));
    }

    [Fact]
    public void Constructor_QuantityNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), "outcome-x", OrderType.Bid, 0.50m, -10m));
    }

    [Fact]
    public void Constructor_OutcomeIdNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), null!, OrderType.Bid, 0.50m, 10m));
    }

    [Fact]
    public void Constructor_OutcomeIdEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), "   ", OrderType.Bid, 0.50m, 10m));
    }

    [Fact]
    public void Constructor_ValidArgs_Succeeds()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "outcome-x", OrderType.Ask, 0.50m, 10m);
        Assert.Equal(10m, order.Quantity);
        Assert.Equal(0.50m, order.Price);
        Assert.Equal("outcome-x", order.OutcomeId);
    }
}
