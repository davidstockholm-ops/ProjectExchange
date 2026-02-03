namespace ProjectExchange.Core.Markets;

/// <summary>
/// Default resolver: derives OutcomeAssetType from OutcomeId (uppercase, hyphens to underscores).
/// E.g. "drake-album" -> "DRAKE_ALBUM", "outcome-xyz" -> "OUTCOME_XYZ".
/// </summary>
public sealed class OutcomeAssetTypeResolver : IOutcomeAssetTypeResolver
{
    public string GetOutcomeAssetType(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            return "OUTCOME_UNKNOWN";
        return outcomeId.Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant();
    }
}
