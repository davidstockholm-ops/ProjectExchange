namespace ProjectExchange.Core.Liquidity;

/// <summary>
/// Runtime override for enabled liquidity providers. When set, overrides config (LiquiditySettings.EnabledProviders).
/// Used by LiquidityController toggle; inject as singleton.
/// </summary>
public class LiquiditySettingsRuntime
{
    private readonly object _lock = new();
    private List<string>? _enabledProvidersOverride;

    /// <summary>When non-null, these provider IDs are used instead of config. When null, config is used.</summary>
    public IReadOnlyList<string>? GetEnabledProvidersOverride()
    {
        lock (_lock)
            return _enabledProvidersOverride == null ? null : _enabledProvidersOverride.ToList();
    }

    /// <summary>Set runtime override for enabled providers. Pass null to revert to config.</summary>
    public void SetEnabledProvidersOverride(IReadOnlyList<string>? providerIds)
    {
        lock (_lock)
            _enabledProvidersOverride = providerIds?.ToList();
    }

    /// <summary>Returns true if the provider is enabled: runtime override if set, otherwise config.</summary>
    public bool IsProviderEnabled(string providerId, LiquiditySettings config)
    {
        var over = GetEnabledProvidersOverride();
        if (over != null)
            return over.Any(p => string.Equals(p, providerId?.Trim(), StringComparison.OrdinalIgnoreCase));
        return config.IsProviderEnabled(providerId ?? "");
    }
}
