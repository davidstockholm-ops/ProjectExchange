using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Entities;

/// <summary>
/// Ledger account in the double-entry system. Belongs to an operator (platform tenant).
/// </summary>
public class Account
{
    public Guid Id { get; }
    public string Name { get; }
    public AccountType Type { get; }
    public string OperatorId { get; }

    public Account(Guid id, string name, AccountType type, string operatorId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(operatorId))
            throw new ArgumentException("OperatorId is required.", nameof(operatorId));

        Id = id;
        Name = name.Trim();
        Type = type;
        OperatorId = operatorId.Trim();
    }
}
