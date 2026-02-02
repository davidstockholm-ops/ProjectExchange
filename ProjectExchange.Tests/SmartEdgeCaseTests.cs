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
/// Smart edge-case tests based on common failure modes: boundaries, missing data, unfollow, oracle validation,
/// seller-with-no-account, and engine missing main account. Complements SecurityIntegrityTests and DatabaseIntegrityTests.
/// </summary>
public class SmartEdgeCaseTests
{
    // ----- Order boundaries and validation -----

    [Fact]
    public void Order_ZeroQuantity_Throws()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "outcome-x", OrderType.Bid, 0.50m, 0m));
    }

    [Fact]
    public void Order_NegativeQuantity_Throws()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "outcome-x", OrderType.Bid, 0.50m, -10m));
    }

    [Fact]
    public void Order_PriceAboveOne_Throws()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "outcome-x", OrderType.Bid, 1.01m, 10m));
    }

    [Fact]
    public void Order_EmptyOutcomeId_Throws()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "", OrderType.Bid, 0.50m, 10m));
        Assert.Throws<ArgumentException>(() =>
            new Order(Guid.NewGuid(), userId.ToString(), "   ", OrderType.Bid, 0.50m, 10m));
    }

    [Fact]
    public void Order_PriceAtBoundary_ZeroAndOne_Accepted()
    {
        var userId = Guid.NewGuid();
        var order0 = new Order(Guid.NewGuid(), userId.ToString(), "outcome-x", OrderType.Bid, 0.00m, 1m);
        var order1 = new Order(Guid.NewGuid(), userId.ToString(), "outcome-x", OrderType.Ask, 1.00m, 1m);
        Assert.Equal(0.00m, order0.Price);
        Assert.Equal(1.00m, order1.Price);
    }

    // ----- CelebrityOracleService validation -----

    [Fact]
    public void CelebrityOracle_CreateMarketEvent_EmptyTitle_Throws()
    {
        var orderBookStore = new OrderBookStore();
        var oracle = new CelebrityOracleService(orderBookStore, new ServiceCollection().BuildServiceProvider());
        Assert.Throws<ArgumentException>(() =>
            oracle.CreateMarketEvent("Drake", "", "Flash", 5));
        Assert.Throws<ArgumentException>(() =>
            oracle.CreateMarketEvent("Drake", "   ", "Flash", 5));
    }

    [Fact]
    public void CelebrityOracle_SimulateTrade_ZeroAmount_Throws()
    {
        var orderBookStore = new OrderBookStore();
        var oracle = new CelebrityOracleService(orderBookStore, new ServiceCollection().BuildServiceProvider());
        var operatorId = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            oracle.SimulateTrade(operatorId.ToString(), 0m, "outcome-x"));
    }

    [Fact]
    public void CelebrityOracle_SimulateTrade_EmptyOutcomeId_Throws()
    {
        var orderBookStore = new OrderBookStore();
        var oracle = new CelebrityOracleService(orderBookStore, new ServiceCollection().BuildServiceProvider());
        var operatorId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            oracle.SimulateTrade(operatorId.ToString(), 100m, ""));
    }

    // ----- Copy-trading: Unfollow then place order -----

    [Fact]
    public async Task Unfollow_ThenCopyTrade_ExcludesUnfollowedUser()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService) = EnterpriseTestSetup.CreateSocialStack();
        var oracle = new CelebrityOracleService(new OrderBookStore(), new ServiceCollection().BuildServiceProvider());
        var masterId = Guid.NewGuid();
        var followerA = Guid.NewGuid();
        var followerB = Guid.NewGuid();
        var lpId = Guid.NewGuid();
        var sinkId = Guid.NewGuid();

        var masterAccount = new Account(Guid.NewGuid(), "Master", AccountType.Asset, masterId.ToString());
        var fanAAccount = new Account(Guid.NewGuid(), "FanA", AccountType.Asset, followerA.ToString());
        var fanBAccount = new Account(Guid.NewGuid(), "FanB", AccountType.Asset, followerB.ToString());
        var lpAccount = new Account(Guid.NewGuid(), "LP", AccountType.Asset, lpId.ToString());
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(masterAccount);
        await accountRepo.CreateAsync(fanAAccount);
        await accountRepo.CreateAsync(fanBAccount);
        await accountRepo.CreateAsync(lpAccount);
        await accountRepo.CreateAsync(sinkAccount);

        copyTradingService.Follow(followerA.ToString(), masterId.ToString());
        copyTradingService.Follow(followerB.ToString(), masterId.ToString());
        copyTradingService.Unfollow(followerA.ToString(), masterId.ToString());

        var evt = oracle.CreateMarketEvent("Drake", "Unfollow Test", "Flash", 5);
        var outcomeId = evt.OutcomeId;
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(masterAccount.Id, 50m, EntryType.Debit, SettlementPhase.Clearing),
            new(fanAAccount.Id, 20m, EntryType.Debit, SettlementPhase.Clearing),
            new(fanBAccount.Id, 20m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 90m, EntryType.Credit, SettlementPhase.Clearing)
        });

        var lpAsk = new Order(Guid.NewGuid(), lpId.ToString(), outcomeId, OrderType.Ask, 0.50m, 200m);
        await marketService.PlaceOrderAsync(lpAsk);
        var masterBid = new Order(Guid.NewGuid(), masterId.ToString(), outcomeId, OrderType.Bid, 0.50m, 50m);
        var result = await marketService.PlaceOrderAsync(masterBid);

        Assert.True(result.MatchCount >= 1);
        Assert.Equal(2, result.TradeTransactionIds.Count);
        var fanABalance = await ledgerService.GetAccountBalanceAsync(fanAAccount.Id, null);
        Assert.Equal(20m, fanABalance);
    }

    // ----- Market: seller with no account -----

    [Fact]
    public async Task Match_SellerWithNoAccount_Throws()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService);

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId.ToString());
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, Guid.NewGuid().ToString());
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sinkAccount);

        const string outcomeId = "outcome-seller-missing";
        const decimal amount = 10m;
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, amount, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, amount, EntryType.Credit, SettlementPhase.Clearing)
        });

        var ask = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, 0.50m, 20m);
        await marketService.PlaceOrderAsync(ask);

        var bid = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, 0.60m, 20m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => marketService.PlaceOrderAsync(bid));
        Assert.Contains("Seller", ex.Message);
        Assert.Contains(sellerId.ToString(), ex.Message);
    }

    // ----- GetOrderBook nonexistent outcome -----

    [Fact]
    public void GetOrderBook_NonexistentOutcome_ReturnsNull()
    {
        var (_, _, _, marketService, _) = EnterpriseTestSetup.CreateMarketStackWithStore();
        var book = marketService.GetOrderBook("outcome-never-created");
        Assert.Null(book);
    }

    // ----- Ledger: account with no entries -----

    [Fact]
    public async Task Ledger_GetAccountBalance_NoEntries_ReturnsZero()
    {
        var (accountRepo, _, ledgerService) = EnterpriseTestSetup.CreateLedger();
        var operatorId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "Empty", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(account);
        var balance = await ledgerService.GetAccountBalanceAsync(account.Id, null);
        Assert.Equal(0m, balance);
    }

    // ----- AutoSettlement: outcome with no clearing transactions -----

    [Fact]
    public async Task AutoSettlement_OutcomeWithNoClearingTransactions_ReturnsMessage()
    {
        var (_, _, _, copyTradingEngine, autoSettlementAgent) = EnterpriseTestSetup.CreateCelebrityStack();
        var result = await autoSettlementAgent.SettleOutcomeAsync("outcome-no-clearing-txs");
        Assert.Empty(result.NewSettlementTransactionIds);
        Assert.Equal("outcome-no-clearing-txs", result.OutcomeId);
        Assert.Empty(result.AlreadySettledClearingIds);
        Assert.Contains("No clearing transactions", result.Message);
    }

    // ----- Idempotent outcome-reached -----

    [Fact]
    public async Task Idempotent_OutcomeReached_CalledTwice_SecondReturnsAlreadySettled()
    {
        var (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent) = EnterpriseTestSetup.CreateCelebrityStack();
        var operatorId = Guid.NewGuid();
        var mainAccountName = CelebrityConstants.GetMainOperatingAccountName("Drake");
        var account = new Account(Guid.NewGuid(), mainAccountName, AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(account);

        var evt = oracle.CreateMarketEvent("Drake", "Idempotent Outcome", "Flash", 5);
        var outcomeId = evt.OutcomeId;
        var signal = new CelebrityTradeSignal(Guid.NewGuid(), operatorId.ToString(), 50m, outcomeId, "Idempotent", "Drake");
        await copyTradingEngine.ExecuteCopyTradeAsync(signal);

        var first = await autoSettlementAgent.SettleOutcomeAsync(outcomeId);
        var second = await autoSettlementAgent.SettleOutcomeAsync(outcomeId);

        Assert.Single(first.NewSettlementTransactionIds);
        Assert.Empty(first.AlreadySettledClearingIds);
        Assert.Single(second.AlreadySettledClearingIds);
        Assert.Equal(first.NewSettlementTransactionIds[0], second.NewSettlementTransactionIds[0]);
    }

    // ----- CopyTradingEngine: missing main operating account -----

    [Fact]
    public async Task CopyTradingEngine_MissingMainOperatingAccount_Throws()
    {
        var (accountRepo, _, _, copyTradingEngine, _) = EnterpriseTestSetup.CreateCelebrityStack();
        var operatorId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "Wrong Name Only", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(account);

        var signal = new CelebrityTradeSignal(
            Guid.NewGuid(),
            operatorId.ToString(),
            100m,
            "outcome-missing-main",
            "Outcome",
            "Celeb");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            copyTradingEngine.ExecuteCopyTradeAsync(signal));
        Assert.Contains("Celeb Main Operating Account", ex.Message);
        Assert.Contains("not found", ex.Message);
    }
}
