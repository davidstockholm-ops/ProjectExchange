using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ITransactionRepository"/>.
/// All data access uses <see cref="DbContext.Set{TEntity}"/> so table names come from the EF model (avoids "relation does not exist" in Postgres).
/// <see cref="AppendAsync"/> adds to the context but does NOT call SaveChanges; caller (or <see cref="IUnitOfWork"/>) must save.
/// </summary>
public class EfTransactionRepository : ITransactionRepository
{
    private readonly ProjectExchangeDbContext _context;

    public EfTransactionRepository(ProjectExchangeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<TransactionEntity>()
            .AsNoTracking()
            .Include(t => t.JournalEntries)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<Transaction>> GetByOperatorIdAsync(string operatorId, CancellationToken cancellationToken = default)
    {
        var accountIds = await _context.Set<AccountEntity>()
            .Where(a => a.OperatorId == operatorId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        var transactionIds = await _context.Set<JournalEntryEntity>()
            .Where(j => accountIds.Contains(j.AccountId))
            .Select(j => j.TransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var entities = await _context.Set<TransactionEntity>()
            .AsNoTracking()
            .Include(t => t.JournalEntries)
            .Where(t => transactionIds.Contains(t.Id))
            .ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<TransactionEntity>()
            .AsNoTracking()
            .Include(t => t.JournalEntries)
            .Where(t => t.JournalEntries.Any(j => j.AccountId == accountId))
            .ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    /// <summary>
    /// Appends the transaction to the context. Does NOT call SaveChanges; caller must call <see cref="IUnitOfWork.SaveChangesAsync"/> or use a transaction scope.
    /// </summary>
    public async Task AppendAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var existsInDb = await _context.Set<TransactionEntity>().AnyAsync(t => t.Id == transaction.Id, cancellationToken);
        var existsLocal = _context.Set<TransactionEntity>().Local.Any(t => t.Id == transaction.Id);
        if (existsInDb || existsLocal)
            throw new InvalidOperationException($"Transaction {transaction.Id} already exists.");

        var entity = ToEntity(transaction);
        _context.Set<TransactionEntity>().Add(entity);
    }

    private static Transaction ToDomain(TransactionEntity e)
    {
        var entries = e.JournalEntries
            .OrderBy(j => j.Id)
            .Select(j => new JournalEntry(j.AccountId, j.Amount, j.EntryType, j.Phase))
            .ToList();
        return new Transaction(e.Id, entries, e.CreatedAt, e.SettlesClearingTransactionId, e.Type);
    }

    private static TransactionEntity ToEntity(Transaction t)
    {
        var entity = new TransactionEntity
        {
            Id = t.Id,
            CreatedAt = t.CreatedAt,
            SettlesClearingTransactionId = t.SettlesClearingTransactionId,
            Type = t.Type
        };
        foreach (var entry in t.JournalEntries)
        {
            entity.JournalEntries.Add(new JournalEntryEntity
            {
                Id = Guid.NewGuid(),
                TransactionId = entity.Id,
                AccountId = entry.AccountId,
                Amount = entry.Amount,
                EntryType = entry.EntryType,
                Phase = entry.Phase
            });
        }
        return entity;
    }
}
