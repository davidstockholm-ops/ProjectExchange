namespace ProjectExchange.Core.Markets;

/// <summary>
/// Registry of valid outcome IDs (markets that have been opened).
/// Used to reject orders for outcomes that don't exist in the system.
/// For binary markets, register both YES and NO as separate tradable instruments via RegisterBinaryMarket.
/// </summary>
public interface IOutcomeRegistry
{
    bool IsValid(string outcomeId);
    void Register(string outcomeId);
    /// <summary>Registers both YES and NO outcome IDs for a binary market so each is a separate tradable instrument.</summary>
    void RegisterBinaryMarket(string baseMarketId);
}
