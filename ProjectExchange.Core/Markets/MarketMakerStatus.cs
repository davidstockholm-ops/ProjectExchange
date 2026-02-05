namespace ProjectExchange.Core.Markets;

/// <summary>
/// Mutable status flag set by MarketMakerService for health checks.
/// </summary>
public class MarketMakerStatus : IMarketMakerStatus
{
    public bool IsRunning { get; set; }
}
