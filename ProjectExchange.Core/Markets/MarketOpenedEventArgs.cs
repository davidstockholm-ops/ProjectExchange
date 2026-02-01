namespace ProjectExchange.Core.Markets;

/// <summary>
/// Raised when a new market is created and its OrderBook is registered (for AI agents / subscribers).
/// </summary>
public class MarketOpenedEventArgs : EventArgs
{
    public MarketEvent MarketEvent { get; }

    public MarketOpenedEventArgs(MarketEvent marketEvent)
    {
        MarketEvent = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
    }
}
