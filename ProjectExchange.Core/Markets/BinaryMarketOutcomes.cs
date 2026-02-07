namespace ProjectExchange.Core.Markets;

/// <summary>
/// Convention for binary markets: each market has two tradable instruments (YES and NO) with outcome IDs derived from base market ID.
/// Use when opening/registering binary markets so both legs are available in the order book.
/// </summary>
public static class BinaryMarketOutcomes
{
    public const string YesSuffix = "-yes";
    public const string NoSuffix = "-no";

    /// <summary>Outcome ID for the YES contract (e.g. "drake-album" -> "drake-album-yes").</summary>
    public static string GetYesOutcomeId(string baseMarketId)
    {
        if (string.IsNullOrWhiteSpace(baseMarketId))
            throw new ArgumentException("Base market ID is required.", nameof(baseMarketId));
        var baseId = baseMarketId.Trim();
        return baseId.EndsWith(YesSuffix, StringComparison.OrdinalIgnoreCase) ? baseId : baseId + YesSuffix;
    }

    /// <summary>Outcome ID for the NO contract (e.g. "drake-album" -> "drake-album-no").</summary>
    public static string GetNoOutcomeId(string baseMarketId)
    {
        if (string.IsNullOrWhiteSpace(baseMarketId))
            throw new ArgumentException("Base market ID is required.", nameof(baseMarketId));
        var baseId = baseMarketId.Trim();
        return baseId.EndsWith(NoSuffix, StringComparison.OrdinalIgnoreCase) ? baseId : baseId + NoSuffix;
    }

    /// <summary>Outcome ID for the given contract type.</summary>
    public static string GetOutcomeId(string baseMarketId, ContractType contractType)
    {
        return contractType == ContractType.Yes ? GetYesOutcomeId(baseMarketId) : GetNoOutcomeId(baseMarketId);
    }

    /// <summary>Both outcome IDs for a binary market (YES, NO). Use to register both in IOutcomeRegistry when opening a binary market.</summary>
    public static (string YesOutcomeId, string NoOutcomeId) GetBothOutcomeIds(string baseMarketId)
    {
        return (GetYesOutcomeId(baseMarketId), GetNoOutcomeId(baseMarketId));
    }

    /// <summary>Try to parse contract type from an outcome ID (e.g. "drake-album-yes" -> Yes). Returns null if not a binary outcome ID.</summary>
    public static ContractType? TryParseContractTypeFromOutcomeId(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId)) return null;
        var id = outcomeId.Trim();
        if (id.EndsWith(YesSuffix, StringComparison.OrdinalIgnoreCase)) return ContractType.Yes;
        if (id.EndsWith(NoSuffix, StringComparison.OrdinalIgnoreCase)) return ContractType.No;
        return null;
    }
}
