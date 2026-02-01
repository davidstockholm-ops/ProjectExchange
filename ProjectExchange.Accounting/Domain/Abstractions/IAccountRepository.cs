using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Accounting.Domain.Abstractions;

/// <summary>
/// Persistence abstraction for accounts (Clean Architecture: domain defines interface).
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetByOperatorIdAsync(Guid operatorId, CancellationToken cancellationToken = default);
    Task CreateAsync(Account account, CancellationToken cancellationToken = default);
}
