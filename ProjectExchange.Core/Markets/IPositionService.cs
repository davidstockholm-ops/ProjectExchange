namespace ProjectExchange.Core.Markets;

/// <summary>
/// Computes a user's net position per outcome by aggregating TradeMatched events from the domain event store.
/// </summary>
public interface IPositionService
{
    /// <summary>
    /// Gets the net position for a user: aggregated from all TradeMatched events where the user was buyer (+quantity) or seller (-quantity).
    /// Optionally scoped to a single market (outcome ID).
    /// </summary>
    /// <param name="userId">User to compute position for.</param>
    /// <param name="marketId">Optional. If set, only positions for this outcome/market are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-outcome net position (e.g. +100 YES for outcome "drake-album-yes").</returns>
    Task<IReadOnlyList<NetPositionDto>> GetNetPositionAsync(string userId, string? marketId = null, CancellationToken cancellationToken = default);
}

/// <summary>Net position in one outcome: e.g. +100 YES for outcome "drake-album-yes".</summary>
public record NetPositionDto(string OutcomeId, decimal NetQuantity);
