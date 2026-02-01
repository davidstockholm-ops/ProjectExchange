namespace ProjectExchange.Core.Markets;

/// <summary>
/// Shared store for order books keyed by outcome ID. Singleton so all requests see the same books.
/// </summary>
public interface IOrderBookStore
{
    OrderBook GetOrCreateOrderBook(string outcomeId);
    OrderBook? GetOrderBook(string outcomeId);
}
