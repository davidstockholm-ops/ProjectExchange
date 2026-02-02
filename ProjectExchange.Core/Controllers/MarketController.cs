using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>Markets and order books: active events (tradeable markets) and per-outcome order book (bids/asks).</summary>
[ApiController]
[Route("api/markets")]
public class MarketController : ControllerBase
{
    private readonly IOutcomeOracle _oracle;
    private readonly MarketService _marketService;

    public MarketController(IOutcomeOracle oracle, MarketService marketService)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
        _marketService = marketService ?? throw new ArgumentNullException(nameof(marketService));
    }

    /// <summary>
    /// **Active Events** — Returns all currently tradeable (active, non-expired) markets for all celebrities (e.g. Drake, Elon).
    /// Each event has an OutcomeId; use that in the Order Book endpoint to see bids and asks.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ActiveMarketResponse>), StatusCodes.Status200OK)]
    public IActionResult GetActive()
    {
        var events = _oracle.GetActiveEvents();
        var response = events.Select(e => new ActiveMarketResponse(
            e.Id,
            e.Title,
            e.Type,
            e.OutcomeId,
            e.ActorId,
            e.ResponsibleOracleId,
            e.DurationMinutes,
            e.CreatedAt,
            e.ExpiresAt));
        return Ok(response);
    }

    /// <summary>
    /// **Order Book** — Returns the order book for the given outcome: bids (descending by price) and asks (ascending by price).
    /// Use OutcomeId from Active Events. Empty lists if the outcome has no orders yet.
    /// </summary>
    /// <param name="outcomeId">Outcome identifier (e.g. from GET /api/markets/active).</param>
    [HttpGet("orderbook/{outcomeId}")]
    [ProducesResponseType(typeof(OrderBookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetOrderBook(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            return BadRequest("OutcomeId is required.");
        var book = _marketService.GetOrderBook(outcomeId.Trim());
        if (book == null)
            return Ok(new OrderBookResponse(outcomeId.Trim(), Array.Empty<OrderBookLevel>(), Array.Empty<OrderBookLevel>()));
        var bids = book.Bids.Select(o => new OrderBookLevel(o.Id, o.UserId, o.Price, o.Quantity)).ToList();
        var asks = book.Asks.Select(o => new OrderBookLevel(o.Id, o.UserId, o.Price, o.Quantity)).ToList();
        return Ok(new OrderBookResponse(outcomeId.Trim(), bids, asks));
    }
}

/// <summary>DTO for an active (tradeable) market.</summary>
public record ActiveMarketResponse(
    Guid Id,
    string Title,
    string Type,
    string OutcomeId,
    string ActorId,
    string ResponsibleOracleId,
    int DurationMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Order book for an outcome: bids and asks.</summary>
public record OrderBookResponse(string OutcomeId, IReadOnlyList<OrderBookLevel> Bids, IReadOnlyList<OrderBookLevel> Asks);

/// <summary>Single level in the order book (price and quantity).</summary>
public record OrderBookLevel(Guid OrderId, Guid UserId, decimal Price, decimal Quantity);
