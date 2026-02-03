using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Entities;

/// <summary>
/// A single ledger line in the transaction ledger: AccountId, AssetType (e.g. USD, DRAKE_WIN), Amount, Direction (Debit/Credit), Timestamp.
/// Used for double-entry booking of matched trades (cash + outcome asset).
/// </summary>
public class LedgerEntry
{
    public Guid AccountId { get; }
    public string AssetType { get; }
    public decimal Amount { get; }
    public EntryType Direction { get; }
    public DateTimeOffset Timestamp { get; }

    public LedgerEntry(Guid accountId, string assetType, decimal amount, EntryType direction, DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(assetType))
            throw new ArgumentException("AssetType is required.", nameof(assetType));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        AccountId = accountId;
        AssetType = assetType.Trim();
        Amount = amount;
        Direction = direction;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }
}
