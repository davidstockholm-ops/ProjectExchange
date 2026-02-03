namespace ProjectExchange.Core.Markets;

/// <summary>
/// Resolves the outcome asset type from market/outcome metadata (e.g. outcome id or market name).
/// Ensures the asset being minted is never hardcoded but always derived from the market being traded.
/// </summary>
public interface IOutcomeAssetTypeResolver
{
    /// <summary>Returns the asset type for the given outcome (e.g. "drake-album" -> "DRAKE_ALBUM").</summary>
    string GetOutcomeAssetType(string outcomeId);
}
