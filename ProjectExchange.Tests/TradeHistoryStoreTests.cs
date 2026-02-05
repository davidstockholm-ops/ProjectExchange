using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Unit tests for TradeHistoryStore: Record and GetByMarketId.
/// </summary>
public class TradeHistoryStoreTests
{
    [Fact]
    public void Record_AddsEntry_GetByMarketId_ReturnsNewestFirst()
    {
        var store = new TradeHistoryStore();
        store.Record("drake-album", 0.85m, 100m, "Buy");
        store.Record("drake-album", 0.84m, 50m, "Sell");

        var trades = store.GetByMarketId("drake-album");
        Assert.Equal(2, trades.Count);
        Assert.Equal(0.84m, trades[0].Price);
        Assert.Equal(50m, trades[0].Quantity);
        Assert.Equal("Sell", trades[0].Side);
        Assert.Equal(0.85m, trades[1].Price);
        Assert.Equal(100m, trades[1].Quantity);
        Assert.Equal("Buy", trades[1].Side);
    }

    [Fact]
    public void GetByMarketId_UnknownMarket_ReturnsEmpty()
    {
        var store = new TradeHistoryStore();
        store.Record("market-a", 0.50m, 10m, "Buy");

        var trades = store.GetByMarketId("market-b");
        Assert.Empty(trades);
    }

    [Fact]
    public void Record_EmptyOrNullMarketId_DoesNotThrow()
    {
        var store = new TradeHistoryStore();
        store.Record("", 0.50m, 10m, "Buy");
        store.Record("   ", 0.51m, 5m, "Sell");

        Assert.Empty(store.GetByMarketId(""));
    }
}
