using System.Collections.Concurrent;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Drake;

/// <summary>
/// Outcome oracle that handles multiple celebrities (actors) by ID. Simulates celebrity trade events
/// (e.g. Drake or Elon betting on an outcome). Emits TradeProposed for CopyTradingEngine; creates
/// market events (Flash/Base), registers OrderBooks, and broadcasts MarketOpened.
/// </summary>
public class CelebrityOracleService : IOutcomeOracle
{
    public const string OracleIdValue = "CelebrityOracle";

    private readonly IOrderBookStore _orderBookStore;
    private readonly IOutcomeRegistry? _outcomeRegistry;
    private readonly ConcurrentDictionary<Guid, MarketEvent> _events = new();

    public CelebrityOracleService(IOrderBookStore orderBookStore, IOutcomeRegistry? outcomeRegistry = null)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
        _outcomeRegistry = outcomeRegistry;
    }

    public string OracleId => OracleIdValue;

    public event EventHandler<CelebrityTradeSignal>? TradeProposed;
    public event EventHandler<MarketOpenedEventArgs>? MarketOpened;

    /// <summary>
    /// Creates a market event for the given actor (celebrity). Registers an OrderBook for the outcome and broadcasts MarketOpened.
    /// </summary>
    public MarketEvent CreateMarketEvent(string actorId, string title, string type, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var effectiveDuration = NormalizeDuration(type, durationMinutes);
        var id = Guid.NewGuid();
        var outcomeId = "outcome-" + id.ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(effectiveDuration);
        var safeActorId = string.IsNullOrWhiteSpace(actorId) ? "Unknown" : actorId.Trim();

        var evt = new MarketEvent(id, title, type, outcomeId, safeActorId, OracleId, effectiveDuration, now, expiresAt);
        _events[id] = evt;

        _outcomeRegistry?.Register(outcomeId);
        _orderBookStore.GetOrCreateOrderBook(outcomeId);
        MarketOpened?.Invoke(this, new MarketOpenedEventArgs(evt));

        return evt;
    }

    public IReadOnlyList<MarketEvent> GetActiveEvents()
    {
        var now = DateTimeOffset.UtcNow;
        return _events.Values.Where(e => e.ExpiresAt > now).ToList();
    }

    /// <summary>
    /// Simulates a celebrity placing a bet on an outcome. Raises TradeProposed (Clearing-phase flow).
    /// </summary>
    public CelebrityTradeSignal SimulateTrade(Guid operatorId, decimal amount, string outcomeId, string outcomeName = "Outcome X", string? actorId = null)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        if (string.IsNullOrWhiteSpace(outcomeId))
            throw new ArgumentException("OutcomeId is required.", nameof(outcomeId));

        var signal = new CelebrityTradeSignal(
            TradeId: Guid.NewGuid(),
            OperatorId: operatorId,
            Amount: amount,
            OutcomeId: outcomeId,
            OutcomeName: string.IsNullOrWhiteSpace(outcomeName) ? "Outcome X" : outcomeName,
            ActorId: string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim());

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
