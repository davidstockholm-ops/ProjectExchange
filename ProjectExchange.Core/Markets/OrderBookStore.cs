using System.Collections.Concurrent;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// In-memory store for order books. Thread-safe; one book per outcome ID.
/// </summary>
public class OrderBookStore : IOrderBookStore
{
    private readonly ConcurrentDictionary<string, OrderBook> _books = new(StringComparer.OrdinalIgnoreCase);

    public OrderBook GetOrCreateOrderBook(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            throw new ArgumentException("OutcomeId is required.", nameof(outcomeId));
        return _books.GetOrAdd(outcomeId.Trim(), _ => new OrderBook());
    }

    public OrderBook? GetOrderBook(string outcomeId) =>
        _books.TryGetValue(outcomeId ?? string.Empty, out var book) ? book : null;
}
