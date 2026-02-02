using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// "The Social Test Suite": verifies copy-trading chain reaction, ledger safety, and mirror matching.
/// Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class SocialTradingTests
{
    private static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService) CreateSocialStack() =>
        EnterpriseTestSetup.CreateSocialStack();

    [Fact]
    public async Task SimpleFollowAndMirror_MasterAndTwoFollowers_OrderBookContainsThreeOrders()
    {
        var (accountRepo, _, copyTradingService, marketService) = CreateSocialStack();

        var celebrityId = Guid.NewGuid();
        var follower1Id = Guid.NewGuid();
        var follower2Id = Guid.NewGuid();

        var celebrityAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, celebrityId.ToString());
        var f1Account = new Account(Guid.NewGuid(), "Follower1", AccountType.Asset, follower1Id.ToString());
        var f2Account = new Account(Guid.NewGuid(), "Follower2", AccountType.Asset, follower2Id.ToString());
        await accountRepo.CreateAsync(celebrityAccount);
        await accountRepo.CreateAsync(f1Account);
        await accountRepo.CreateAsync(f2Account);

        copyTradingService.Follow(follower1Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(follower2Id.ToString(), celebrityId.ToString());

        const string outcomeId = "outcome-social";
        var celebrityOrder = new Order(Guid.NewGuid(), celebrityId.ToString(), outcomeId, OrderType.Bid, 0.60m, 50m);
        await marketService.PlaceOrderAsync(celebrityOrder);

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Equal(3, book.Bids.Count);
        Assert.Empty(book.Asks);
    }

    [Fact]
    public async Task MirrorAndMatch_LargeAskThenMasterBid_FollowersMatchAndLedgerBalancesCorrect()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService) = CreateSocialStack();

        var sellerId = Guid.NewGuid();
        var celebrityId = Guid.NewGuid();
        var follower1Id = Guid.NewGuid();
        var follower2Id = Guid.NewGuid();

        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId.ToString());
        var celebrityAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, celebrityId.ToString());
        var f1Account = new Account(Guid.NewGuid(), "Follower1", AccountType.Asset, follower1Id.ToString());
        var f2Account = new Account(Guid.NewGuid(), "Follower2", AccountType.Asset, follower2Id.ToString());
        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(sellerAccount);
        await accountRepo.CreateAsync(celebrityAccount);
        await accountRepo.CreateAsync(f1Account);
        await accountRepo.CreateAsync(f2Account);
        await accountRepo.CreateAsync(sinkAccount);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(celebrityAccount.Id, 25m, EntryType.Debit, SettlementPhase.Clearing),
            new(f1Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(f2Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 35m, EntryType.Credit, SettlementPhase.Clearing)
        });

        copyTradingService.Follow(follower1Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(follower2Id.ToString(), celebrityId.ToString());

        const string outcomeId = "outcome-mirror-match";
        const decimal askPrice = 0.50m;
        const decimal bidPrice = 0.60m;

        var sellerAsk = new Order(Guid.NewGuid(), sellerId.ToString(), outcomeId, OrderType.Ask, askPrice, 100m);
        await marketService.PlaceOrderAsync(sellerAsk);

        var celebrityBid = new Order(Guid.NewGuid(), celebrityId.ToString(), outcomeId, OrderType.Bid, bidPrice, 50m);
        var result = await marketService.PlaceOrderAsync(celebrityBid);

        Assert.True(result.MatchCount >= 1);
        Assert.True(result.TradeTransactionIds.Count >= 3);

        // Buyers pay (Credit): balances decrease. Seller receives (Debit): balance increases.
        var celebrityBalance = await ledgerService.GetAccountBalanceAsync(celebrityAccount.Id, null);
        var f1Balance = await ledgerService.GetAccountBalanceAsync(f1Account.Id, null);
        var f2Balance = await ledgerService.GetAccountBalanceAsync(f2Account.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);

        Assert.Equal(0m, celebrityBalance);  // 25 - 25 paid
        Assert.Equal(0m, f1Balance);        // 5 - 5 paid
        Assert.Equal(0m, f2Balance);        // 5 - 5 paid
        Assert.Equal(35m, sellerBalance);  // received 25+5+5
        Assert.Equal(35m, celebrityBalance + f1Balance + f2Balance + sellerBalance);
    }

    [Fact]
    public async Task NoMirror_WhenUserHasNoFollowers_OnlyOneOrderInBook()
    {
        var (accountRepo, _, _, marketService) = CreateSocialStack();

        var userId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "Solo", AccountType.Asset, userId.ToString());
        await accountRepo.CreateAsync(account);

        const string outcomeId = "outcome-solo";
        var order = new Order(Guid.NewGuid(), userId.ToString(), outcomeId, OrderType.Bid, 0.60m, 20m);
        await marketService.PlaceOrderAsync(order);

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Single(book.Bids);
        Assert.Empty(book.Asks);
    }
}
