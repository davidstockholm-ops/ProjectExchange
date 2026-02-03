using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Services;

/// <summary>
/// Books matched trades with double-entry logic: cash (USD) and outcome asset (e.g. DRAKE_WIN).
/// Buyer: minus cash (Credit USD), plus outcome asset (Debit DRAKE_WIN).
/// Seller: plus cash (Debit USD), minus outcome asset (Credit DRAKE_WIN).
/// </summary>
public class AccountingService
{
    public const string AssetTypeCash = "USD";
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountingService(ILedgerEntryRepository ledgerEntryRepository, IUnitOfWork unitOfWork)
    {
        _ledgerEntryRepository = ledgerEntryRepository ?? throw new ArgumentNullException(nameof(ledgerEntryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Books a matched trade. Writes four entries: buyer (Credit USD, Debit outcome), seller (Debit USD, Credit outcome).
    /// </summary>
    /// <param name="buyerAccountId">Buyer's account.</param>
    /// <param name="sellerAccountId">Seller's account.</param>
    /// <param name="cashAmount">Cash amount (e.g. 0.50 USD).</param>
    /// <param name="outcomeAssetType">Outcome asset type (e.g. DRAKE_WIN).</param>
    /// <param name="outcomeQuantity">Outcome quantity (e.g. 1).</param>
    /// <param name="timestamp">Optional timestamp; defaults to UtcNow.</param>
    public async Task BookTradeAsync(
        Guid buyerAccountId,
        Guid sellerAccountId,
        decimal cashAmount,
        string outcomeAssetType,
        decimal outcomeQuantity,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        if (cashAmount <= 0 || outcomeQuantity <= 0)
            throw new ArgumentOutOfRangeException("Cash and outcome quantity must be positive.");
        if (string.IsNullOrWhiteSpace(outcomeAssetType))
            throw new ArgumentException("Outcome asset type is required.", nameof(outcomeAssetType));

        var ts = timestamp ?? DateTimeOffset.UtcNow;

        // Köpare: Minus 0.50 USD (Credit USD), Plus 1 Drake-Asset (Debit DRAKE_WIN)
        // Säljare: Plus 0.50 USD (Debit USD), Minus 1 Drake-Asset (Credit DRAKE_WIN)
        var entries = new List<LedgerEntry>
        {
            new(buyerAccountId, AssetTypeCash, cashAmount, EntryType.Credit, ts),
            new(buyerAccountId, outcomeAssetType.Trim(), outcomeQuantity, EntryType.Debit, ts),
            new(sellerAccountId, AssetTypeCash, cashAmount, EntryType.Debit, ts),
            new(sellerAccountId, outcomeAssetType.Trim(), outcomeQuantity, EntryType.Credit, ts)
        };

        await _ledgerEntryRepository.AddRangeAsync(entries, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Books settlement payouts in one batch: for each holder, Credit their outcome (remove tokens), Debit their USD (add cash);
    /// settlement account is the counterparty. All entries in one transaction (one SaveChanges).
    /// </summary>
    /// <param name="settlementAccountId">Account that pays USD and receives outcome tokens (e.g. market/settlement account).</param>
    /// <param name="holders">Holder account ID and their outcome quantity to settle.</param>
    /// <param name="outcomeAssetType">Outcome asset type (e.g. DRAKE_WIN).</param>
    /// <param name="usdPerToken">USD paid per token (e.g. 1.00).</param>
    public async Task BookSettlementPayoutBatchAsync(
        Guid settlementAccountId,
        IReadOnlyList<(Guid HolderAccountId, decimal OutcomeQuantity)> holders,
        string outcomeAssetType,
        decimal usdPerToken = 1.00m,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        if (holders == null || holders.Count == 0)
            return;
        if (string.IsNullOrWhiteSpace(outcomeAssetType))
            throw new ArgumentException("Outcome asset type is required.", nameof(outcomeAssetType));
        if (usdPerToken <= 0)
            throw new ArgumentOutOfRangeException(nameof(usdPerToken), "USD per token must be positive.");

        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var entries = new List<LedgerEntry>();

        foreach (var (holderAccountId, outcomeQuantity) in holders)
        {
            if (outcomeQuantity <= 0)
                continue;
            var usdAmount = outcomeQuantity * usdPerToken;
            // Holder: Credit outcome (remove tokens), Debit USD (add cash)
            entries.Add(new LedgerEntry(holderAccountId, outcomeAssetType.Trim(), outcomeQuantity, EntryType.Credit, ts));
            entries.Add(new LedgerEntry(holderAccountId, AssetTypeCash, usdAmount, EntryType.Debit, ts));
            // Settlement: Debit outcome (receive tokens), Credit USD (pay out)
            entries.Add(new LedgerEntry(settlementAccountId, outcomeAssetType.Trim(), outcomeQuantity, EntryType.Debit, ts));
            entries.Add(new LedgerEntry(settlementAccountId, AssetTypeCash, usdAmount, EntryType.Credit, ts));
        }

        if (entries.Count > 0)
        {
            await _ledgerEntryRepository.AddRangeAsync(entries, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
