using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Tests for the matching engine: basic match, no match, partial fill, price priority, quantity depletion.
/// </summary>
public class OrderBookTests
{
    private static Order NewBid(Guid? id, Guid? userId, decimal price, decimal quantity) =>
        new(id ?? Guid.NewGuid(), (userId ?? Guid.NewGuid()).ToString(), "outcome-x", OrderType.Bid, price, quantity);

    private static Order NewAsk(Guid? id, Guid? userId, decimal price, decimal quantity) =>
        new(id ?? Guid.NewGuid(), (userId ?? Guid.NewGuid()).ToString(), "outcome-x", OrderType.Ask, price, quantity);

    [Fact]
    public void BasicMatch_Bid060_Ask050_MatchesAt050()
    {
        var book = new OrderBook();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        book.AddOrder(NewBid(null, buyerId, 0.60m, 10m));
        book.AddOrder(NewAsk(null, sellerId, 0.50m, 10m));

        var results = book.MatchOrders();

        Assert.Single(results);
        var m = results[0];
        Assert.Equal(0.50m, m.Price);
        Assert.Equal(10m, m.Quantity);
        Assert.Equal(buyerId.ToString(), m.BuyerUserId);
        Assert.Equal(sellerId.ToString(), m.SellerUserId);
        Assert.Empty(book.Bids);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public void NoMatch_Bid040_Ask050_NoMatchOccurs()
    {
        var book = new OrderBook();
        book.AddOrder(NewBid(null, null, 0.40m, 10m));
        book.AddOrder(NewAsk(null, null, 0.50m, 10m));

        var results = book.MatchOrders();

        Assert.Empty(results);
        Assert.Single(book.Bids);
        Assert.Single(book.Asks);
        Assert.Equal(0.40m, book.Bids[0].Price);
        Assert.Equal(10m, book.Bids[0].Quantity);
        Assert.Equal(0.50m, book.Asks[0].Price);
        Assert.Equal(10m, book.Asks[0].Quantity);
    }

    [Fact]
    public void PartialFill_Bid100_Ask40_BidPartiallyFilled40_Remaining60InBook()
    {
        var book = new OrderBook();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        book.AddOrder(NewBid(null, buyerId, 0.60m, 100m));
        book.AddOrder(NewAsk(null, sellerId, 0.50m, 40m));

        var results = book.MatchOrders();

        Assert.Single(results);
        Assert.Equal(0.50m, results[0].Price);
        Assert.Equal(40m, results[0].Quantity);
        Assert.Equal(buyerId.ToString(), results[0].BuyerUserId);
        Assert.Equal(sellerId.ToString(), results[0].SellerUserId);
        Assert.Single(book.Bids);
        Assert.Equal(60m, book.Bids[0].Quantity);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public void PricePriority_BidMatchesWithLowestAvailableAskFirst()
    {
        var book = new OrderBook();
        var buyerId = Guid.NewGuid();
        var sellerLowId = Guid.NewGuid();
        var sellerMidId = Guid.NewGuid();
        var sellerHighId = Guid.NewGuid();
        book.AddOrder(NewAsk(null, sellerHighId, 0.70m, 10m));
        book.AddOrder(NewAsk(null, sellerLowId, 0.50m, 10m));
        book.AddOrder(NewAsk(null, sellerMidId, 0.60m, 10m));
        book.AddOrder(NewBid(null, buyerId, 0.65m, 10m));

        var results = book.MatchOrders();

        Assert.Single(results);
        Assert.Equal(0.50m, results[0].Price);
        Assert.Equal(sellerLowId.ToString(), results[0].SellerUserId);
        Assert.Equal(buyerId.ToString(), results[0].BuyerUserId);
        Assert.Empty(book.Bids);
        Assert.Equal(2, book.Asks.Count);
        Assert.Equal(0.60m, book.Asks[0].Price);
        Assert.Equal(0.70m, book.Asks[1].Price);
    }

    [Fact]
    public void PricePriority_LargeBidClearsMultipleAsksInPriceOrder_BookEmptyAfter()
    {
        var book = new OrderBook();
        var buyerId = Guid.NewGuid();
        var sellerLowId = Guid.NewGuid();
        var sellerMidId = Guid.NewGuid();
        var sellerHighId = Guid.NewGuid();
        book.AddOrder(NewAsk(null, sellerHighId, 0.70m, 10m));
        book.AddOrder(NewAsk(null, sellerLowId, 0.50m, 10m));
        book.AddOrder(NewAsk(null, sellerMidId, 0.60m, 10m));
        book.AddOrder(NewBid(null, buyerId, 0.75m, 30m));

        var results = book.MatchOrders();

        Assert.Equal(3, results.Count);
        Assert.Equal(0.50m, results[0].Price);
        Assert.Equal(10m, results[0].Quantity);
        Assert.Equal(sellerLowId.ToString(), results[0].SellerUserId);
        Assert.Equal(0.60m, results[1].Price);
        Assert.Equal(10m, results[1].Quantity);
        Assert.Equal(sellerMidId.ToString(), results[1].SellerUserId);
        Assert.Equal(0.70m, results[2].Price);
        Assert.Equal(10m, results[2].Quantity);
        Assert.Equal(sellerHighId.ToString(), results[2].SellerUserId);
        Assert.All(results, r => Assert.Equal(buyerId.ToString(), r.BuyerUserId));
        Assert.Empty(book.Bids);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public void QuantityDepletion_OrdersRemovedWhenQuantityReachesZero()
    {
        var book = new OrderBook();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        book.AddOrder(NewBid(null, buyerId, 0.60m, 50m));
        book.AddOrder(NewAsk(null, sellerId, 0.50m, 50m));

        var results = book.MatchOrders();

        Assert.Single(results);
        Assert.Equal(50m, results[0].Quantity);
        Assert.Empty(book.Bids);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public void QuantityDepletion_MultipleMatches_OnlyFilledOrdersRemoved()
    {
        var book = new OrderBook();
        book.AddOrder(NewBid(null, Guid.NewGuid(), 0.60m, 30m));
        book.AddOrder(NewBid(null, Guid.NewGuid(), 0.55m, 20m));
        book.AddOrder(NewAsk(null, Guid.NewGuid(), 0.50m, 25m));
        book.AddOrder(NewAsk(null, Guid.NewGuid(), 0.52m, 15m));

        var results = book.MatchOrders();

        Assert.Equal(3, results.Count);
        Assert.Equal(25m, results[0].Quantity);
        Assert.Equal(5m, results[1].Quantity);
        Assert.Equal(10m, results[2].Quantity);
        Assert.Single(book.Bids);
        Assert.Equal(0.55m, book.Bids[0].Price);
        Assert.Equal(10m, book.Bids[0].Quantity);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public void AddOrder_NullOrder_ThrowsArgumentNullException()
    {
        var book = new OrderBook();
        Assert.Throws<ArgumentNullException>(() => book.AddOrder(null!));
    }

    [Fact]
    public void MatchOrders_EmptyBook_ReturnsEmpty()
    {
        var book = new OrderBook();
        var results = book.MatchOrders();
        Assert.Empty(results);
    }

    [Fact]
    public void RemoveOrdersByOperator_RemovesOnlyOrdersForGivenOperator()
    {
        var book = new OrderBook();
        var bidMm = new Order(Guid.NewGuid(), "u1", "outcome-x", OrderType.Bid, 0.50m, 10m, "mm-provider");
        var askMm = new Order(Guid.NewGuid(), "u1", "outcome-x", OrderType.Ask, 0.55m, 10m, "mm-provider");
        var bidRetail = new Order(Guid.NewGuid(), "u2", "outcome-x", OrderType.Bid, 0.45m, 5m, "apple-pay");
        book.AddOrder(bidMm);
        book.AddOrder(askMm);
        book.AddOrder(bidRetail);

        var removed = book.RemoveOrdersByOperator("mm-provider");

        Assert.Equal(2, removed);
        Assert.Single(book.Bids);
        Assert.Empty(book.Asks);
        Assert.Equal("apple-pay", book.Bids[0].OperatorId);
    }

    [Fact]
    public void RemoveOrdersByOperator_EmptyBook_ReturnsZero()
    {
        var book = new OrderBook();
        var removed = book.RemoveOrdersByOperator("mm-provider");
        Assert.Equal(0, removed);
    }
}
