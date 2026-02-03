using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Core.Controllers;

/// <summary>Admin: market resolution and settlement (manual resolve-market).</summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly SettlementService _settlementService;

    public AdminController(SettlementService settlementService)
    {
        _settlementService = settlementService ?? throw new ArgumentNullException(nameof(settlementService));
    }

    /// <summary>
    /// Resolves a market for the winning outcome: settles all holders of the winning asset (removes tokens, pays USD).
    /// Returns number of accounts settled and total USD paid out.
    /// </summary>
    /// <param name="request">outcomeId, winningAssetType, settlementAccountId, optional usdPerToken.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("resolve-market")]
    [ProducesResponseType(typeof(ResolveMarketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResolveMarketResponse>> ResolveMarket(
        [FromBody] ResolveMarketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.OutcomeId))
            return BadRequest("outcomeId is required.");
        if (string.IsNullOrWhiteSpace(request.WinningAssetType))
            return BadRequest("winningAssetType is required.");
        if (request.SettlementAccountId == default)
            return BadRequest("settlementAccountId is required.");

        var (accountsSettled, totalUsdPaidOut) = await _settlementService.ResolveMarketAsync(
            request.OutcomeId,
            request.WinningAssetType,
            request.SettlementAccountId,
            request.UsdPerToken ?? 1.00m,
            cancellationToken);

        var response = new ResolveMarketResponse(accountsSettled, totalUsdPaidOut);
        return Ok(response);
    }
}

/// <summary>Request body for POST /api/admin/resolve-market.</summary>
public record ResolveMarketRequest(
    string OutcomeId,
    string WinningAssetType,
    Guid SettlementAccountId,
    decimal? UsdPerToken = 1.00m);

/// <summary>Response: number of accounts settled and total USD paid out.</summary>
public record ResolveMarketResponse(int AccountsSettled, decimal TotalUsdPaidOut);
