using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Accounting.Domain.Abstractions;

/// <summary>
/// Persistence abstraction for transactions (Clean Architecture: domain defines interface).
/// </summary>
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByOperatorIdAsync(Guid operatorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task AppendAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
