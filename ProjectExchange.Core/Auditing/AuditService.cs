using Microsoft.EntityFrameworkCore;
using ProjectExchange.Core.Infrastructure.Persistence;

namespace ProjectExchange.Core.Auditing;

/// <summary>
/// Reads back the event stream for a given MarketId or UserId. Used for audit and support.
/// </summary>
public class AuditService
{
    private readonly ProjectExchangeDbContext _dbContext;

    public AuditService(ProjectExchangeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Returns all domain events for the given market (order placed, trade matched), oldest first.
    /// Returns empty list on DB/query errors and logs to console.
    /// </summary>
    public async Task<IReadOnlyList<AuditEventDto>> GetByMarketIdAsync(string marketId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            return Array.Empty<AuditEventDto>();

        try
        {
            var list = await _dbContext.DomainEvents
                .AsNoTracking()
                .Where(e => e.MarketId == marketId.Trim())
                .OrderBy(e => e.Id)
                .Select(e => new AuditEventDto(e.EventType, e.Payload, e.OccurredAt, e.MarketId, e.UserId))
                .ToListAsync(cancellationToken);
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuditService] GetByMarketIdAsync failed (returning empty list): {ex}");
            return Array.Empty<AuditEventDto>();
        }
    }

    /// <summary>
    /// Returns all domain events where the given user is involved (order or match side), oldest first.
    /// Returns empty list on DB/query errors (e.g. missing domain_events table) and logs to console.
    /// </summary>
    public async Task<IReadOnlyList<AuditEventDto>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Array.Empty<AuditEventDto>();

        try
        {
            var list = await _dbContext.DomainEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId.Trim())
                .OrderBy(e => e.Id)
                .Select(e => new AuditEventDto(e.EventType, e.Payload, e.OccurredAt, e.MarketId, e.UserId))
                .ToListAsync(cancellationToken);
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuditService] GetByUserIdAsync failed (returning empty list): {ex}");
            return Array.Empty<AuditEventDto>();
        }
    }
}

/// <summary>Single event in the audit stream: type, JSON payload, timestamp, optional market/user.</summary>
public record AuditEventDto(string EventType, string Payload, DateTimeOffset OccurredAt, string? MarketId, string? UserId);
