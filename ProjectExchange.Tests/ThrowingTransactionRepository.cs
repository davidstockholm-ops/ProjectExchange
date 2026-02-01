using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Tests;

/// <summary>
/// Wraps an ITransactionRepository and throws <see cref="DbUpdateException"/> on the Nth call to AppendAsync.
/// Used to verify transaction rollback (zero-sum integrity).
/// </summary>
public sealed class ThrowingTransactionRepository : ITransactionRepository
{
    private readonly ITransactionRepository _inner;
    private readonly int _throwOnCall;
    private int _callCount;

    public ThrowingTransactionRepository(ITransactionRepository inner, int throwOnCall = 2)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _throwOnCall = throwOnCall;
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetByOperatorIdAsync(Guid operatorId, CancellationToken cancellationToken = default) =>
        _inner.GetByOperatorIdAsync(operatorId, cancellationToken);

    public Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _inner.GetByAccountIdAsync(accountId, cancellationToken);

    public async Task AppendAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        _callCount++;
        if (_callCount == _throwOnCall)
            throw new DbUpdateException("Simulated failure on second ledger entry (integrity test).");

        await _inner.AppendAsync(transaction, cancellationToken);
    }
}
