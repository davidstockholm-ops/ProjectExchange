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

        var drakeId = Guid.NewGuid();
        var follower1Id = Guid.NewGuid();
        var follower2Id = Guid.NewGuid();

        var drakeAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, drakeId);
        var f1Account = new Account(Guid.NewGuid(), "Follower1", AccountType.Asset, follower1Id);
        var f2Account = new Account(Guid.NewGuid(), "Follower2", AccountType.Asset, follower2Id);
        await accountRepo.CreateAsync(drakeAccount);
        await accountRepo.CreateAsync(f1Account);
        await accountRepo.CreateAsync(f2Account);

        copyTradingService.Follow(follower1Id, drakeId);
        copyTradingService.Follow(follower2Id, drakeId);

        const string outcomeId = "outcome-social";
        var drakeOrder = new Order(Guid.NewGuid(), drakeId, outcomeId, OrderType.Bid, 0.60m, 50m);
        await marketService.PlaceOrderAsync(drakeOrder);

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
        var drakeId = Guid.NewGuid();
        var follower1Id = Guid.NewGuid();
        var follower2Id = Guid.NewGuid();

        var sellerAccount = new Account(Guid.NewGuid(), "Seller", AccountType.Asset, sellerId);
        var drakeAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, drakeId);
        var f1Account = new Account(Guid.NewGuid(), "Follower1", AccountType.Asset, follower1Id);
        var f2Account = new Account(Guid.NewGuid(), "Follower2", AccountType.Asset, follower2Id);
        await accountRepo.CreateAsync(sellerAccount);
        await accountRepo.CreateAsync(drakeAccount);
        await accountRepo.CreateAsync(f1Account);
        await accountRepo.CreateAsync(f2Account);

        copyTradingService.Follow(follower1Id, drakeId);
        copyTradingService.Follow(follower2Id, drakeId);

        const string outcomeId = "outcome-mirror-match";
        const decimal askPrice = 0.50m;
        const decimal bidPrice = 0.60m;

        var sellerAsk = new Order(Guid.NewGuid(), sellerId, outcomeId, OrderType.Ask, askPrice, 100m);
        await marketService.PlaceOrderAsync(sellerAsk);

        var drakeBid = new Order(Guid.NewGuid(), drakeId, outcomeId, OrderType.Bid, bidPrice, 50m);
        var result = await marketService.PlaceOrderAsync(drakeBid);

        Assert.True(result.MatchCount >= 1);
        Assert.True(result.TradeTransactionIds.Count >= 3);

        var drakeBalance = await ledgerService.GetAccountBalanceAsync(drakeAccount.Id, null);
        var f1Balance = await ledgerService.GetAccountBalanceAsync(f1Account.Id, null);
        var f2Balance = await ledgerService.GetAccountBalanceAsync(f2Account.Id, null);
        var sellerBalance = await ledgerService.GetAccountBalanceAsync(sellerAccount.Id, null);

        Assert.Equal(25m, drakeBalance);
        Assert.Equal(5m, f1Balance);
        Assert.Equal(5m, f2Balance);
        Assert.Equal(-35m, sellerBalance);
        Assert.Equal(0m, drakeBalance + f1Balance + f2Balance + sellerBalance);
    }

    [Fact]
    public async Task NoMirror_WhenUserHasNoFollowers_OnlyOneOrderInBook()
    {
        var (accountRepo, _, _, marketService) = CreateSocialStack();

        var userId = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), "Solo", AccountType.Asset, userId);
        await accountRepo.CreateAsync(account);

        const string outcomeId = "outcome-solo";
        var order = new Order(Guid.NewGuid(), userId, outcomeId, OrderType.Bid, 0.60m, 20m);
        await marketService.PlaceOrderAsync(order);

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Single(book.Bids);
        Assert.Empty(book.Asks);
    }
}
