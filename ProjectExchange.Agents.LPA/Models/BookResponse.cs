namespace ProjectExchange.Agents.LPA.Models;

/// <summary>Response from GET /api/secondary/book/{marketId}.</summary>
public record BookResponse(
    string MarketId,
    IReadOnlyList<BookLevel> Bids,
    IReadOnlyList<BookLevel> Asks);

/// <summary>Single level (bid or ask) in the order book.</summary>
public record BookLevel(
    Guid OrderId,
    string UserId,
    string? OperatorId,
    decimal Price,
    decimal Quantity);
