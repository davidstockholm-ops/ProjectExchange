namespace ProjectExchange.Core.Auditing;

/// <summary>
/// Appends domain events (order placed, trade matched) for audit trail. Used by the matching path to record every order and match.
/// </summary>
public interface IDomainEventStore
{
    /// <summary>
    /// Appends an event with the given type, JSON payload, and optional MarketId/UserId for indexing.
    /// </summary>
    Task AppendAsync(string eventType, string payloadJson, string? marketId = null, string? userId = null, CancellationToken cancellationToken = default);
}
