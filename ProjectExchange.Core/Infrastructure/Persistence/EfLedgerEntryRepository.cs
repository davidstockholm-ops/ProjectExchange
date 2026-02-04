using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// Persists LedgerEntry via DbContext.Set{T} so table names come from the EF model (avoids "relation does not exist" in Postgres).
/// </summary>
public class EfLedgerEntryRepository : ILedgerEntryRepository
{
    private readonly ProjectExchangeDbContext _context;

    public EfLedgerEntryRepository(ProjectExchangeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddRangeAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
            return;

        var entities = entries.Select(e => new LedgerEntryEntity
        {
            Id = Guid.NewGuid(),
            AccountId = e.AccountId,
            AssetType = e.AssetType,
            Amount = e.Amount,
            Direction = e.Direction,
            Timestamp = e.Timestamp
        }).ToList();

        await _context.Set<LedgerEntryEntity>().AddRangeAsync(entities, cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<LedgerEntryEntity>()
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new LedgerEntry(
            e.AccountId,
            e.AssetType,
            e.Amount,
            e.Direction,
            e.Timestamp)).ToList();
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetType))
            return Array.Empty<LedgerEntry>();

        var entities = await _context.Set<LedgerEntryEntity>()
            .AsNoTracking()
            .Where(e => e.AssetType == assetType.Trim())
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new LedgerEntry(
            e.AccountId,
            e.AssetType,
            e.Amount,
            e.Direction,
            e.Timestamp)).ToList();
    }
}
