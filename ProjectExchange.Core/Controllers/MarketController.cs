using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

[ApiController]
[Route("api/markets")]
public class MarketController : ControllerBase
{
    private readonly IOutcomeOracle _oracle;

    public MarketController(IOutcomeOracle oracle)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
    }

    /// <summary>
    /// Returns all currently tradeable (active, non-expired) markets for all celebrities (e.g. Drake, Elon).
    /// </summary>
    [HttpGet("active")]
    public IActionResult GetActive()
    {
        var events = _oracle.GetActiveEvents();
        var response = events.Select(e => new ActiveMarketResponse(
            e.Id,
            e.Title,
            e.Type,
            e.OutcomeId,
            e.ActorId,
            e.ResponsibleOracleId,
            e.DurationMinutes,
            e.CreatedAt,
            e.ExpiresAt));
        return Ok(response);
    }
}

/// <summary>DTO for an active (tradeable) market.</summary>
public record ActiveMarketResponse(
    Guid Id,
    string Title,
    string Type,
    string OutcomeId,
    string ActorId,
    string ResponsibleOracleId,
    int DurationMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
