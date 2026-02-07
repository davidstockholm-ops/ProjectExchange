namespace ProjectExchange.Core.Liquidity;

/// <summary>
/// Provider of liquidity (quotes) for a market. Multiple instances can be registered (e.g. Internal, Partner_A, Partner_B).
/// Safety and routing are controlled via LiquiditySettings (EnabledProviders, RestrictedMarkets).
/// </summary>
public interface ILiquidityProvider
{
    /// <summary>Unique provider identifier (e.g. "Internal", "Partner_A", "Partner_B"). Used by LiquiditySettings.EnabledProviders.</summary>
    string ProviderId { get; }

    /// <summary>Returns current quotes (bid/ask) for the given market. Empty if no liquidity.</summary>
    Task<LiquidityQuoteResult> GetQuotesAsync(string marketId, CancellationToken cancellationToken = default);
}

/// <summary>Result of a liquidity quote request: best bid/ask and optional levels.</summary>
public record LiquidityQuoteResult(
    string MarketId,
    string ProviderId,
    decimal? BestBid,
    decimal? BestAsk,
    decimal? Spread,
    IReadOnlyList<QuoteLevel> Bids,
    IReadOnlyList<QuoteLevel> Asks);

/// <summary>Single price level (price and quantity).</summary>
public record QuoteLevel(decimal Price, decimal Quantity);
