using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence model for <see cref="Accounting.Domain.Entities.Account"/>.
/// </summary>
public class AccountEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string OperatorId { get; set; } = string.Empty;
}
