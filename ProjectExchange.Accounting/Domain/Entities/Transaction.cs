using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Exceptions;

namespace ProjectExchange.Accounting.Domain.Entities;

/// <summary>
/// A balanced double-entry transaction. Sum of debits must equal sum of credits.
/// Supports Clearing & Settlement Split: entries can be Clearing (internal debt) or Settlement (external).
/// </summary>
public class Transaction
{
    public Guid Id { get; }
    public IReadOnlyList<JournalEntry> JournalEntries { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When this transaction is a settlement, references the clearing transaction it settles.
    /// </summary>
    public Guid? SettlesClearingTransactionId { get; }

    /// <summary>
    /// Optional type (e.g. Trade for order-book matches).
    /// </summary>
    public TransactionType? Type { get; }

    public Transaction(
        Guid id,
        IReadOnlyList<JournalEntry> journalEntries,
        DateTimeOffset? createdAt = null,
        Guid? settlesClearingTransactionId = null,
        TransactionType? type = null)
    {
        if (journalEntries is null || journalEntries.Count == 0)
            throw new ArgumentException("Transaction must have at least two journal entries.", nameof(journalEntries));

        var totalDebits = journalEntries
            .Where(e => e.EntryType == EntryType.Debit)
            .Sum(e => e.Amount);
        var totalCredits = journalEntries
            .Where(e => e.EntryType == EntryType.Credit)
            .Sum(e => e.Amount);

        if (totalDebits != totalCredits)
            throw new TransactionNotBalancedException(totalDebits, totalCredits);

        Id = id;
        JournalEntries = journalEntries;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        SettlesClearingTransactionId = settlesClearingTransactionId;
        Type = type;
    }

    public decimal TotalAmount => JournalEntries
        .Where(e => e.EntryType == EntryType.Debit)
        .Sum(e => e.Amount);
}
