using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>Portfolio: aggregated holdings per account from the double-entry outcome ledger (LedgerEntries). Positions from trades (PositionService) for liquid contracts.</summary>
[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly PortfolioService _portfolioService;
    private readonly IPositionService _positionService;

    public PortfolioController(PortfolioService portfolioService, IPositionService positionService)
    {
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));
        _positionService = positionService ?? throw new ArgumentNullException(nameof(positionService));
    }

    /// <summary>
    /// Returns the portfolio for the given account: each asset type and its balance (e.g. USD: 950, DRAKE_ALBUM: 10).
    /// Data is aggregated from LedgerEntries for this account.
    /// </summary>
    /// <param name="accountId">Account ID (Guid).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{accountId}")]
    [ProducesResponseType(typeof(PortfolioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PortfolioResponse>> GetPortfolio(string accountId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[PORTFOLIO] GET /api/portfolio/{accountId}");
        try
        {
            var holdings = await _portfolioService.GetPortfolioAsync(accountId, cancellationToken);
            var response = new PortfolioResponse(accountId, holdings ?? new Dictionary<string, decimal>());
            Console.WriteLine($"[PORTFOLIO] Account {accountId} -> {response.Holdings.Count} asset(s)");
            return Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PORTFOLIO] GetPortfolio failed for accountId={accountId}: {ex}");
            return Ok(new PortfolioResponse(accountId, new Dictionary<string, decimal>()));
        }
    }

    /// <summary>
    /// Returns net position per outcome for a user (from TradeMatched domain events). E.g. +100 YES for drake-album-yes.
    /// Use for liquid contracts / binary YES-NO positions in real-time.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="marketId">Optional. If set, only positions for this outcome/market are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("position")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PositionResponse>> GetPosition([FromQuery] string? userId, [FromQuery] string? marketId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");
        try
        {
            var positions = await _positionService.GetNetPositionAsync(userId!.Trim(), string.IsNullOrWhiteSpace(marketId) ? null : marketId!.Trim(), cancellationToken);
            return Ok(new PositionResponse(userId.Trim(), positions ?? Array.Empty<NetPositionDto>()));
        }
        catch
        {
            return Ok(new PositionResponse(userId!.Trim(), Array.Empty<NetPositionDto>()));
        }
    }
}

/// <summary>Portfolio response: account id and holdings (asset type -> balance).</summary>
public record PortfolioResponse(string AccountId, IReadOnlyDictionary<string, decimal> Holdings);

/// <summary>Net position response: userId and per-outcome positions (e.g. +100 YES).</summary>
public record PositionResponse(string UserId, IReadOnlyList<NetPositionDto> Positions);
