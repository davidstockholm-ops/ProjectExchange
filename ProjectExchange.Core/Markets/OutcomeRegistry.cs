using System.Collections.Concurrent;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// In-memory registry of valid outcome IDs. Outcomes are registered when a market is opened (e.g. CelebrityOracleService).
/// </summary>
public class OutcomeRegistry : IOutcomeRegistry
{
    private readonly ConcurrentDictionary<string, byte> _outcomes = new();

    public bool IsValid(string outcomeId) => !string.IsNullOrWhiteSpace(outcomeId) && _outcomes.ContainsKey(outcomeId.Trim());

    public void Register(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            return;
        _outcomes.TryAdd(outcomeId.Trim(), 0);
    }

    /// <inheritdoc />
    public void RegisterBinaryMarket(string baseMarketId)
    {
        if (string.IsNullOrWhiteSpace(baseMarketId))
            return;
        var (yesId, noId) = BinaryMarketOutcomes.GetBothOutcomeIds(baseMarketId.Trim());
        _outcomes.TryAdd(yesId, 0);
        _outcomes.TryAdd(noId, 0);
    }
}
