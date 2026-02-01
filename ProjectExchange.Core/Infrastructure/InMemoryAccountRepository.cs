using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;

namespace ProjectExchange.Core.Infrastructure;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();
    private readonly SemaphoreSlim _sync = new(1, 1);

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        return Task.FromResult(account);
    }

    public Task<IReadOnlyList<Account>> GetByOperatorIdAsync(Guid operatorId, CancellationToken cancellationToken = default)
    {
        var list = _accounts.Where(a => a.OperatorId == operatorId).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<Account>>(list);
    }

    public async Task CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_accounts.Any(a => a.Id == account.Id))
                throw new InvalidOperationException($"Account {account.Id} already exists.");
            _accounts.Add(account);
        }
        finally
        {
            _sync.Release();
        }
    }
}
