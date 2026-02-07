namespace ProjectExchange.Core.Liquidity;

/// <summary>
/// Placeholder liquidity provider for external partners. Register with providerId "Partner_A" or "Partner_B".
/// Returns empty quotes until integrated with partner API.
/// </summary>
public class PartnerLiquidityProvider : ILiquidityProvider
{
    private readonly string _providerId;

    public PartnerLiquidityProvider(string providerId)
    {
        _providerId = string.IsNullOrWhiteSpace(providerId) ? "Partner" : providerId.Trim();
    }

    public string ProviderId => _providerId;

    /// <inheritdoc />
    public Task<LiquidityQuoteResult> GetQuotesAsync(string marketId, CancellationToken cancellationToken = default)
    {
        var result = new LiquidityQuoteResult(
            marketId ?? string.Empty,
            _providerId,
            BestBid: null,
            BestAsk: null,
            Spread: null,
            Bids: Array.Empty<QuoteLevel>(),
            Asks: Array.Empty<QuoteLevel>());
        return Task.FromResult(result);
    }
}
