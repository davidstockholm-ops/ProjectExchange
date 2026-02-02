namespace ProjectExchange.Core.Markets;

/// <summary>
/// Standard interface for any event oracle (Base, Flash, Celebrity, Sports).
/// Defines creating markets (CreateMarketEvent), listing active events (GetActiveEvents),
/// and triggering OutcomeReached for auto-settlement. Implementations can add domain-specific
/// behaviour (e.g. Celebrity: SimulateTrade, TradeProposed).
/// </summary>
public interface IMarketOracle
{
    /// <summary>Unique identifier for this oracle (e.g. "CelebrityOracle", "FlashOracle"). Used by MarketEvent to track responsibility.</summary>
    string OracleId { get; }

    /// <summary>Raised when a new market is opened (OrderBook registered). For AI agents / subscribers.</summary>
    event EventHandler<MarketOpenedEventArgs>? MarketOpened;

    /// <summary>
    /// Creates a market event and registers its OrderBook. Event type can be Base, Flash, Celebrity, Sports, etc.
    /// </summary>
    /// <param name="title">Event title.</param>
    /// <param name="type">Event type (e.g. "Base", "Flash", "Celebrity", "Sports").</param>
    /// <param name="durationMinutes">Duration in minutes.</param>
    /// <param name="context">Optional context (e.g. actorId for celebrity). Implementation-specific.</param>
    MarketEvent CreateMarketEvent(string title, string type, int durationMinutes, IReadOnlyDictionary<string, string>? context = null);

    /// <summary>Returns all currently tradeable (non-expired) markets for this oracle.</summary>
    IReadOnlyList<MarketEvent> GetActiveEvents();

    /// <summary>
    /// Triggers OutcomeReached for the given outcome: runs auto-settlement for any clearing transactions.
    /// Idempotent. Optional confidence score and source verification list for Agentic AI.
    /// </summary>
    Task<OutcomeReachedResult> NotifyOutcomeReachedAsync(
        string outcomeId,
        decimal? confidenceScore = null,
        IReadOnlyList<string>? sourceVerificationList = null,
        CancellationToken cancellationToken = default);
}
