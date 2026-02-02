namespace ProjectExchange.Core.Markets;

/// <summary>
/// Result of processing an order: order id and any matches produced.
/// </summary>
public record ProcessOrderResult(Guid OrderId, IReadOnlyList<MatchResult> Matches);

/// <summary>
/// Matching engine for the secondary market: accepts an order, adds it to the book, and runs matching.
/// Enterprise: used with OperatorId and UserId for settlement and the internal accounting ledger.
/// </summary>
public interface IMatchingEngine
{
    /// <summary>
    /// Process an order: add to the book keyed by order.OutcomeId (e.g. marketId), run matching, return result.
    /// </summary>
    Task<ProcessOrderResult> ProcessOrderAsync(Order order, CancellationToken cancellationToken = default);
}
