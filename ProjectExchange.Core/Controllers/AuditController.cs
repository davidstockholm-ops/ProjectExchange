using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Auditing;

namespace ProjectExchange.Core.Controllers;

/// <summary>
/// Audit API: read back event stream by MarketId or UserId (Project Exchange Ultimate).
/// </summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditController(AuditService auditService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>Returns all domain events for the given market (order placed, trade matched), oldest first.</summary>
    [HttpGet("market/{marketId}")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByMarketId(string marketId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(marketId))
            return BadRequest("MarketId is required.");
        var events = await _auditService.GetByMarketIdAsync(marketId.Trim(), cancellationToken);
        return Ok(events);
    }

    /// <summary>Returns all domain events where the given user participated (order or match side), oldest first.</summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByUserId(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId is required.");
        var events = await _auditService.GetByUserIdAsync(userId.Trim(), cancellationToken);
        return Ok(events);
    }
}
