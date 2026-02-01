using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Drake;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Integration tests for MarketService, DrakeOracleService, and ledger: end-to-end trade,
/// oracle-to-market (CreateMarketEvent → OrderBook + GetActiveEvents), and multiple matches.
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
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId);
        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId);
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
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

        var bid = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, 0.60m, quantity);
        var ask = new Order(Guid.NewGuid(), sellerId, outcomeId, OrderType.Ask, price, quantity);

        await marketService.PlaceOrderAsync(bid);
        var result = await marketService.PlaceOrderAsync(ask);

        Assert.Equal(1, result.MatchCount);
        Assert.Single(result.TradeTransactionIds);

        var buyerTxns = await transactionRepo.GetByAccountIdAsync(buyerAccount.Id);
        var tradeTxns = buyerTxns.Where(t => t.Type == TransactionType.Trade).ToList();
        Assert.NotEmpty(tradeTxns);
        Assert.Contains(tradeTxns, t => t.TotalAmount == amount);

        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);
        Assert.Equal(amount * 2, buyerBalance);
        Assert.Equal(-amount, sellerBalance);
        Assert.Equal(amount, buyerBalance + sellerBalance);
    }

    [Fact]
    public void OracleToMarket_CreateMarketEvent_OrderBookInitializedAndEventInGetActiveEvents()
    {
        var (accountRepo, _, _, marketService, orderBookStore) = EnterpriseTestSetup.CreateMarketStackWithStore();
        var oracle = new DrakeOracleService(orderBookStore);

        var evt = oracle.CreateMarketEvent("Will X win?", "Flash", 5);

        Assert.NotNull(evt.OutcomeId);
        Assert.True(evt.IsActive);
        Assert.Equal("Flash", evt.Type);

        var book = marketService.GetOrderBook(evt.OutcomeId);
        Assert.NotNull(book);
        Assert.Empty(book.Bids);
        Assert.Empty(book.Asks);

        var active = oracle.GetActiveEvents();
        Assert.NotEmpty(active);
        Assert.Contains(active, e => e.Id == evt.Id && e.OutcomeId == evt.OutcomeId);
    }

    [Fact]
    public async Task MultipleMatches_OneLargeAskThreeSmallBids_AllMatchesProcessedAndLedgerReflectsThreeTrades()
    {
        var (accountRepo, transactionRepo, ledgerService, marketService) = CreateMarketStack();

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId);
        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId);
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
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

        var ask = new Order(Guid.NewGuid(), sellerId, outcomeId, OrderType.Ask, askPrice, 100m);
        await marketService.PlaceOrderAsync(ask);

        var bid1 = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, bidPrice, 30m);
        var bid2 = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, bidPrice, 30m);
        var bid3 = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, bidPrice, 40m);

        var r1 = await marketService.PlaceOrderAsync(bid1);
        var r2 = await marketService.PlaceOrderAsync(bid2);
        var r3 = await marketService.PlaceOrderAsync(bid3);

        Assert.Equal(1, r1.MatchCount);
        Assert.Equal(1, r2.MatchCount);
        Assert.Equal(1, r3.MatchCount);

        var buyerTxns = await transactionRepo.GetByAccountIdAsync(buyerAccount.Id);
        var tradeTxns = buyerTxns.Where(t => t.Type == TransactionType.Trade).ToList();
        Assert.Equal(3, tradeTxns.Count);

        decimal totalBuyerDebit = tradeTxns.Sum(t => t.TotalAmount);
        Assert.Equal(30m * askPrice + 30m * askPrice + 40m * askPrice, totalBuyerDebit);
        Assert.Equal(50m, totalBuyerDebit);
        Assert.Equal(100m, await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null));

        var buyerBalance = await ledgerService.GetAccountBalanceAsync(buyerAccount.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);
        Assert.Equal(100m, buyerBalance);
        Assert.Equal(-50m, sellerBalance);
        Assert.Equal(50m, buyerBalance + sellerBalance);
    }

    [Fact]
    public async Task PlaceOrder_MatchWhereSellerHasNoAccount_ThrowsInvalidOperationException()
    {
        var (accountRepo, _, ledgerService, marketService) = CreateMarketStack();

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerAccount = new Account(Guid.NewGuid(), "Buyer", AccountType.Asset, buyerId);
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId);
        await accountRepo.CreateAsync(buyerAccount);
        await accountRepo.CreateAsync(sinkAccount);
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccount.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 5m, EntryType.Credit, SettlementPhase.Clearing)
        });

        const string outcomeId = "outcome-no-seller";
        var ask = new Order(Guid.NewGuid(), sellerId, outcomeId, OrderType.Ask, 0.50m, 10m);
        var bid = new Order(Guid.NewGuid(), buyerId, outcomeId, OrderType.Bid, 0.60m, 10m);

        await marketService.PlaceOrderAsync(ask);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => marketService.PlaceOrderAsync(bid));
        Assert.Contains("Seller", ex.Message);
        Assert.Contains("no account", ex.Message);
    }
}
