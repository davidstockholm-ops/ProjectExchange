using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Entities;

/// <summary>
/// A single debit or credit line in a transaction. Amount is always positive; side is Debit or Credit.
/// Supports Clearing & Settlement Split via <see cref="Phase"/>.
/// </summary>
public class JournalEntry
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    public EntryType EntryType { get; }
    public SettlementPhase Phase { get; }

    public JournalEntry(Guid accountId, decimal amount, EntryType entryType, SettlementPhase phase = SettlementPhase.Clearing)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Journal entry amount must be positive.");

        AccountId = accountId;
        Amount = amount;
        EntryType = entryType;
        Phase = phase;
    }
}
