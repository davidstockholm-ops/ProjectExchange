using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies MarketMakerService matching logic and that trades are logged correctly.
/// Uses same flow: bid then ask at same price -> match -> TradeHistoryStore.Record.
/// </summary>
public class MarketMakerServiceTests
{
    private const string MarketId = "drake-album";
    private const string OperatorId = "mm-provider";
    private const string UserId = "market-maker";

    [Fact]
    public async Task MatchingPair_BidThenAskAtSamePrice_RecordsOneTradeInStore()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();

        decimal price = 0.85m;
        decimal quantity = 25m;

        var bid = new Order(Guid.NewGuid(), UserId, MarketId, OrderType.Bid, price, quantity, OperatorId);
        var bidResult = await engine.ProcessOrderAsync(bid);
        Assert.Empty(bidResult.Matches);

        foreach (var m in bidResult.Matches)
            tradeHistory.Record(MarketId, m.Price, m.Quantity, "Buy");

        var ask = new Order(Guid.NewGuid(), UserId, MarketId, OrderType.Ask, price, quantity, OperatorId);
        var askResult = await engine.ProcessOrderAsync(ask);
        Assert.Single(askResult.Matches);
        Assert.Equal(price, askResult.Matches[0].Price);
        Assert.Equal(quantity, askResult.Matches[0].Quantity);

        foreach (var m in askResult.Matches)
            tradeHistory.Record(MarketId, m.Price, m.Quantity, "Sell");

        var trades = tradeHistory.GetByMarketId(MarketId);
        Assert.Single(trades);
        Assert.Equal(0.85m, trades[0].Price);
        Assert.Equal(25m, trades[0].Quantity);
        Assert.Equal("Sell", trades[0].Side);
    }

    [Fact]
    public async Task SingleOrder_NoMatch_RecordsNothingInStore()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();

        var order = new Order(Guid.NewGuid(), UserId, MarketId, OrderType.Bid, 0.82m, 10m, OperatorId);
        var result = await engine.ProcessOrderAsync(order);
        Assert.Empty(result.Matches);

        foreach (var m in result.Matches)
            tradeHistory.Record(MarketId, m.Price, m.Quantity, "Buy");

        Assert.Empty(tradeHistory.GetByMarketId(MarketId));
    }
}
