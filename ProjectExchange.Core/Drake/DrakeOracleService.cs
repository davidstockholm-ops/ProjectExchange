using System.Collections.Concurrent;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Drake;

/// <summary>
/// Simulates celebrity trade events (e.g. Drake betting on an outcome).
/// Emits <see cref="TradeProposed"/> for CopyTradingEngine to execute; all traffic uses Clearing phase.
/// Also creates market events (Flash/Base), registers OrderBooks, and broadcasts MarketOpened.
/// </summary>
public class DrakeOracleService
{
    private readonly IOrderBookStore _orderBookStore;
    private readonly ConcurrentDictionary<Guid, MarketEvent> _events = new();

    public DrakeOracleService(IOrderBookStore orderBookStore)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
    }

    /// <summary>Raised when Drake simulates a trade. CopyTradingEngine subscribes and posts to the ledger.</summary>
    public event EventHandler<DrakeTradeSignal>? TradeProposed;

    /// <summary>Raised when a new market is opened (OrderBook registered). For AI agents / subscribers.</summary>
    public event EventHandler<MarketOpenedEventArgs>? MarketOpened;

    /// <summary>
    /// Creates a market event: Flash (short expiry) or Base (longer expiry).
    /// Registers an OrderBook for the outcome and broadcasts MarketOpened.
    /// </summary>
    public MarketEvent CreateMarketEvent(string title, string type, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var effectiveDuration = NormalizeDuration(type, durationMinutes);
        var id = Guid.NewGuid();
        var outcomeId = "outcome-" + id.ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(effectiveDuration);

        var evt = new MarketEvent(id, title, type, outcomeId, effectiveDuration, now, expiresAt);
        _events[id] = evt;

        _orderBookStore.GetOrCreateOrderBook(outcomeId);
        MarketOpened?.Invoke(this, new MarketOpenedEventArgs(evt));

        return evt;
    }

    /// <summary>Returns all currently tradeable (non-expired) markets.</summary>
    public IReadOnlyList<MarketEvent> GetActiveEvents()
    {
        var now = DateTimeOffset.UtcNow;
        return _events.Values.Where(e => e.ExpiresAt > now).ToList();
    }

    /// <summary>
    /// Simulates Drake placing a bet on an outcome. Raises <see cref="TradeProposed"/> (Clearing-phase flow).
    /// </summary>
    public DrakeTradeSignal SimulateTrade(Guid operatorId, decimal amount, string outcomeId, string outcomeName = "Outcome X")
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        if (string.IsNullOrWhiteSpace(outcomeId))
            throw new ArgumentException("OutcomeId is required.", nameof(outcomeId));

        var signal = new DrakeTradeSignal(
            TradeId: Guid.NewGuid(),
            OperatorId: operatorId,
            Amount: amount,
            OutcomeId: outcomeId,
            OutcomeName: string.IsNullOrWhiteSpace(outcomeName) ? "Outcome X" : outcomeName);

        TradeProposed?.Invoke(this, signal);
        return signal;
    }

    private static int NormalizeDuration(string type, int durationMinutes)
    {
        if (string.Equals(type, "Flash", StringComparison.OrdinalIgnoreCase))
            return Math.Min(durationMinutes, 15);
        if (string.Equals(type, "Base", StringComparison.OrdinalIgnoreCase))
            return Math.Max(durationMinutes, 60);
        return durationMinutes;
    }
}
