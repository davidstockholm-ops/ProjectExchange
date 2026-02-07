namespace ProjectExchange.Core.Liquidity;

/// <summary>
/// Global safety switch for liquidity: which providers are enabled and which markets are restricted.
/// Bind from configuration section "LiquiditySettings" in appsettings.json.
/// </summary>
public class LiquiditySettings
{
    public const string SectionName = "LiquiditySettings";

    /// <summary>Provider IDs that are allowed to serve quotes (e.g. "Internal", "Partner_A", "Partner_B"). Empty = none enabled.</summary>
    public IReadOnlyList<string> EnabledProviders { get; set; } = Array.Empty<string>();

    /// <summary>Market/outcome IDs where liquidity is restricted (no quotes from any provider). Empty = no restrictions.</summary>
    public IReadOnlyList<string> RestrictedMarkets { get; set; } = Array.Empty<string>();

    /// <summary>Returns true if the given provider is enabled.</summary>
    public bool IsProviderEnabled(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return false;
        return EnabledProviders?.Any(p => string.Equals(p, providerId.Trim(), StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    /// <summary>Returns true if the given market is restricted (no liquidity).</summary>
    public bool IsMarketRestricted(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId)) return false;
        return RestrictedMarkets?.Any(m => string.Equals(m, marketId.Trim(), StringComparison.OrdinalIgnoreCase)) ?? false;
    }
}
