namespace ProjectExchange.Core.Markets;

/// <summary>
/// Celebrity (and copy-trading) oracle: extends IMarketOracle with trade simulation and TradeProposed.
/// Creates market events with actor context and simulates celebrity trades for settlement.
/// </summary>
public interface IOutcomeOracle : IMarketOracle
{
    /// <summary>Raised when the oracle simulates a trade. CopyTradingEngine subscribes and posts to the ledger.</summary>
    event EventHandler<CelebrityTradeSignal>? TradeProposed;

    /// <summary>
    /// Creates a market event for the given actor (celebrity). Convenience overload; calls base CreateMarketEvent with context.
    /// </summary>
    /// <param name="actorId">Identifier of the celebrity/actor (e.g. "Drake", "Elon").</param>
    /// <param name="title">Event title.</param>
    /// <param name="type">Flash or Base.</param>
    /// <param name="durationMinutes">Duration in minutes.</param>
    MarketEvent CreateMarketEvent(string actorId, string title, string type, int durationMinutes);

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
