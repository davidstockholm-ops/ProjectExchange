namespace ProjectExchange.Core.Markets;

/// <summary>
/// Service that settles outcomes by posting Settlement-phase transactions for clearing transactions.
/// Used by any oracle (Base, Flash, Celebrity, Sports) when OutcomeReached is triggered.
/// Identifies clearing transactions by OutcomeId only (universal secondary market).
/// </summary>
public interface IOutcomeSettlementService
{
    /// <summary>
    /// Settles the given outcome: finds clearing transaction(s) for this outcome and posts Settlement-phase reverses.
    /// Idempotent. Optional confidence score and source verification list for Agentic AI.
    /// </summary>
    Task<OutcomeReachedResult> SettleOutcomeAsync(
        string outcomeId,
        decimal? confidenceScore = null,
        IReadOnlyList<string>? sourceVerificationList = null,
        CancellationToken cancellationToken = default);
}
