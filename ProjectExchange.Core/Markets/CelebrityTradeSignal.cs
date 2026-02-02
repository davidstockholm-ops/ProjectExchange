namespace ProjectExchange.Core.Markets;

/// <summary>
/// Signal emitted when an outcome oracle (e.g. celebrity) simulates a trade. CopyTradingEngine listens and executes.
/// </summary>
/// <param name="TradeId">Unique trade identifier.</param>
/// <param name="OperatorId">Operator (celebrity) account ID.</param>
/// <param name="Amount">Trade amount.</param>
/// <param name="OutcomeId">Outcome identifier.</param>
/// <param name="OutcomeName">Display name for the outcome.</param>
/// <param name="ActorId">Optional actor/celebrity ID (e.g. "Drake", "Elon") for account naming and display.</param>
public record CelebrityTradeSignal(
    Guid TradeId,
    Guid OperatorId,
    decimal Amount,
    string OutcomeId,
    string OutcomeName,
    string? ActorId = null);
