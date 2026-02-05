namespace ProjectExchange.Core.Markets;

/// <summary>
/// In-memory store of executed trades per market for GET /api/secondary/trades/{marketId}.
/// </summary>
public interface ITradeHistoryStore
{
    void Record(string marketId, decimal price, decimal quantity, string side);
    IReadOnlyList<TradeHistoryEntry> GetByMarketId(string marketId);
}

/// <summary>
/// Single executed trade (one row in trade history).
/// </summary>
public record TradeHistoryEntry(Guid Id, DateTimeOffset Time, decimal Price, decimal Quantity, string Side);
