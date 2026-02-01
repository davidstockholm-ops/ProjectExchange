namespace ProjectExchange.Core.Markets;

/// <summary>
/// Registry of valid outcome IDs (markets that have been opened).
/// Used to reject orders for outcomes that don't exist in the system.
/// </summary>
public interface IOutcomeRegistry
{
    bool IsValid(string outcomeId);
    void Register(string outcomeId);
}
