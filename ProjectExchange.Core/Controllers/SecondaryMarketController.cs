using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>
/// Secondary market API: submit orders, bulk orders (Market Maker), and retrieve order book by market.
/// Enterprise: OperatorId and UserId are mandatory for settlement and the internal accounting ledger.
/// Market Makers must use operatorId "mm-provider" for bulk orders so the Ledger can track them separately from retail.
/// </summary>
[ApiController]
[Route("api/secondary")]
[Produces("application/json")]
public class SecondaryMarketController : ControllerBase
{
    /// <summary>OperatorId required for Market Maker bulk orders; used so the Ledger can track MM flow separately from retail.</summary>
    public const string MarketMakerOperatorId = "mm-provider";

    private readonly IMatchingEngine _matchingEngine;
    private readonly IOrderBookStore _orderBookStore;

    public SecondaryMarketController(IMatchingEngine matchingEngine, IOrderBookStore orderBookStore)
    {
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
    }

    /// <summary>Submit an order to the secondary market. All parameters must be sent as query string (not JSON body) so Swagger shows them as individual fields.</summary>
    /// <param name="marketId">Outcome or market identifier (e.g. drake-album). Required.</param>
    /// <param name="price">Limit price between 0.00 and 1.00. Required.</param>
    /// <param name="quantity">Order size. Must be positive. Required.</param>
    /// <param name="side">Order side: Buy or Sell (or 0/1). Kept as string to avoid enum binding issues. Required.</param>
    /// <param name="operatorId">Settlement operator (e.g. apple-pay). Required for ledger.</param>
    /// <param name="userId">User identifier (e.g. user-david). Required for ledger.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Order id and list of matches, or 400 with validation message.</returns>
    [HttpPost("order")]
    [ProducesResponseType(typeof(SecondaryOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostOrder(
        [FromQuery] string? marketId,
        [FromQuery] decimal? price,
        [FromQuery] decimal? quantity,
        [FromQuery] string? side,
        [FromQuery] string? operatorId,
        [FromQuery] string? userId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SWAGGER INCOMING] marketId={marketId} price={price} quantity={quantity} side={side} operatorId={operatorId} userId={userId}");

        // Enterprise: OperatorId and UserId are mandatory for settlement and the accounting ledger.
        if (string.IsNullOrWhiteSpace(operatorId))
            return BadRequest("OperatorId is required. Send all parameters as query string: ?marketId=...&price=...&quantity=...&side=Buy&operatorId=...&userId=...");
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId is required. Send all parameters as query string.");
        if (string.IsNullOrWhiteSpace(marketId))
            return BadRequest("MarketId is required. Send all parameters as query string.");
        if (string.IsNullOrWhiteSpace(side))
            return BadRequest("Side is required (Buy or Sell). Send as query: side=Buy or side=Sell.");
        if (!price.HasValue || price.Value < 0.00m || price.Value > 1.00m)
            return BadRequest("Price must be between 0.00 and 1.00.");
        if (!quantity.HasValue || quantity.Value <= 0)
            return BadRequest("Quantity must be positive.");

        // Parse side as string to avoid enum binding issues (Swagger may send "Buy", "0", etc.)
        var sideParsed = ParseOrderSide(side!);
        if (sideParsed == null)
            return BadRequest("Side must be Buy or Sell (or 0/1).");

        var marketIdTrimmed = marketId!.Trim();
        var userIdTrimmed = userId!.Trim();
        var operatorIdTrimmed = operatorId!.Trim();

        var orderType = sideParsed == OrderSide.Buy ? OrderType.Bid : OrderType.Ask;
        var order = new Order(
            Guid.NewGuid(),
            userIdTrimmed,
            marketIdTrimmed,
            orderType,
            price!.Value,
            quantity!.Value,
            operatorIdTrimmed);

        var result = await _matchingEngine.ProcessOrderAsync(order, cancellationToken);

        PrintMarketState(marketIdTrimmed);
        return Ok(new SecondaryOrderResponse(
            result.OrderId,
            result.Matches.Select(m => new SecondaryMatchLevel(m.Price, m.Quantity, m.BuyerUserId, m.SellerUserId)).ToList()));
    }

    /// <summary>Retrieve the full order book (all bids and asks) for the given market. Use this to see the spread for Market Maker or bot logic.</summary>
    /// <param name="marketId">Outcome or market identifier (e.g. drake, drake-album).</param>
    /// <returns>Full order book: bids (descending by price), asks (ascending by price). Empty lists if no book or no orders.</returns>
    [HttpGet("book/{marketId}")]
    [ProducesResponseType(typeof(SecondaryBookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetBook(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            return BadRequest("MarketId is required.");

        var key = marketId.Trim();
        var book = _orderBookStore.GetOrderBook(key);
        if (book == null)
            return Ok(new SecondaryBookResponse(key, Array.Empty<SecondaryBookLevel>(), Array.Empty<SecondaryBookLevel>()));

        var bids = book.Bids.Select(o => new SecondaryBookLevel(o.Id, o.UserId, o.OperatorId, o.Price, o.Quantity)).ToList();
        var asks = book.Asks.Select(o => new SecondaryBookLevel(o.Id, o.UserId, o.OperatorId, o.Price, o.Quantity)).ToList();
        return Ok(new SecondaryBookResponse(key, bids, asks));
    }

    /// <summary>Primary tool for clearing liquidity provider positions. Removes all active orders for the given market that belong to the specified operator (e.g. mm-provider).</summary>
    /// <param name="marketId">Outcome or market identifier.</param>
    /// <param name="operatorId">Operator whose orders to cancel (e.g. mm-provider for Market Makers).</param>
    /// <returns>200 OK with the number of orders cancelled. 404 if the market does not exist.</returns>
    [HttpDelete("orders/{marketId}/{operatorId}")]
    [ProducesResponseType(typeof(CancelOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult DeleteOrdersByOperator(string marketId, string operatorId)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            return BadRequest("MarketId is required.");
        if (string.IsNullOrWhiteSpace(operatorId))
            return BadRequest("OperatorId is required.");

        var key = marketId.Trim();
        var book = _orderBookStore.GetOrderBook(key);
        if (book == null)
            return NotFound();

        var cancelled = book.RemoveOrdersByOperator(operatorId.Trim());
        PrintMarketState(key);
        return Ok(new CancelOrdersResponse(key, operatorId.Trim(), cancelled));
    }

    /// <summary>Submit multiple orders in one request (Market Maker cancel-and-replace). All orders must use operatorId "mm-provider" so the Ledger can track Market Makers separately from retail.</summary>
    /// <param name="request">List of orders to place. Each order must have operatorId equal to "mm-provider".</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>One result per order (OrderId and Matches). 400 if any order has operatorId != "mm-provider" or validation fails.</returns>
    [HttpPost("order/bulk")]
    [ProducesResponseType(typeof(BulkOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostOrderBulk([FromBody] BulkOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Orders == null || request.Orders.Count == 0)
            return BadRequest("At least one order is required in the bulk request.");

        var results = new List<SecondaryOrderResponse>();
        foreach (var item in request.Orders)
        {
            if (string.IsNullOrWhiteSpace(item.OperatorId) || !string.Equals(item.OperatorId.Trim(), MarketMakerOperatorId, StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Market Maker bulk orders must use operatorId \"{MarketMakerOperatorId}\". Got: {item.OperatorId ?? "(null)"}.");
            if (string.IsNullOrWhiteSpace(item.UserId))
                return BadRequest("UserId is required for every order.");
            if (string.IsNullOrWhiteSpace(item.MarketId))
                return BadRequest("MarketId is required for every order.");
            if (string.IsNullOrWhiteSpace(item.Side))
                return BadRequest("Side is required (Buy or Sell) for every order.");
            if (item.Price < 0.00m || item.Price > 1.00m)
                return BadRequest("Price must be between 0.00 and 1.00 for every order.");
            if (item.Quantity <= 0)
                return BadRequest("Quantity must be positive for every order.");

            var sideParsed = ParseOrderSide(item.Side);
            if (sideParsed == null)
                return BadRequest($"Side must be Buy or Sell (or 0/1). Got: {item.Side}.");

            var order = new Order(
                Guid.NewGuid(),
                item.UserId.Trim(),
                item.MarketId.Trim(),
                sideParsed == OrderSide.Buy ? OrderType.Bid : OrderType.Ask,
                item.Price,
                item.Quantity,
                item.OperatorId.Trim());

            var result = await _matchingEngine.ProcessOrderAsync(order, cancellationToken);
            results.Add(new SecondaryOrderResponse(
                result.OrderId,
                result.Matches.Select(m => new SecondaryMatchLevel(m.Price, m.Quantity, m.BuyerUserId, m.SellerUserId)).ToList()));
        }

        foreach (var marketId in request.Orders.Select(o => o.MarketId?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct())
            PrintMarketState(marketId!);

        return Ok(new BulkOrderResponse(results));
    }

    /// <summary>Fetches the order book and prints a terminal-style view (Bids in green, Asks in red). Use after PostOrder, BulkOrder, CancelOrders.</summary>
    private void PrintMarketState(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId)) return;

        var key = marketId.Trim();
        var book = _orderBookStore.GetOrderBook(key);

        if (book == null || (book.Bids.Count == 0 && book.Asks.Count == 0))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("--- MARKET: {0} (EMPTY/DELETED) ---", key);
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("--- MARKET: {0} ---", key);
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("BIDS (Green): [Price | Qty | User]");
        foreach (var o in book.Bids)
            Console.WriteLine("  {0,5:F2} | {1,6:F2} | {2}", o.Price, o.Quantity, o.UserId ?? o.OperatorId ?? "-");
        if (book.Bids.Count == 0)
            Console.WriteLine("  (none)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("ASKS (Red):   [Price | Qty | User]");
        foreach (var o in book.Asks)
            Console.WriteLine("  {0,5:F2} | {1,6:F2} | {2}", o.Price, o.Quantity, o.UserId ?? o.OperatorId ?? "-");
        if (book.Asks.Count == 0)
            Console.WriteLine("  (none)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("--------------------------");
        Console.ResetColor();
    }

    /// <summary>Parses side from query string: "Buy", "Sell", "0", "1" (case-insensitive).</summary>
    private static OrderSide? ParseOrderSide(string side)
    {
        if (string.IsNullOrWhiteSpace(side)) return null;
        var s = side.Trim();
        if (string.Equals(s, "Buy", StringComparison.OrdinalIgnoreCase) || s == "0") return OrderSide.Buy;
        if (string.Equals(s, "Sell", StringComparison.OrdinalIgnoreCase) || s == "1") return OrderSide.Sell;
        return null;
    }
}

// ----- Request/Response DTOs -----

/// <summary>Order request for the secondary market (query params). MarketId/OperatorId/UserId are strings (e.g. "drake", "apple-pay", "david"). Side accepts "Buy"/"Sell" or 0/1.</summary>
public record OrderRequest(
    [property: JsonPropertyName("marketId")] string MarketId,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("side"), JsonConverter(typeof(OrderSideJsonConverter))] OrderSide Side,
    [property: JsonPropertyName("operatorId")] string OperatorId,
    [property: JsonPropertyName("userId")] string UserId);

public record SecondaryOrderResponse(Guid OrderId, IReadOnlyList<SecondaryMatchLevel> Matches);

public record SecondaryMatchLevel(decimal Price, decimal Quantity, string BuyerUserId, string SellerUserId);

public record SecondaryBookResponse(string MarketId, IReadOnlyList<SecondaryBookLevel> Bids, IReadOnlyList<SecondaryBookLevel> Asks);

public record SecondaryBookLevel(Guid OrderId, string UserId, string? OperatorId, decimal Price, decimal Quantity);

/// <summary>Request body for POST /api/secondary/order/bulk. All orders must use operatorId "mm-provider".</summary>
public record BulkOrderRequest(IReadOnlyList<BulkOrderItem> Orders);

/// <summary>Single order in a bulk request. operatorId must be "mm-provider" for Market Maker tracking.</summary>
public record BulkOrderItem(
    string MarketId,
    decimal Price,
    decimal Quantity,
    string Side,
    string OperatorId,
    string UserId);

/// <summary>Response for bulk order: one result per order (OrderId and Matches).</summary>
public record BulkOrderResponse(IReadOnlyList<SecondaryOrderResponse> Results);

/// <summary>Response for DELETE /api/secondary/orders/{marketId}/{operatorId}: number of orders cancelled for that market and operator.</summary>
public record CancelOrdersResponse(string MarketId, string OperatorId, int CancelledCount);
