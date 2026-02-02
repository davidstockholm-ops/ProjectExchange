namespace ProjectExchange.Core.Markets;

/// <summary>
/// Result of a match between a bid and an ask: price, quantity, buyer and seller user IDs.
/// </summary>
public record MatchResult(decimal Price, decimal Quantity, string BuyerUserId, string SellerUserId);

/// <summary>
/// Order book for a market: Bids sorted descending by price, Asks sorted ascending by price.
/// </summary>
public class OrderBook
{
    /// <summary>Bids: best (highest) price first.</summary>
    private readonly List<Order> _bids = new();

    /// <summary>Asks: best (lowest) price first.</summary>
    private readonly List<Order> _asks = new();

    /// <summary>Bids in descending order by price (best bid first).</summary>
    public IReadOnlyList<Order> Bids => _bids.AsReadOnly();

    /// <summary>Asks in ascending order by price (best ask first).</summary>
    public IReadOnlyList<Order> Asks => _asks.AsReadOnly();

    /// <summary>
    /// Places the order in the correct side (Bids or Asks) and keeps the list sorted.
    /// </summary>
    public void AddOrder(Order order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        if (order.Type == OrderType.Bid)
        {
            _bids.Add(order);
            _bids.Sort((a, b) => b.Price.CompareTo(a.Price)); // descending
        }
        else
        {
            _asks.Add(order);
            _asks.Sort((a, b) => a.Price.CompareTo(b.Price)); // ascending
        }
    }

    /// <summary>
    /// Matches top of book: while best bid price >= best ask price, fills at match price (resting order's price),
    /// decrements quantities, removes filled orders, and returns all match results.
    /// </summary>
    public IReadOnlyList<MatchResult> MatchOrders()
    {
        var results = new List<MatchResult>();

        while (_bids.Count > 0 && _asks.Count > 0 && _bids[0].Price >= _asks[0].Price)
        {
            var bid = _bids[0];
            var ask = _asks[0];
            decimal matchQuantity = Math.Min(bid.Quantity, ask.Quantity);
            // Match price = price of the order that was already in the book (resting / maker).
            // Here we use the ask's price; in production we would use maker price when we have time priority.
            decimal matchPrice = ask.Price;

            var matchResult = new MatchResult(matchPrice, matchQuantity, bid.UserId, ask.UserId);
            results.Add(matchResult);

            bid.ReduceBy(matchQuantity);
            ask.ReduceBy(matchQuantity);

            if (bid.Quantity == 0)
                _bids.RemoveAt(0);
            if (ask.Quantity == 0)
                _asks.RemoveAt(0);

            // TODO: Integration with LedgerService — record the financial transaction for this match.
            // e.g. await _ledgerService.PostTransactionAsync(entries for buyer debit, seller credit, at matchPrice * matchQuantity);
        }

        return results;
    }
}
