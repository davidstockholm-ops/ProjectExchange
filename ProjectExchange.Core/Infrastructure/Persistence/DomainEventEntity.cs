namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// Entity for the domain_events audit table. Stores event type, payload as JSON, timestamp, and optional MarketId/UserId for querying.
/// </summary>
public class DomainEventEntity
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>Optional; set for order/trade events to support AuditService.GetByMarketId.</summary>
    public string? MarketId { get; set; }
    /// <summary>Optional; set for order/trade events to support AuditService.GetByUserId.</summary>
    public string? UserId { get; set; }
}
