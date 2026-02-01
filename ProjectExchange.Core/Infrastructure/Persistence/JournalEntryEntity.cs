using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence model for a journal entry line. Belongs to a transaction.
/// </summary>
public class JournalEntryEntity
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public EntryType EntryType { get; set; }
    public SettlementPhase Phase { get; set; }

    public TransactionEntity? Transaction { get; set; }
}
