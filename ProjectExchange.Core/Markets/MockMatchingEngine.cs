namespace ProjectExchange.Core.Markets;

/// <summary>
/// Mock matching engine: adds the order to the book keyed by order.OutcomeId (e.g. marketId), runs matching, returns result.
/// </summary>
public class MockMatchingEngine : IMatchingEngine
{
    private readonly IOrderBookStore _orderBookStore;

    public MockMatchingEngine(IOrderBookStore orderBookStore)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
    }

    /// <inheritdoc />
    public Task<ProcessOrderResult> ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        var book = _orderBookStore.GetOrCreateOrderBook(order.OutcomeId);
        book.AddOrder(order);
        var matches = book.MatchOrders();
        return Task.FromResult(new ProcessOrderResult(order.Id, matches));
    }
}
