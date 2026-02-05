namespace ProjectExchange.Core.Settlement;

/// <summary>
/// Outbound gateway to external settlement/payment systems. Called after market resolution to notify or trigger external payouts.
/// Resilience (Retry, Circuit Breaker) is applied in the implementation.
/// </summary>
public interface ISettlementGateway
{
    /// <summary>
    /// Notifies the external system that a market was resolved and settlement was booked.
    /// No-op or HTTP call depending on configuration.
    /// </summary>
    Task NotifySettlementAsync(string outcomeId, string winningAssetType, int accountsSettled, decimal totalUsdPaidOut, CancellationToken cancellationToken = default);
}
