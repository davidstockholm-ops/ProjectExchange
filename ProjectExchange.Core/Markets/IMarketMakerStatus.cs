namespace ProjectExchange.Core.Markets;

/// <summary>
/// Reports whether MarketMakerService is currently running (for /health).
/// </summary>
public interface IMarketMakerStatus
{
    bool IsRunning { get; }
}
