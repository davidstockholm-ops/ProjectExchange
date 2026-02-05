using System.Collections.Concurrent;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// In-memory trade history; records trades when orders match. Thread-safe.
/// </summary>
public class TradeHistoryStore : ITradeHistoryStore
{
    private readonly ConcurrentDictionary<string, List<TradeHistoryEntry>> _byMarket = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string marketId, decimal price, decimal quantity, string side)
    {
        if (string.IsNullOrWhiteSpace(marketId)) return;
        var key = marketId.Trim();
        var list = _byMarket.GetOrAdd(key, _ => new List<TradeHistoryEntry>());
        lock (list)
        {
            list.Add(new TradeHistoryEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, price, quantity, side));
        }
    }

    public IReadOnlyList<TradeHistoryEntry> GetByMarketId(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId)) return Array.Empty<TradeHistoryEntry>();
        if (!_byMarket.TryGetValue(marketId.Trim(), out var list))
            return Array.Empty<TradeHistoryEntry>();
        lock (list)
        {
            return list.OrderByDescending(t => t.Time).ToList();
        }
    }
}
