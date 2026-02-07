using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectExchange.Core.Auditing;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies that PositionService computes correct net position from TradeMatched domain events for binary contracts (YES/NO).
/// </summary>
public class PositionServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetNetPositionAsync_NoEvents_ReturnsEmpty()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var positions = await positionService.GetNetPositionAsync("user-1");

        Assert.Empty(positions);
    }

    [Fact]
    public async Task GetNetPositionAsync_BuyerIncreasesPosition_SellerDecreases()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        SeedTradeMatched(context, "drake-album-yes", 10m, "alice", "bob");
        SeedTradeMatched(context, "drake-album-yes", 5m, "bob", "alice");
        await context.SaveChangesAsync();

        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var alicePositions = await positionService.GetNetPositionAsync("alice");
        var bobPositions = await positionService.GetNetPositionAsync("bob");

        Assert.Single(alicePositions);
        Assert.Equal("drake-album-yes", alicePositions[0].OutcomeId);
        Assert.Equal(5m, alicePositions[0].NetQuantity); // +10 (buyer) - 5 (seller) = +5

        Assert.Single(bobPositions);
        Assert.Equal("drake-album-yes", bobPositions[0].OutcomeId);
        Assert.Equal(-5m, bobPositions[0].NetQuantity); // -10 (seller) + 5 (buyer) = -5
    }

    [Fact]
    public async Task GetNetPositionAsync_BinaryYesNo_SeparatePositions()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        // Alice buys 100 YES, sells 30 NO
        SeedTradeMatched(context, "market-x-yes", 100m, "alice", "mm");
        SeedTradeMatched(context, "market-x-no", 30m, "mm", "alice");
        await context.SaveChangesAsync();

        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var positions = await positionService.GetNetPositionAsync("alice");

        Assert.Equal(2, positions.Count);
        var yesPos = positions.FirstOrDefault(p => p.OutcomeId == "market-x-yes");
        var noPos = positions.FirstOrDefault(p => p.OutcomeId == "market-x-no");
        Assert.NotNull(yesPos);
        Assert.NotNull(noPos);
        Assert.Equal(100m, yesPos.NetQuantity);
        Assert.Equal(-30m, noPos.NetQuantity);
    }

    [Fact]
    public async Task GetNetPositionAsync_FilterByMarketId_ReturnsOnlyThatMarket()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        SeedTradeMatched(context, "drake-album-yes", 10m, "user-1", "mm");
        SeedTradeMatched(context, "other-market-yes", 5m, "user-1", "mm");
        await context.SaveChangesAsync();

        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var positions = await positionService.GetNetPositionAsync("user-1", marketId: "drake-album-yes");

        Assert.Single(positions);
        Assert.Equal("drake-album-yes", positions[0].OutcomeId);
        Assert.Equal(10m, positions[0].NetQuantity);
    }

    [Fact]
    public async Task GetNetPositionAsync_EmptyUserId_ReturnsEmpty()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var positions = await positionService.GetNetPositionAsync("");

        Assert.Empty(positions);
    }

    [Fact]
    public async Task GetNetPositionAsync_UserNotInAnyTrade_ReturnsEmpty()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        SeedTradeMatched(context, "drake-album-yes", 10m, "alice", "bob");
        await context.SaveChangesAsync();

        var auditService = new AuditService(context);
        var positionService = new PositionService(auditService);

        var positions = await positionService.GetNetPositionAsync("charlie");

        Assert.Empty(positions);
    }

    private static void SeedTradeMatched(
        ProjectExchangeDbContext context,
        string outcomeId,
        decimal quantity,
        string buyerUserId,
        string sellerUserId,
        decimal price = 0.85m)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Price = price,
            Quantity = quantity,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            OutcomeId = outcomeId
        }, JsonOptions);
        context.DomainEvents.Add(new DomainEventEntity
        {
            EventType = "TradeMatched",
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow,
            MarketId = outcomeId,
            UserId = buyerUserId
        });
        context.DomainEvents.Add(new DomainEventEntity
        {
            EventType = "TradeMatched",
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow,
            MarketId = outcomeId,
            UserId = sellerUserId
        });
    }
}
