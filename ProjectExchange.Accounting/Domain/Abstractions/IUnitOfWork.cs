namespace ProjectExchange.Accounting.Domain.Abstractions;

/// <summary>
/// Persistence unit of work: commits pending changes. Used so ledger operations can batch in a single transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
