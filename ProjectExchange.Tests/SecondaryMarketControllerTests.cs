using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Controllers;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Tests for SecondaryMarketController: bulk order (mm-provider) and delete orders by operator.
/// </summary>
public class SecondaryMarketControllerTests
{
    private const string MarketId = "test-market";
    private const string MmOperatorId = "mm-provider";

    [Fact]
    public async Task PostOrderBulk_NonMmOperatorId_Returns400()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        var request = new BulkOrderRequest(new[]
        {
            new BulkOrderItem(MarketId, 0.45m, 10m, "Buy", "apple-pay", "user-1")
        });

        var result = await controller.PostOrderBulk(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(MmOperatorId, badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task PostOrderBulk_MmProvider_Returns200AndOrdersInBook()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        var request = new BulkOrderRequest(new[]
        {
            new BulkOrderItem(MarketId, 0.45m, 10m, "Buy", MmOperatorId, "agent-lpa-test"),
            new BulkOrderItem(MarketId, 0.55m, 10m, "Sell", MmOperatorId, "agent-lpa-test")
        });

        var result = await controller.PostOrderBulk(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BulkOrderResponse>(ok.Value);
        Assert.Equal(2, response.Results.Count);

        var book = store.GetOrderBook(MarketId);
        Assert.NotNull(book);
        Assert.Single(book.Bids);
        Assert.Single(book.Asks);
        Assert.Equal(0.45m, book.Bids[0].Price);
        Assert.Equal(0.55m, book.Asks[0].Price);
        Assert.Equal(MmOperatorId, book.Bids[0].OperatorId);
    }

    [Fact]
    public void DeleteOrdersByOperator_MarketDoesNotExist_Returns404()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        var result = controller.DeleteOrdersByOperator("nonexistent-market", MmOperatorId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DeleteOrdersByOperator_MarketExistsWithOrders_Returns200AndRemovesOrders()
    {
        var store = new OrderBookStore();
        var book = store.GetOrCreateOrderBook(MarketId);
        book.AddOrder(new Order(Guid.NewGuid(), "u1", MarketId, OrderType.Bid, 0.45m, 10m, MmOperatorId));
        book.AddOrder(new Order(Guid.NewGuid(), "u1", MarketId, OrderType.Ask, 0.55m, 10m, MmOperatorId));
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        var result = controller.DeleteOrdersByOperator(MarketId, MmOperatorId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CancelOrdersResponse>(ok.Value);
        Assert.Equal(MarketId, response.MarketId);
        Assert.Equal(MmOperatorId, response.OperatorId);
        Assert.Equal(2, response.CancelledCount);

        var bookAfter = store.GetOrderBook(MarketId);
        Assert.NotNull(bookAfter);
        Assert.Empty(bookAfter.Bids);
        Assert.Empty(bookAfter.Asks);
    }

    [Fact]
    public void DeleteOrdersByOperator_MarketExistsNoMatchingOperator_Returns200ZeroCancelled()
    {
        var store = new OrderBookStore();
        var book = store.GetOrCreateOrderBook(MarketId);
        book.AddOrder(new Order(Guid.NewGuid(), "u1", MarketId, OrderType.Bid, 0.45m, 10m, "apple-pay"));
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        var result = controller.DeleteOrdersByOperator(MarketId, MmOperatorId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CancelOrdersResponse>(ok.Value);
        Assert.Equal(0, response.CancelledCount);
        Assert.Single(store.GetOrderBook(MarketId)!.Bids);
    }

    [Fact]
    public async Task PostOrder_BidThenAskAtSamePrice_MatchRecordedInTradeHistory()
    {
        var store = new OrderBookStore();
        var engine = new MockMatchingEngine(store);
        var tradeHistory = new TradeHistoryStore();
        var controller = new SecondaryMarketController(engine, store, tradeHistory);

        await controller.PostOrder(MarketId, 0.85m, 10m, "Buy", "op1", "user1");
        await controller.PostOrder(MarketId, 0.85m, 10m, "Sell", "op2", "user2");

        var tradesResult = controller.GetTrades(MarketId);
        var ok = Assert.IsType<OkObjectResult>(tradesResult);
        var response = Assert.IsType<SecondaryTradesResponse>(ok.Value);
        Assert.NotEmpty(response.Trades);
        var trade = response.Trades[0];
        Assert.Equal(0.85m, trade.Price);
        Assert.Equal(10m, trade.Quantity);
    }
}
