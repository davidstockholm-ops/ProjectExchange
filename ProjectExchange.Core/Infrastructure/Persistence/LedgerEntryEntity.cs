using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence model for double-entry outcome ledger. Mapped to table ledger_entries (snake_case).
/// </summary>
public class LedgerEntryEntity
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public EntryType Direction { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
