using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Liquidity;

/// <summary>
/// Liquidity provider that reads quotes from the internal order book. Register as "Internal" in DI.
/// </summary>
public class InternalLiquidityProvider : ILiquidityProvider
{
    public const string ProviderId = "Internal";

    private readonly IOrderBookStore _orderBookStore;

    public InternalLiquidityProvider(IOrderBookStore orderBookStore)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
    }

    string ILiquidityProvider.ProviderId => ProviderId;

    /// <inheritdoc />
    public Task<LiquidityQuoteResult> GetQuotesAsync(string marketId, CancellationToken cancellationToken = default)
    {
        var book = _orderBookStore.GetOrderBook(marketId?.Trim() ?? string.Empty);
        if (book == null || (book.Bids.Count == 0 && book.Asks.Count == 0))
        {
            return Task.FromResult(new LiquidityQuoteResult(
                marketId ?? string.Empty,
                ProviderId,
                BestBid: null,
                BestAsk: null,
                Spread: null,
                Bids: Array.Empty<QuoteLevel>(),
                Asks: Array.Empty<QuoteLevel>()));
        }

        var bids = book.Bids.Select(o => new QuoteLevel(o.Price, o.Quantity)).ToList();
        var asks = book.Asks.Select(o => new QuoteLevel(o.Price, o.Quantity)).ToList();
        decimal? bestBid = bids.Count > 0 ? bids[0].Price : null;
        decimal? bestAsk = asks.Count > 0 ? asks[0].Price : null;
        decimal? spread = (bestBid.HasValue && bestAsk.HasValue) ? bestAsk.Value - bestBid.Value : null;

        var result = new LiquidityQuoteResult(
            marketId ?? string.Empty,
            ProviderId,
            bestBid,
            bestAsk,
            spread,
            bids,
            asks);

        return Task.FromResult(result);
    }
}
