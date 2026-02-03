using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Accounting.Domain.Abstractions;

/// <summary>
/// Persists ledger entries (double-entry outcome ledger) to e.g. JournalEntries table.
/// </summary>
public interface ILedgerEntryRepository
{
    Task AddRangeAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Gets all ledger entries for the given account (for portfolio aggregation).</summary>
    Task<IReadOnlyList<LedgerEntry>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Gets all ledger entries for the given asset type (for settlement: find holders).</summary>
    Task<IReadOnlyList<LedgerEntry>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default);
}
