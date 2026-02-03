using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for Project Exchange clearing and settlement data.
/// Maps to domain: Account, Transaction, JournalEntry (Accounting ledger).
/// </summary>
public class ProjectExchangeDbContext : DbContext, IUnitOfWork
{
    public ProjectExchangeDbContext(DbContextOptions<ProjectExchangeDbContext> options)
        : base(options)
    {
    }

    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<JournalEntryEntity> JournalEntries => Set<JournalEntryEntity>();
    public DbSet<LedgerEntryEntity> LedgerEntries => Set<LedgerEntryEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasIndex(x => new { x.OperatorId, x.Name }).IsUnique(false);
        });

        modelBuilder.Entity<TransactionEntity>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type);
            e.Property(x => x.SettlesClearingTransactionId);
        });

        modelBuilder.Entity<JournalEntryEntity>(e =>
        {
            e.ToTable("journal_entries");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Transaction)
                .WithMany(t => t.JournalEntries)
                .HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.TransactionId);
        });

        modelBuilder.Entity<LedgerEntryEntity>(e =>
        {
            e.ToTable("ledger_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.AssetType).HasMaxLength(64);
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<OrderEntity>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.OutcomeId).HasMaxLength(128);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
        });
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(cancellationToken);
}
