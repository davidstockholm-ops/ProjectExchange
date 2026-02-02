using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAccountRepository"/> using <see cref="ProjectExchangeDbContext"/>.
/// </summary>
public class EfAccountRepository : IAccountRepository
{
    private readonly ProjectExchangeDbContext _context;

    public EfAccountRepository(ProjectExchangeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Account>> GetByOperatorIdAsync(string operatorId, CancellationToken cancellationToken = default)
    {
        var list = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.OperatorId == operatorId)
            .ToListAsync(cancellationToken);
        return list.Select(ToDomain).ToList();
    }

    public async Task CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        if (await _context.Accounts.AnyAsync(a => a.Id == account.Id, cancellationToken))
            throw new InvalidOperationException($"Account {account.Id} already exists.");

        _context.Accounts.Add(ToEntity(account));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Account ToDomain(AccountEntity e) =>
        new(e.Id, e.Name, e.Type, e.OperatorId);

    private static AccountEntity ToEntity(Account a) =>
        new() { Id = a.Id, Name = a.Name, Type = a.Type, OperatorId = a.OperatorId };
}
