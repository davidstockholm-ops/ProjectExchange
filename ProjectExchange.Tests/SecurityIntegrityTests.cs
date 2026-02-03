using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Exceptions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// Security / negative tests: insufficient funds, invalid outcome, negative price.
/// Ensures the system rejects invalid or fraudulent transactions and maintains integrity.
/// Uses EnterpriseTestSetup with SQLite in-memory.
/// </summary>
public class SecurityIntegrityTests
{
    /// <summary>
    /// Account has 100 SEK; buy order would cost 150 SEK when matched.
    /// MarketService must reject and create NO transaction or ledger entry.
    /// </summary>
    [Fact]
    public async Task InsufficientFunds_ShouldRejectOrder()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var accountingService = scope.ServiceProvider.GetRequiredService<AccountingService>();
        var outcomeAssetTypeResolver = scope.ServiceProvider.GetRequiredService<IOutcomeAssetTypeResolver>();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService, accountingService, outcomeAssetTypeResolver);

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId.ToString());
        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId.ToString());
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sellerAccount);
        await accountRepo.CreateAsync(sinkAccount);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, 100m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 100m, EntryType.Credit, SettlementPhase.Clearing)
        });

        const string outcomeId = "security-outcome-insufficient";
        var sellerAsk = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, 0.75m, 200m);
        await marketService.PlaceOrderAsync(sellerAsk);

        var buyerBid = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, 0.75m, 200m);
        var ex = await Assert.ThrowsAsync<InsufficientFundsException>(() => marketService.PlaceOrderAsync(buyerBid));
        Assert.Equal(150m, ex.Required);
        Assert.Equal(100m, ex.Available);

        var entryCount = await context.JournalEntries.AsNoTracking().CountAsync();
        Assert.Equal(2, entryCount);
        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        Assert.Equal(100m, buyerBalance);
    }

    /// <summary>
    /// Place an order for an OutcomeId that was never registered (no market opened).
    /// System must throw DomainException (InvalidOutcomeException).
    /// </summary>
    [Fact]
    public async Task InvalidOutcome_ShouldRejectOrder()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var accountingService = scope.ServiceProvider.GetRequiredService<AccountingService>();
        var outcomeAssetTypeResolver = scope.ServiceProvider.GetRequiredService<IOutcomeAssetTypeResolver>();
        var registry = new OutcomeRegistry();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService, accountingService, outcomeAssetTypeResolver, registry);

        var userId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "User", AccountType.Asset, userId.ToString());
        await accountRepo.CreateAsync(account);

        const string invalidOutcomeId = "outcome-nonexistent-in-system";
        var order = new Order(Guid.NewGuid(), userId.ToString(), invalidOutcomeId, OrderType.Bid, 0.50m, 10m);

        var ex = await Assert.ThrowsAsync<InvalidOutcomeException>(() => marketService.PlaceOrderAsync(order));
        Assert.Equal(invalidOutcomeId, ex.OutcomeId);
        Assert.IsAssignableFrom<DomainException>(ex);
    }

    /// <summary>
    /// Place an order with price -10.00 SEK. System must reject (Order ctor throws) and account balance unchanged.
    /// </summary>
    [Fact]
    public async Task NegativePrice_ShouldRejectOrder()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();

        var userId = Guid.NewGuid();
        var sinkId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "User", AccountType.Asset, userId.ToString());
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(account);
        await accountRepo.CreateAsync(sinkAccount);
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(account.Id, 100m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 100m, EntryType.Credit, SettlementPhase.Clearing)
        });
        var balanceBefore = await ledgerService.GetAccountBalanceAsync(account.Id, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "any-outcome", OrderType.Bid, -10.00m, 10m));

        var balanceAfter = await ledgerService.GetAccountBalanceAsync(account.Id, null);
        Assert.Equal(balanceBefore, balanceAfter);
    }
}
