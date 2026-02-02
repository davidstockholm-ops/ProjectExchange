using System.Collections.Concurrent;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Celebrity;

/// <summary>
/// Outcome oracle that handles multiple celebrities (actors) by ID. Extends BaseOracleService with
/// SimulateTrade and TradeProposed for copy-trading. Creates market events (Flash/Base) via the base
/// generic logic; celebrity-specific CreateMarketEvent(actorId, ...) passes actorId in context.
/// </summary>
public class CelebrityOracleService : BaseOracleService, IOutcomeOracle
{
    public const string OracleIdValue = "CelebrityOracle";

    public CelebrityOracleService(
        IOrderBookStore orderBookStore,
        IServiceProvider serviceProvider,
        IOutcomeRegistry? outcomeRegistry = null)
        : base(orderBookStore, serviceProvider, outcomeRegistry)
    {
    }

    public override string OracleId => OracleIdValue;

    public event EventHandler<CelebrityTradeSignal>? TradeProposed;

    /// <summary>
    /// Celebrity-specific overload: creates a market event for the given actor. Delegates to base CreateMarketEvent
    /// so that base logic runs: event stored in Events, OutcomeId registered, OrderBook created, MarketOpened raised.
    /// </summary>
    public MarketEvent CreateMarketEvent(string actorId, string title, string type, int durationMinutes)
    {
        var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["actorId"] = string.IsNullOrWhiteSpace(actorId) ? "Unknown" : actorId.Trim()
        };
        return CreateMarketEvent(title, type, durationMinutes, context);
    }

    protected override MarketEvent CreateMarketEventCore(string title, string type, int durationMinutes, IReadOnlyDictionary<string, string>? context)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var effectiveDuration = NormalizeDuration(type, durationMinutes);
        var id = Guid.NewGuid();
        var outcomeId = "outcome-" + id.ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(effectiveDuration);
        var actorId = context != null && context.TryGetValue("actorId", out var a) ? a : "Unknown";

        return new MarketEvent(id, title, type, outcomeId, actorId, OracleId, effectiveDuration, now, expiresAt);
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
