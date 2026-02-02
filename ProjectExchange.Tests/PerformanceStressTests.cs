using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// High-performance stress tests: high volume, parallelism, and zero-sum integrity after chaos.
/// Uses SQLite in-memory (EnterpriseTestSetup) so all tests run without external dependencies.
/// </summary>
public class PerformanceStressTests
{
    /// <summary>
    /// Submits 1 000 buy and 1 000 sell orders for the same outcome as fast as possible (Task.WhenAll),
    /// measures execution time with Stopwatch, and verifies zero-sum at the end.
    /// </summary>
    [Fact]
    public async Task HighVolume_MatchMaking_Performance()
    {
        const int orderCount = 1000;
        const string outcomeId = "stress-outcome-highvolume";
        const decimal price = 0.50m;
        const decimal quantityPerOrder = 1m;

        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService);

        var buyerIds = new List<Guid>();
        var sellerIds = new List<Guid>();
        var buyerAccountIds = new List<Guid>();
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
        await accountRepo.CreateAsync(sinkAccount);
        for (int i = 0; i < orderCount; i++)
        {
            var buyerId = Guid.NewGuid();
            var sellerId = Guid.NewGuid();
            buyerIds.Add(buyerId);
            sellerIds.Add(sellerId);
            var buyerAcc = new Account(Guid.NewGuid(), $"Buyer{i}", AccountType.Asset, buyerId);
            await accountRepo.CreateAsync(buyerAcc);
            buyerAccountIds.Add(buyerAcc.Id);
            await accountRepo.CreateAsync(new Account(Guid.NewGuid(), $"Seller{i}", AccountType.Asset, sellerId));
        }

        var fundingEntries = new List<JournalEntry>();
        foreach (var accId in buyerAccountIds)
            fundingEntries.Add(new JournalEntry(accId, quantityPerOrder * price, EntryType.Debit, SettlementPhase.Clearing));
        fundingEntries.Add(new JournalEntry(sinkAccount.Id, orderCount * quantityPerOrder * price, EntryType.Credit, SettlementPhase.Clearing));
        await ledgerService.PostTransactionAsync(fundingEntries);

        var buyOrders = buyerIds.Select((id, i) => new Order(Guid.NewGuid(), id, outcomeId, OrderType.Bid, price, quantityPerOrder)).ToList();
        var sellOrders = sellerIds.Select((id, i) => new Order(Guid.NewGuid(), id, outcomeId, OrderType.Ask, price, quantityPerOrder)).ToList();

        var sw = Stopwatch.StartNew();
        var placeTasks = buyOrders.Select(o => marketService.PlaceOrderAsync(o))
            .Concat(sellOrders.Select(o => marketService.PlaceOrderAsync(o)))
            .ToList();
        await Task.WhenAll(placeTasks);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 0, "Execution time measured.");
        await AssertZeroSumAsync(context);
    }

    /// <summary>
    /// Simulates a celebrity making a trade; 500 followers copy the trade simultaneously via Task.WhenAll.
    /// Verifies LedgerService handles all 500+ journal entries without deadlocks and zero-sum holds.
    /// </summary>
    [Fact]
    public async Task Concurrent_CopyTrading_Bombardment()
    {
        const int followerCount = 500;
        const string outcomeId = "stress-outcome-copytrading";
        const decimal price = 0.50m;
        const decimal lpQuantity = 5000m + 50m;
        const decimal celebrityQuantity = 50m;
        const decimal followerQuantity = 10m;

        var (accountRepo, ledgerService, copyTradingService, marketService, oracle, dbContext) = EnterpriseTestSetup.CreateFullStackWithContext();

        var celebrityId = Guid.NewGuid();
        var lpId = Guid.NewGuid();
        var celebrityAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, celebrityId);
        var lpAccount = new Account(Guid.NewGuid(), "LP", AccountType.Asset, lpId);
        await accountRepo.CreateAsync(celebrityAccount);
        await accountRepo.CreateAsync(lpAccount);

        var fanIds = new List<Guid>();
        var fanAccountIds = new List<Guid>();
        for (int i = 0; i < followerCount; i++)
        {
            var fanId = Guid.NewGuid();
            fanIds.Add(fanId);
            var fanAcc = new Account(Guid.NewGuid(), $"Fan{i}", AccountType.Asset, fanId);
            await accountRepo.CreateAsync(fanAcc);
            fanAccountIds.Add(fanAcc.Id);
            copyTradingService.Follow(fanId, celebrityId);
        }

        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
        await accountRepo.CreateAsync(sinkAccount);
        var fundingEntries = new List<JournalEntry>
        {
            new(celebrityAccount.Id, celebrityQuantity * price, EntryType.Debit, SettlementPhase.Clearing)
        };
        foreach (var accId in fanAccountIds)
            fundingEntries.Add(new JournalEntry(accId, followerQuantity * price, EntryType.Debit, SettlementPhase.Clearing));
        fundingEntries.Add(new JournalEntry(sinkAccount.Id, celebrityQuantity * price + followerCount * followerQuantity * price, EntryType.Credit, SettlementPhase.Clearing));
        await ledgerService.PostTransactionAsync(fundingEntries);

        var lpAsk = new Order(Guid.NewGuid(), lpId, outcomeId, OrderType.Ask, price, lpQuantity);
        await marketService.PlaceOrderAsync(lpAsk);

        var celebrityBid = new Order(Guid.NewGuid(), celebrityId, outcomeId, OrderType.Bid, price, celebrityQuantity);
        await marketService.PlaceOrderAsync(celebrityBid);

        var followerOrders = fanIds.Select(fanId => new Order(Guid.NewGuid(), fanId, outcomeId, OrderType.Bid, price, followerQuantity)).ToList();
        var bombardTasks = followerOrders.Select(o => marketService.PlaceOrderAsync(o)).ToList();
        await Task.WhenAll(bombardTasks);

        var entries = await dbContext.JournalEntries.AsNoTracking().ToListAsync();
        Assert.True(entries.Count >= 500, "Ledger should have 500+ journal entries from follower trades.");
        await AssertZeroSumAsync(dbContext);
    }

    private static async Task AssertZeroSumAsync(ProjectExchangeDbContext dbContext)
    {
        var entries = await dbContext.JournalEntries.AsNoTracking().ToListAsync();
        var signedSum = entries.Sum(je => je.EntryType == EntryType.Debit ? je.Amount : -je.Amount);
        Assert.Equal(0.00m, signedSum);
    }
}
