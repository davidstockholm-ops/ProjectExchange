using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;
using ProjectExchange.Tests;

namespace ProjectExchange.Tests.Integration;

/// <summary>
/// Integration test for the full copy-trading flow: followers table → LoadFollowRelations →
/// MarketService.PlaceOrderAsync(Leader) → mirrored order for Follower in the order book.
/// </summary>
public class CopyTradingIntegrationTests
{
    [Fact]
    public async Task FollowRelationInDb_LeaderPlacesOrder_TwoOrdersInBook_LeaderAndMirroredFollower()
    {
        const string leaderId = "Leader";
        const string followerId = "Follower";
        const string outcomeId = "outcome-copy-integration";

        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateServiceProvider(context);
        using var scope = provider.CreateScope();

        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var accountingService = scope.ServiceProvider.GetRequiredService<AccountingService>();
        var outcomeAssetTypeResolver = scope.ServiceProvider.GetRequiredService<IOutcomeAssetTypeResolver>();

        var leaderAccount = new Account(Guid.NewGuid(), "Leader Account", AccountType.Asset, leaderId);
        var followerAccount = new Account(Guid.NewGuid(), "Follower Account", AccountType.Asset, followerId);
        await accountRepo.CreateAsync(leaderAccount);
        await accountRepo.CreateAsync(followerAccount);

        context.Followers.Add(new FollowerEntity
        {
            FollowerId = followerId,
            LeaderId = leaderId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var copyTradingService = new CopyTradingService();
        var relations = await context.Followers.Select(f => (f.FollowerId, f.LeaderId)).ToListAsync();
        copyTradingService.LoadFollowRelations(relations);

        var orderBookStore = new OrderBookStore();
        var marketService = new MarketService(
            orderBookStore,
            accountRepo,
            transactionRepo,
            context,
            copyTradingService,
            ledgerService,
            accountingService,
            outcomeAssetTypeResolver);

        var leaderOrder = new Order(Guid.NewGuid(), leaderId, outcomeId, OrderType.Bid, 0.55m, 20m, leaderId);
        await marketService.PlaceOrderAsync(leaderOrder);

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Equal(2, book.Bids.Count);

        var userIds = book.Bids.Select(b => b.UserId).OrderBy(x => x).ToList();
        Assert.Contains(leaderId, userIds);
        Assert.Contains(followerId, userIds);
    }
}
