using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Liquidity;

namespace ProjectExchange.Core.Controllers;

/// <summary>
/// Liquidity API: aggregated quotes from enabled providers and runtime toggle for EnabledProviders.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LiquidityController : ControllerBase
{
    private readonly IEnumerable<ILiquidityProvider> _providers;
    private readonly LiquiditySettings _settings;
    private readonly LiquiditySettingsRuntime _runtime;

    public LiquidityController(
        IEnumerable<ILiquidityProvider> providers,
        Microsoft.Extensions.Options.IOptions<LiquiditySettings> settings,
        LiquiditySettingsRuntime runtime)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>
    /// Returns aggregated quotes for the market from all enabled providers (respects LiquiditySettings and runtime override).
    /// If market is in RestrictedMarkets, returns 403.
    /// </summary>
    [HttpGet("quotes")]
    [ProducesResponseType(typeof(AggregatedQuotesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AggregatedQuotesResponse>> GetQuotes([FromQuery] string? marketId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            return BadRequest("marketId is required.");
        var id = marketId!.Trim();
        if (_settings.IsMarketRestricted(id))
            return StatusCode(403, new { error = "Market is restricted." });

        var enabled = _providers.Where(p => _runtime.IsProviderEnabled(p.ProviderId, _settings)).ToList();
        var results = new List<LiquidityQuoteResult>();
        foreach (var provider in enabled)
        {
            var quote = await provider.GetQuotesAsync(id, cancellationToken);
            results.Add(quote);
        }

        return Ok(new AggregatedQuotesResponse(id, results));
    }

    /// <summary>
    /// Returns current liquidity settings: config-based enabled providers and optional runtime override.
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(LiquiditySettingsResponse), StatusCodes.Status200OK)]
    public IActionResult GetSettings()
    {
        var configEnabled = _settings.EnabledProviders?.ToList() ?? new List<string>();
        var runtimeOverride = _runtime.GetEnabledProvidersOverride();
        return Ok(new LiquiditySettingsResponse(
            configEnabled,
            runtimeOverride,
            EffectiveEnabled: (runtimeOverride ?? configEnabled).ToList(),
            _settings.RestrictedMarkets?.ToList() ?? new List<string>()));
    }

    /// <summary>
    /// Toggle enabled providers at runtime. Pass the list of provider IDs to enable (e.g. ["Internal", "Partner_A"]).
    /// Pass null or empty body to revert to config.
    /// </summary>
    [HttpPatch("settings")]
    [ProducesResponseType(typeof(LiquiditySettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult UpdateSettings([FromBody] LiquiditySettingsPatchRequest? request)
    {
        if (request == null)
        {
            _runtime.SetEnabledProvidersOverride(null);
            return Ok(GetSettingsResponse());
        }
        _runtime.SetEnabledProvidersOverride(request.EnabledProviders ?? new List<string>());
        return Ok(GetSettingsResponse());
    }

    private LiquiditySettingsResponse GetSettingsResponse()
    {
        var configEnabled = _settings.EnabledProviders?.ToList() ?? new List<string>();
        var runtimeOverride = _runtime.GetEnabledProvidersOverride();
        return new LiquiditySettingsResponse(
            configEnabled,
            runtimeOverride,
            EffectiveEnabled: (runtimeOverride ?? configEnabled).ToList(),
            _settings.RestrictedMarkets?.ToList() ?? new List<string>());
    }
}

/// <summary>Aggregated quotes from all enabled providers for a market.</summary>
public record AggregatedQuotesResponse(string MarketId, IReadOnlyList<LiquidityQuoteResult> ProviderQuotes);

/// <summary>Current liquidity settings (config + runtime override + effective).</summary>
public record LiquiditySettingsResponse(
    IReadOnlyList<string> ConfigEnabledProviders,
    IReadOnlyList<string>? RuntimeOverrideProviders,
    IReadOnlyList<string> EffectiveEnabled,
    IReadOnlyList<string> RestrictedMarkets);

/// <summary>Request body for PATCH /api/liquidity/settings.</summary>
public record LiquiditySettingsPatchRequest(IReadOnlyList<string>? EnabledProviders);
