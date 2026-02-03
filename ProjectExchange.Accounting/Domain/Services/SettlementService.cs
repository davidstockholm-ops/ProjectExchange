using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Services;

/// <summary>
/// Resolves a market by settling all holders of the winning outcome: removes their tokens and pays USD (e.g. 1.00 per token).
/// Uses a single transaction so either all holders are paid or none.
/// </summary>
public class SettlementService
{
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly AccountingService _accountingService;

    public SettlementService(
        ILedgerEntryRepository ledgerEntryRepository,
        AccountingService accountingService)
    {
        _ledgerEntryRepository = ledgerEntryRepository ?? throw new ArgumentNullException(nameof(ledgerEntryRepository));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
    }

    /// <summary>
    /// Resolves the market for the winning outcome: finds all accounts holding the winning asset,
    /// computes each balance (Debit - Credit), and pays out USD per token in one atomic transaction.
    /// </summary>
    /// <param name="outcomeId">Outcome identifier (for logging).</param>
    /// <param name="winningAssetType">Asset type of the winning outcome (e.g. DRAKE_WIN).</param>
    /// <param name="settlementAccountId">Account that pays USD and receives outcome tokens (counterparty).</param>
    /// <param name="usdPerToken">USD paid per token (default 1.00).</param>
    /// <returns>Number of accounts settled and total USD paid out.</returns>
    public async Task<(int AccountsSettled, decimal TotalUsdPaidOut)> ResolveMarketAsync(
        string outcomeId,
        string winningAssetType,
        Guid settlementAccountId,
        decimal usdPerToken = 1.00m,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(winningAssetType))
            throw new ArgumentException("Winning asset type is required.", nameof(winningAssetType));

        var assetType = winningAssetType.Trim();
        var entries = await _ledgerEntryRepository.GetByAssetTypeAsync(assetType, cancellationToken);

        var balancesByAccount = new Dictionary<Guid, decimal>();
        foreach (var e in entries)
        {
            if (!balancesByAccount.ContainsKey(e.AccountId))
                balancesByAccount[e.AccountId] = 0;
            balancesByAccount[e.AccountId] += e.Direction == EntryType.Debit ? e.Amount : -e.Amount;
        }

        var holders = balancesByAccount
            .Where(kv => kv.Value > 0)
            .Select(kv => (HolderAccountId: kv.Key, OutcomeQuantity: kv.Value))
            .ToList();

        if (holders.Count == 0)
        {
            Console.WriteLine($"[Settlement] OutcomeId={outcomeId}, AssetType={assetType}: no holders to settle.");
            return (0, 0);
        }

        await _accountingService.BookSettlementPayoutBatchAsync(
            settlementAccountId,
            holders,
            assetType,
            usdPerToken,
            null,
            cancellationToken);

        var totalUsd = holders.Sum(h => h.OutcomeQuantity * usdPerToken);
        Console.WriteLine($"[Settlement] OutcomeId={outcomeId}, AssetType={assetType}: settled {holders.Count} account(s), total USD paid out={totalUsd:F2}");

        return (holders.Count, totalUsd);
    }
}
