using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// CopyTradingService edge cases: self-follow no-op, unknown master, mirror with no followers.
/// </summary>
public class CopyTradingServiceTests
{
    [Fact]
    public void Follow_SelfFollow_DoesNothing()
    {
        var service = new CopyTradingService();
        var id = Guid.NewGuid().ToString();
        service.Follow(id, id);
        var followers = service.GetFollowers(id);
        Assert.Empty(followers);
    }

    [Fact]
    public void GetFollowers_UnknownMaster_ReturnsEmpty()
    {
        var service = new CopyTradingService();
        var followers = service.GetFollowers(Guid.NewGuid().ToString());
        Assert.Empty(followers);
    }

    [Fact]
    public async Task MirrorOrderAsync_MasterWithNoFollowers_ReturnsEmpty()
    {
        var service = new CopyTradingService();
        var masterId = Guid.NewGuid().ToString();
        var order = new Order(Guid.NewGuid(), masterId, "outcome-x", OrderType.Bid, 0.60m, 50m);
        var mirrored = await service.MirrorOrderAsync(order);
        Assert.Empty(mirrored);
    }

    [Fact]
    public async Task MirrorOrderAsync_MasterWithFollowers_ReturnsOneOrderPerFollower()
    {
        var service = new CopyTradingService();
        var masterId = Guid.NewGuid().ToString();
        var f1 = Guid.NewGuid().ToString();
        var f2 = Guid.NewGuid().ToString();
        service.Follow(f1, masterId);
        service.Follow(f2, masterId);
        var order = new Order(Guid.NewGuid(), masterId, "outcome-x", OrderType.Bid, 0.60m, 50m);
        var mirrored = await service.MirrorOrderAsync(order);
        Assert.Equal(2, mirrored.Count);
        Assert.All(mirrored, o =>
        {
            Assert.Equal("outcome-x", o.OutcomeId);
            Assert.Equal(0.60m, o.Price);
            Assert.Equal(OrderType.Bid, o.Type);
        });
    }
}
