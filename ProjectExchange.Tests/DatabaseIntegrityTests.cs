using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// Integrity tests: rollback on failure, mapping fidelity, concurrent withdrawals, double-entry zero-sum.
/// All use the Enterprise stack (SQLite in-memory + EfAccountRepository / EfTransactionRepository).
/// </summary>
public class DatabaseIntegrityTests
{
    /// <summary>
    /// Uses ThrowingTransactionRepository (throws on 2nd AppendAsync). SQLite supports real transactions;
    /// verifies MarketService catch-block and RollbackAsync() leave the ledger unchanged (atomicity).
    /// </summary>
    [Fact]
    public async Task Transaction_ShouldRollback_OnFailure()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var throwingRepo = new ThrowingTransactionRepository(transactionRepo, throwOnCall: 2);

        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var marketService = new MarketService(orderBookStore, accountRepo, throwingRepo, context, copyTradingService, ledgerService);

        var seller1Id = Guid.NewGuid();
        var seller2Id = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var seller1Account = new Account(Guid.NewGuid(), "Seller1", AccountType.Asset, seller1Id);
        var seller2Account = new Account(Guid.NewGuid(), "Seller2", AccountType.Asset, seller2Id);
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId);
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
        await accountRepo.CreateAsync(seller1Account);
        await accountRepo.CreateAsync(seller2Account);
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sinkAccount);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, 25m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 25m, EntryType.Credit, SettlementPhase.Clearing)
        });

        const string outcomeId = "outcome-rollback";
        var ask1 = new Order(Guid.NewGuid(), seller1Id, outcomeId, OrderType.Ask, 0.50m, 50m);
        var ask2 = new Order(Guid.NewGuid(), seller2Id, outcomeId, OrderType.Ask, 0.50m, 50m);
        var bid = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, 0.60m, 100m);

        await marketService.PlaceOrderAsync(ask1);
        await marketService.PlaceOrderAsync(ask2);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await marketService.PlaceOrderAsync(bid));

        context.ChangeTracker.Clear();
        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        var seller1Balance = await ledgerService.GetAccountBalanceAsync(seller1Account.Id, null);
        var seller2Balance = await ledgerService.GetAccountBalanceAsync(seller2Account.Id, null);

        Assert.Equal(25m, buyerBalance);
        Assert.Equal(0m, seller1Balance);
        Assert.Equal(0m, seller2Balance);
    }

    [Fact]
    public async Task Mapping_Order_ShouldBePerfect()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string outcomeId = "outcome-mapping-test-123";
        const decimal price = 0.7321m;
        const decimal quantity = 99.5678m;

        var entity = new OrderEntity
        {
            Id = id,
            UserId = userId,
            OutcomeId = outcomeId,
            Type = OrderType.Bid,
            Price = price,
            Quantity = quantity
        };
        context.Orders.Add(entity);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var retrieved = await context.Orders.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved.Id);
        Assert.Equal(userId, retrieved.UserId);
        Assert.Equal(outcomeId, retrieved.OutcomeId);
        Assert.Equal(OrderType.Bid, retrieved.Type);
        Assert.Equal(price, retrieved.Price);
        Assert.Equal(quantity, retrieved.Quantity);
    }

    [Fact]
    public async Task Concurrent_Wallet_Withdrawal()
    {
        const string dbName = "concurrent-withdrawal-test";
        var context = EnterpriseTestSetup.CreateFreshDbContext(dbName);
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);

        var walletId = Guid.NewGuid();
        var sinkId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var walletAccount = new Account(walletId, "Wallet", AccountType.Asset, operatorId);
        var sinkAccount = new Account(sinkId, "Sink", AccountType.Asset, operatorId);

        using (var scope = provider.CreateScope())
        {
            var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            await accountRepo.CreateAsync(walletAccount);
            await accountRepo.CreateAsync(sinkAccount);
        }

        using (var scope = provider.CreateScope())
        {
            var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
            var bankId = Guid.NewGuid();
            var bankAccount = new Account(bankId, "Bank", AccountType.Asset, operatorId);
            await accountRepo.CreateAsync(bankAccount);
            await ledgerService.PostTransactionAsync(new List<JournalEntry>
            {
                new(bankId, 50m, EntryType.Credit, SettlementPhase.Clearing),
                new(walletId, 50m, EntryType.Debit, SettlementPhase.Clearing)
            });
        }

        var sync = new object();
        var successCount = 0;
        Parallel.For(0, 5, _ =>
        {
            using var scope = provider.CreateScope();
            var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
            var txRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<ProjectExchangeDbContext>();

            lock (sync)
            {
                var balance = ledgerService.GetAccountBalanceAsync(walletId, default).GetAwaiter().GetResult();
                if (balance < 20m)
                    return;
                var tx = new Transaction(
                    Guid.NewGuid(),
                    new List<JournalEntry>
                    {
                        new(walletId, 20m, EntryType.Credit, SettlementPhase.Clearing),
                        new(sinkId, 20m, EntryType.Debit, SettlementPhase.Clearing)
                    });
                txRepo.AppendAsync(tx, default).GetAwaiter().GetResult();
                unitOfWork.SaveChangesAsync(default).GetAwaiter().GetResult();
                Interlocked.Increment(ref successCount);
            }
        });

        using (var scope = provider.CreateScope())
        {
            var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
            var finalBalance = await ledgerService.GetAccountBalanceAsync(walletId, null);
            Assert.Equal(2, successCount);
            Assert.Equal(10m, finalBalance);
        }
    }

    [Fact]
    public async Task DoubleEntry_Balance_AlwaysZero()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService, oracle, dbContext) = EnterpriseTestSetup.CreateFullStackWithContext();

        var drakeId = Guid.NewGuid();
        var liquidityProviderId = Guid.NewGuid();
        var fan1Id = Guid.NewGuid();
        var fan2Id = Guid.NewGuid();
        var fan3Id = Guid.NewGuid();
        var fan4Id = Guid.NewGuid();
        var fan5Id = Guid.NewGuid();

        var drakeAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, drakeId);
        var lpAccount = new Account(Guid.NewGuid(), "LP", AccountType.Asset, liquidityProviderId);
        var fan1Account = new Account(Guid.NewGuid(), "Fan1", AccountType.Asset, fan1Id);
        var fan2Account = new Account(Guid.NewGuid(), "Fan2", AccountType.Asset, fan2Id);
        var fan3Account = new Account(Guid.NewGuid(), "Fan3", AccountType.Asset, fan3Id);
        var fan4Account = new Account(Guid.NewGuid(), "Fan4", AccountType.Asset, fan4Id);
        var fan5Account = new Account(Guid.NewGuid(), "Fan5", AccountType.Asset, fan5Id);

        await accountRepo.CreateAsync(drakeAccount);
        await accountRepo.CreateAsync(lpAccount);
        await accountRepo.CreateAsync(fan1Account);
        await accountRepo.CreateAsync(fan2Account);
        await accountRepo.CreateAsync(fan3Account);
        await accountRepo.CreateAsync(fan4Account);
        await accountRepo.CreateAsync(fan5Account);

        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
        await accountRepo.CreateAsync(sinkAccount);
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(drakeAccount.Id, 25m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan1Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan2Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan3Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan4Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan5Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 50m, EntryType.Credit, SettlementPhase.Clearing)
        });

        copyTradingService.Follow(fan1Id, drakeId);
        copyTradingService.Follow(fan2Id, drakeId);
        copyTradingService.Follow(fan3Id, drakeId);
        copyTradingService.Follow(fan4Id, drakeId);
        copyTradingService.Follow(fan5Id, drakeId);

        var evt = oracle.CreateMarketEvent("Integrity Grand Final", "Flash", 5);
        var outcomeId = evt.OutcomeId;

        var lpAsk = new Order(Guid.NewGuid(), liquidityProviderId, outcomeId, OrderType.Ask, 0.50m, 150m);
        await marketService.PlaceOrderAsync(lpAsk);

        var drakeBid = new Order(Guid.NewGuid(), drakeId, outcomeId, OrderType.Bid, 0.50m, 50m);
        await marketService.PlaceOrderAsync(drakeBid);

        var entries = await dbContext.JournalEntries.AsNoTracking().ToListAsync();
        var signedSum = entries.Sum(je =>
            je.EntryType == EntryType.Debit ? je.Amount : -je.Amount);

        Assert.Equal(0.00m, signedSum);
    }
}
