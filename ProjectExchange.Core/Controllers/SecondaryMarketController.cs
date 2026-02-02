using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    /// <summary>Submit an order to the secondary market. Use query parameters for a form-like UX in Swagger. OperatorId and UserId are required (Enterprise).</summary>
    [HttpPost("order")]
    [ProducesResponseType(typeof(SecondaryOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SecondaryOrderResponse>> PostOrder([FromQuery] OrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest("Request is required.");

        // Enterprise: OperatorId and UserId are mandatory for settlement and the accounting ledger.
        if (string.IsNullOrWhiteSpace(request.OperatorId))
            return BadRequest("OperatorId is required.");
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("UserId is required.");
        if (string.IsNullOrWhiteSpace(request.MarketId))
            return BadRequest("MarketId is required.");
        if (request.Price < 0.00m || request.Price > 1.00m)
            return BadRequest("Price must be between 0.00 and 1.00.");
        if (request.Quantity <= 0)
            return BadRequest("Quantity must be positive.");

        var marketId = request.MarketId.Trim();
        var userId = request.UserId.Trim();
        var operatorId = request.OperatorId.Trim();
        Console.WriteLine($"--- TRADE READY: [User: {userId}] [Op: {operatorId}] [Market: {marketId}] [Side: {request.Side}] ---");

        var orderType = request.Side == OrderSide.Buy ? OrderType.Bid : OrderType.Ask;
        var order = new Order(
            Guid.NewGuid(),
            userId,
            marketId,
            orderType,
            request.Price,
            request.Quantity,
            operatorId);

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
}

// ----- Request/Response DTOs -----

/// <summary>Order request for the secondary market (query params). MarketId/OperatorId/UserId are strings (e.g. "drake", "apple-pay", "david"). Side accepts "Buy"/"Sell" or 0/1.</summary>
public record OrderRequest(
    string MarketId,
    decimal Price,
    decimal Quantity,
    [property: JsonConverter(typeof(OrderSideJsonConverter))] OrderSide Side,
    [property: BindRequired] string OperatorId,
    [property: BindRequired] string UserId);

public record SecondaryOrderResponse(Guid OrderId, IReadOnlyList<SecondaryMatchLevel> Matches);

public record SecondaryMatchLevel(decimal Price, decimal Quantity, string BuyerUserId, string SellerUserId);

public record SecondaryBookResponse(string MarketId, IReadOnlyList<SecondaryBookLevel> Bids, IReadOnlyList<SecondaryBookLevel> Asks);

public record SecondaryBookLevel(Guid OrderId, string UserId, string? OperatorId, decimal Price, decimal Quantity);
