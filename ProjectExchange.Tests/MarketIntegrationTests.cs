using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Integration tests for MarketService, CelebrityOracleService, and ledger: end-to-end trade,
/// oracle-to-market (CreateMarketEvent(actorId) → OrderBook + GetActiveEvents), and multiple matches.
/// Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class MarketIntegrationTests
{
    private static (
        IAccountRepository AccountRepo,
        ITransactionRepository TransactionRepo,
        LedgerService LedgerService,
        MarketService MarketService) CreateMarketStack() =>
        EnterpriseTestSetup.CreateMarketStack();

    [Fact]
    public async Task EndToEndTrade_TwoAccounts_BidAndAskMatch_LedgerRecordsTradeAndBalancesMove()
    {
        var (accountRepo, transactionRepo, ledgerService, marketService) = CreateMarketStack();

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId.ToString());
        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId.ToString());
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sellerAccount);
        await accountRepo.CreateAsync(sinkAccount);

        const string outcomeId = "outcome-e2e";
        const decimal price = 0.50m;
        const decimal quantity = 20m;
        decimal amount = price * quantity;

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, amount, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, amount, EntryType.Credit, SettlementPhase.Clearing)
        });

        var bid = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, 0.60m, quantity);
        var ask = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, price, quantity);

        await marketService.PlaceOrderAsync(bid);
        var result = await marketService.PlaceOrderAsync(ask);

        Assert.Equal(1, result.MatchCount);
        Assert.Single(result.TradeTransactionIds);

        var buyerTxns = await transactionRepo.GetByAccountIdAsync(buyerAccount.Id);
        var tradeTxns = buyerTxns.Where(t => t.Type == TransactionType.Trade).ToList();
        Assert.NotEmpty(tradeTxns);
        Assert.Contains(tradeTxns, t => t.TotalAmount == amount);

        // Buyer pays (Credit): balance decreases. Seller receives (Debit): balance increases.
        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);
        Assert.Equal(0m, buyerBalance);           // started with amount, paid amount
        Assert.Equal(amount, sellerBalance);      // received amount
        Assert.Equal(amount, buyerBalance + sellerBalance);
    }

    [Fact]
    public void OracleToMarket_CreateMarketEvent_OrderBookInitializedAndEventInGetActiveEvents()
    {
        var (accountRepo, _, _, marketService, orderBookStore) = EnterpriseTestSetup.CreateMarketStackWithStore();
        var oracle = new CelebrityOracleService(orderBookStore, new ServiceCollection().BuildServiceProvider());

        var evt = oracle.CreateMarketEvent("Drake", "Will X win?", "Flash", 5);

        Assert.NotNull(evt.OutcomeId);
        Assert.True(evt.IsActive);
        Assert.Equal("Flash", evt.Type);
        Assert.Equal("Drake", evt.ActorId);
        Assert.Equal(CelebrityOracleService.OracleIdValue, evt.ResponsibleOracleId);

        var book = marketService.GetOrderBook(evt.OutcomeId);
        Assert.NotNull(book);
        Assert.Empty(book.Bids);
        Assert.Empty(book.Asks);

        var active = oracle.GetActiveEvents();
        Assert.NotEmpty(active);
        Assert.Contains(active, e => e.Id == evt.Id && e.OutcomeId == evt.OutcomeId);
    }

    [Fact]
    public void OracleToMarket_DrakeAndElonMarkets_SameOracle_BothActiveAndTrackedByActor()
    {
        var (_, _, _, marketService, orderBookStore) = EnterpriseTestSetup.CreateMarketStackWithStore();
        var oracle = new CelebrityOracleService(orderBookStore, new ServiceCollection().BuildServiceProvider());

        var drakeEvt = oracle.CreateMarketEvent("Drake", "Will Drake win?", "Flash", 5);
        var elonEvt = oracle.CreateMarketEvent("Elon", "Will Elon tweet?", "Base", 60);

        Assert.NotEqual(drakeEvt.OutcomeId, elonEvt.OutcomeId);
        Assert.Equal("Drake", drakeEvt.ActorId);
        Assert.Equal("Elon", elonEvt.ActorId);
        Assert.Equal(CelebrityOracleService.OracleIdValue, drakeEvt.ResponsibleOracleId);
        Assert.Equal(CelebrityOracleService.OracleIdValue, elonEvt.ResponsibleOracleId);

        var active = oracle.GetActiveEvents();
        Assert.Equal(2, active.Count);
        Assert.Contains(active, e => e.ActorId == "Drake" && e.OutcomeId == drakeEvt.OutcomeId);
        Assert.Contains(active, e => e.ActorId == "Elon" && e.OutcomeId == elonEvt.OutcomeId);

        var drakeBook = marketService.GetOrderBook(drakeEvt.OutcomeId);
        var elonBook = marketService.GetOrderBook(elonEvt.OutcomeId);
        Assert.NotNull(drakeBook);
        Assert.NotNull(elonBook);
        Assert.Empty(drakeBook.Bids);
        Assert.Empty(elonBook.Asks);
    }

    [Fact]
    public async Task MultipleMatches_OneLargeAskThreeSmallBids_AllMatchesProcessedAndLedgerReflectsThreeTrades()
    {
        var (accountRepo, transactionRepo, ledgerService, marketService) = CreateMarketStack();

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
            new(buyerAccount.Id, 50m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 50m, EntryType.Credit, SettlementPhase.Clearing)
        });

        const string outcomeId = "outcome-multi";
        const decimal askPrice = 0.50m;
        const decimal bidPrice = 0.60m;

        var ask = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, askPrice, 100m);
        await marketService.PlaceOrderAsync(ask);

        var bid1 = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, bidPrice, 30m);
        var bid2 = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, bidPrice, 30m);
        var bid3 = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, bidPrice, 40m);

        var r1 = await marketService.PlaceOrderAsync(bid1);
        var r2 = await marketService.PlaceOrderAsync(bid2);
        var r3 = await marketService.PlaceOrderAsync(bid3);

        Assert.Equal(1, r1.MatchCount);
        Assert.Equal(1, r2.MatchCount);
        Assert.Equal(1, r3.MatchCount);

        var buyerTxns = await transactionRepo.GetByAccountIdAsync(buyerAccount.Id);
        var tradeTxns = buyerTxns.Where(t => t.Type == TransactionType.Trade).ToList();
        Assert.Equal(3, tradeTxns.Count);

        decimal totalTradeAmount = tradeTxns.Sum(t => t.TotalAmount);
        Assert.Equal(30m * askPrice + 30m * askPrice + 40m * askPrice, totalTradeAmount);
        Assert.Equal(50m, totalTradeAmount);

        // Buyer pays (Credit): balance decreases. Seller receives (Debit): balance increases.
        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);
        Assert.Equal(0m, buyerBalance);   // started with 50, paid 50
        Assert.Equal(50m, sellerBalance); // received 50
        Assert.Equal(50m, buyerBalance + sellerBalance);
    }

    [Fact]
    public async Task PlaceOrder_MatchWhereSellerHasNoAccount_ThrowsInvalidOperationException()
    {
        var (accountRepo, _, ledgerService, marketService) = CreateMarketStack();

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId.ToString());
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sinkAccount);
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 5m, EntryType.Credit, SettlementPhase.Clearing)
        });

        const string outcomeId = "outcome-no-seller";
        var ask = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, 0.50m, 10m);
        var bid = new Order(Guid.NewGuid(), buyerId.ToString(), outcomeId, OrderType.Bid, 0.60m, 10m);

        await marketService.PlaceOrderAsync(ask);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => marketService.PlaceOrderAsync(bid));
        Assert.Contains("Seller", ex.Message);
        Assert.Contains("no account", ex.Message);
    }
}
