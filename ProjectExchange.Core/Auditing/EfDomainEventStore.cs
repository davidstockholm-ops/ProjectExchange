using Microsoft.EntityFrameworkCore;
using ProjectExchange.Core.Infrastructure.Persistence;

namespace ProjectExchange.Core.Auditing;

/// <summary>
/// Persists domain events to the domain_events table. Each append writes one row with JSON payload and timestamp.
/// </summary>
public class EfDomainEventStore : IDomainEventStore
{
    private readonly ProjectExchangeDbContext _dbContext;

    public EfDomainEventStore(ProjectExchangeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task AppendAsync(string eventType, string payloadJson, string? marketId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        var entity = new DomainEventEntity
        {
            EventType = eventType,
            Payload = payloadJson,
            OccurredAt = DateTimeOffset.UtcNow,
            MarketId = string.IsNullOrWhiteSpace(marketId) ? null : marketId.Trim(),
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim()
        };
        _dbContext.DomainEvents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
