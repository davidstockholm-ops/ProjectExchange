using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Core.Controllers;

/// <summary>
/// Copy-trading API: follow a leader (Master) so their orders are mirrored to the follower.
/// Persists follow relations in the followers table and keeps CopyTradingService in sync.
/// </summary>
[ApiController]
[Route("api/copy-trading")]
[Produces("application/json")]
public class CopyTradingController : ControllerBase
{
    private readonly ProjectExchangeDbContext _dbContext;
    private readonly CopyTradingService _copyTradingService;

    public CopyTradingController(ProjectExchangeDbContext dbContext, CopyTradingService copyTradingService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _copyTradingService = copyTradingService ?? throw new ArgumentNullException(nameof(copyTradingService));
    }

    /// <summary>
    /// Start following a leader. When the leader places an order, a matching order is created for the follower.
    /// </summary>
    [HttpPost("follow")]
    [ProducesResponseType(typeof(FollowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FollowResponse>> Follow([FromBody] FollowRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FollowerId) || string.IsNullOrWhiteSpace(request.LeaderId))
            return BadRequest("followerId and leaderId are required.");
        if (string.Equals(request.FollowerId.Trim(), request.LeaderId.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest("followerId and leaderId must be different.");

        var followerId = request.FollowerId.Trim();
        var leaderId = request.LeaderId.Trim();

        var exists = await _dbContext.Followers
            .AnyAsync(f => f.FollowerId == followerId && f.LeaderId == leaderId, cancellationToken);
        if (exists)
            return Ok(new FollowResponse { FollowerId = followerId, LeaderId = leaderId, AlreadyFollowing = true });

        _dbContext.Followers.Add(new FollowerEntity
        {
            FollowerId = followerId,
            LeaderId = leaderId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        _copyTradingService.Follow(followerId, leaderId);

        return Ok(new FollowResponse { FollowerId = followerId, LeaderId = leaderId, AlreadyFollowing = false });
    }

    /// <summary>
    /// Returns the list of leader IDs that the given user (follower) is following.
    /// </summary>
    [HttpGet("following")]
    [ProducesResponseType(typeof(FollowingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FollowingResponse>> GetFollowing([FromQuery] string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");

        var leaderIds = await _dbContext.Followers
            .Where(f => f.FollowerId == userId!.Trim())
            .Select(f => f.LeaderId)
            .ToListAsync(cancellationToken);

        return Ok(new FollowingResponse { FollowerId = userId!.Trim(), LeaderIds = leaderIds });
    }
}

public class FollowRequest
{
    public string FollowerId { get; set; } = string.Empty;
    public string LeaderId { get; set; } = string.Empty;
}

public class FollowResponse
{
    public string FollowerId { get; set; } = string.Empty;
    public string LeaderId { get; set; } = string.Empty;
    public bool AlreadyFollowing { get; set; }
}

public class FollowingResponse
{
    public string FollowerId { get; set; } = string.Empty;
    public IReadOnlyList<string> LeaderIds { get; set; } = Array.Empty<string>();
}
