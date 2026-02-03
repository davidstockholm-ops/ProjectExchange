using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Core.Controllers;

/// <summary>Portfolio: aggregated holdings per account from the double-entry outcome ledger (LedgerEntries).</summary>
[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly PortfolioService _portfolioService;

    public PortfolioController(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));
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
        var holdings = await _portfolioService.GetPortfolioAsync(accountId, cancellationToken);
        var response = new PortfolioResponse(accountId, holdings);
        Console.WriteLine($"[PORTFOLIO] Account {accountId} -> {holdings.Count} asset(s): {string.Join(", ", holdings.Select(kv => $"{kv.Key}: {kv.Value}"))}");
        return Ok(response);
    }
}

/// <summary>Portfolio response: account id and holdings (asset type -> balance).</summary>
public record PortfolioResponse(string AccountId, IReadOnlyDictionary<string, decimal> Holdings);
