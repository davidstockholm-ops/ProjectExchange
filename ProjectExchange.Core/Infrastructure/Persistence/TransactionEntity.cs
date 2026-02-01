using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence model for <see cref="Accounting.Domain.Entities.Transaction"/>.
/// </summary>
public class TransactionEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SettlesClearingTransactionId { get; set; }
    public TransactionType? Type { get; set; }

    public ICollection<JournalEntryEntity> JournalEntries { get; set; } = new List<JournalEntryEntity>();
}
