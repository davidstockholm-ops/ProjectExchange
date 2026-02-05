using ProjectExchange.Core.Auditing;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies that MockMatchingEngine writes domain events via IDomainEventStore when orders are placed.
/// </summary>
public class EventSourcingTests
{
    [Fact]
    public async Task MockMatchingEngine_WhenOrderPlaced_CallsAppendAsyncOnDomainEventStore()
    {
        var store = new OrderBookStore();
        var eventStore = new RecordingDomainEventStore();
        var engine = new MockMatchingEngine(store, eventStore);

        var order = new Order(
            Guid.NewGuid(),
            "user-1",
            "drake-album",
            OrderType.Bid,
            0.85m,
            10m,
            "operator-1");

        await engine.ProcessOrderAsync(order);

        Assert.True(eventStore.AppendCalls.Count >= 1, "AppendAsync should be called at least once when an order is placed.");
        var orderPlacedCall = eventStore.AppendCalls.FirstOrDefault(c =>
            c.EventType == "OrderPlaced" &&
            c.MarketId == "drake-album" &&
            c.UserId == "user-1");
        Assert.NotNull(orderPlacedCall.Payload);
        Assert.Contains(order.Id.ToString(), orderPlacedCall.Payload);
        Assert.Contains("0.85", orderPlacedCall.Payload);
    }

    [Fact]
    public async Task MockMatchingEngine_WhenOrderMatches_CallsAppendAsyncForTradeMatched()
    {
        var store = new OrderBookStore();
        var eventStore = new RecordingDomainEventStore();
        var engine = new MockMatchingEngine(store, eventStore);

        var bid = new Order(Guid.NewGuid(), "buyer", "test-market", OrderType.Bid, 0.50m, 10m, "op");
        var ask = new Order(Guid.NewGuid(), "seller", "test-market", OrderType.Ask, 0.50m, 10m, "op");

        await engine.ProcessOrderAsync(bid);
        await engine.ProcessOrderAsync(ask);

        Assert.Contains(eventStore.AppendCalls, c => c.EventType == "TradeMatched");
        Assert.Contains(eventStore.AppendCalls, c => c.EventType == "TradeMatched" && c.MarketId == "test-market");
    }

    /// <summary>Fake that records all AppendAsync calls for assertions.</summary>
    private sealed class RecordingDomainEventStore : IDomainEventStore
    {
        public List<(string EventType, string Payload, string? MarketId, string? UserId)> AppendCalls { get; } = new();

        public Task AppendAsync(string eventType, string payloadJson, string? marketId = null, string? userId = null, CancellationToken cancellationToken = default)
        {
            AppendCalls.Add((eventType, payloadJson, marketId, userId));
            return Task.CompletedTask;
        }
    }
}
