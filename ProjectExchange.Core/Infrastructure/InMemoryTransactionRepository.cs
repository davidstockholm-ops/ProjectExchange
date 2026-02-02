using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Core.Infrastructure;

public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();
    private readonly IAccountRepository _accountRepository;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public InMemoryTransactionRepository(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = _transactions.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(tx);
    }

    public async Task<IReadOnlyList<Transaction>> GetByOperatorIdAsync(string operatorId, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetByOperatorIdAsync(operatorId, cancellationToken);
        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var list = _transactions
            .Where(t => t.JournalEntries.Any(e => accountIds.Contains(e.AccountId)))
            .ToList()
            .AsReadOnly();
        return list;
    }

    public Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var list = _transactions
            .Where(t => t.JournalEntries.Any(e => e.AccountId == accountId))
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Transaction>>(list);
    }

    public async Task AppendAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_transactions.Any(t => t.Id == transaction.Id))
                throw new InvalidOperationException($"Transaction {transaction.Id} already exists.");
            _transactions.Add(transaction);
        }
        finally
        {
            _sync.Release();
        }
    }
}
