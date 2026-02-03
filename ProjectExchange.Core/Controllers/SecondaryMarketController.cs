using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>
/// Secondary market API: submit orders and retrieve order book by market.
/// Enterprise: OperatorId and UserId are mandatory for settlement and the internal accounting ledger.
/// </summary>
[ApiController]
[Route("api/secondary")]
[Produces("application/json")]
public class SecondaryMarketController : ControllerBase
{
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

        return Ok(new SecondaryOrderResponse(
            result.OrderId,
            result.Matches.Select(m => new SecondaryMatchLevel(m.Price, m.Quantity, m.BuyerUserId, m.SellerUserId)).ToList()));
    }

    /// <summary>Retrieve the current bids and asks for the given market.</summary>
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
