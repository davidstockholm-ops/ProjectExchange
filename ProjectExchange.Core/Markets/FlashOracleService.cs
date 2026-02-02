namespace ProjectExchange.Core.Markets;

/// <summary>
/// Flash oracle: fast markets without Copy-Trading or Social Traffic. Focuses on matching and auto-settlement.
/// Inherits from BaseOracleService; uses generic MarketEvent.EventType.Flash. No SimulateTrade or TradeProposed.
/// </summary>
public class FlashOracleService : BaseOracleService, IMarketOracle
{
    public const string OracleIdValue = "FlashOracle";

    /// <summary>Max duration for Flash events (minutes).</summary>
    public const int MaxFlashDurationMinutes = 15;

    public FlashOracleService(
        IOrderBookStore orderBookStore,
        IServiceProvider serviceProvider,
        IOutcomeRegistry? outcomeRegistry = null)
        : base(orderBookStore, serviceProvider, outcomeRegistry)
    {
    }

    public override string OracleId => OracleIdValue;

    /// <summary>
    /// Creates a Flash market: uses <see cref="MarketEvent.EventType.Flash"/> and caps duration at <see cref="MaxFlashDurationMinutes"/>.
    /// Delegates to base so event is stored, OrderBook created, and MarketOpened raised.
    /// </summary>
    public Task<MarketEvent> CreateFlashMarketAsync(string title, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var cappedDuration = Math.Min(Math.Max(1, durationMinutes), MaxFlashDurationMinutes);
        var evt = CreateMarketEvent(title, MarketEvent.EventType.Flash, cappedDuration, context: null);
        return Task.FromResult(evt);
    }

    /// <inheritdoc />
    protected override MarketEvent CreateMarketEventCore(string title, string type, int durationMinutes, IReadOnlyDictionary<string, string>? context)
    {
        var effectiveDuration = Math.Min(Math.Max(1, durationMinutes), MaxFlashDurationMinutes);
        var id = Guid.NewGuid();
        var outcomeId = "outcome-" + id.ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(effectiveDuration);

        return new MarketEvent(id, title ?? string.Empty, MarketEvent.EventType.Flash, outcomeId, "", OracleId, effectiveDuration, now, expiresAt);
    }
}
