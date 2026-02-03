using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Services;

/// <summary>
/// Aggregates LedgerEntries per account into a portfolio view (asset type -> balance).
/// Balance per asset = Sum(Debit amounts) - Sum(Credit amounts).
/// </summary>
public class PortfolioService
{
    private readonly ILedgerEntryRepository _ledgerEntryRepository;

    public PortfolioService(ILedgerEntryRepository ledgerEntryRepository)
    {
        _ledgerEntryRepository = ledgerEntryRepository ?? throw new ArgumentNullException(nameof(ledgerEntryRepository));
    }

    /// <summary>
    /// Returns the portfolio for the given account: each asset type and its balance (Debits - Credits).
    /// Only includes assets with non-zero balance.
    /// </summary>
    /// <param name="accountId">Account ID (Guid string).</param>
    /// <returns>Asset type -> balance. Empty dictionary if account has no entries or invalid id.</returns>
    public async Task<IReadOnlyDictionary<string, decimal>> GetPortfolioAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return new Dictionary<string, decimal>();

        if (!Guid.TryParse(accountId.Trim(), out var accountGuid))
            return new Dictionary<string, decimal>();

        var entries = await _ledgerEntryRepository.GetByAccountIdAsync(accountGuid, cancellationToken);
        var balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            if (!balances.ContainsKey(e.AssetType))
                balances[e.AssetType] = 0;

            balances[e.AssetType] += e.Direction == EntryType.Debit ? e.Amount : -e.Amount;
        }

        return balances.Where(kv => kv.Value != 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
