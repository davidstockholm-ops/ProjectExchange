namespace ProjectExchange.Core.Drake;

/// <summary>
/// Signal emitted when Drake (oracle) simulates a trade. CopyTradingEngine listens and executes.
/// </summary>
public record DrakeTradeSignal(
    Guid TradeId,
    Guid OperatorId,
    decimal Amount,
    string OutcomeId,
    string OutcomeName);
