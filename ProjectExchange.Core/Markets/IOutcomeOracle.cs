namespace ProjectExchange.Core.Markets;

/// <summary>
/// Outcome oracle: creates market events and proposes trades for settlement.
/// Implementations (e.g. CelebrityOracleService) can handle multiple actors (celebrities) by ID.
/// </summary>
public interface IOutcomeOracle
{
    /// <summary>Unique identifier for this oracle (e.g. "CelebrityOracle"). Used by MarketEvent to track responsibility.</summary>
    string OracleId { get; }

    /// <summary>Raised when the oracle simulates a trade. CopyTradingEngine subscribes and posts to the ledger.</summary>
    event EventHandler<CelebrityTradeSignal>? TradeProposed;

    /// <summary>Raised when a new market is opened (OrderBook registered). For AI agents / subscribers.</summary>
    event EventHandler<MarketOpenedEventArgs>? MarketOpened;

    /// <summary>
    /// Creates a market event for the given actor (celebrity). Registers an OrderBook for the outcome and broadcasts MarketOpened.
    /// </summary>
    /// <param name="actorId">Identifier of the celebrity/actor (e.g. "Drake", "Elon").</param>
    /// <param name="title">Event title.</param>
    /// <param name="type">Flash or Base.</param>
    /// <param name="durationMinutes">Duration in minutes.</param>
    MarketEvent CreateMarketEvent(string actorId, string title, string type, int durationMinutes);

    /// <summary>Returns all currently tradeable (non-expired) markets for this oracle.</summary>
    IReadOnlyList<MarketEvent> GetActiveEvents();

    /// <summary>
    /// Simulates a celebrity trade. Raises TradeProposed (Clearing-phase flow).
    /// </summary>
    /// <param name="operatorId">Operator (celebrity) account ID.</param>
    /// <param name="amount">Trade amount.</param>
    /// <param name="outcomeId">Outcome identifier.</param>
    /// <param name="outcomeName">Display name for the outcome.</param>
    /// <param name="actorId">Optional actor ID (e.g. "Drake", "Elon"); used for account naming and display.</param>
    CelebrityTradeSignal SimulateTrade(Guid operatorId, decimal amount, string outcomeId, string outcomeName = "Outcome X", string? actorId = null);
}
