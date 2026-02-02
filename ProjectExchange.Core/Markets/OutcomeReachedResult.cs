namespace ProjectExchange.Core.Markets;

/// <summary>
/// Result of notifying that an outcome was reached (OutcomeReached). Used by any oracle (Base, Flash, Celebrity, Sports).
/// Supports optional Agentic AI fields: confidence score and source verification list.
/// </summary>
public record OutcomeReachedResult(
    string OutcomeId,
    IReadOnlyList<Guid> NewSettlementTransactionIds,
    IReadOnlyList<Guid> AlreadySettledClearingIds,
    string Message,
    decimal? ConfidenceScore = null,
    IReadOnlyList<string>? SourceVerificationList = null);
