namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence for copy-trading follow relationship: FollowerId follows LeaderId (Master).
/// Table: followers (snake_case columns via UseSnakeCaseNamingConvention).
/// </summary>
public class FollowerEntity
{
    public long Id { get; set; }
    /// <summary>User/operator ID that follows the leader (e.g. "user-dashboard").</summary>
    public string FollowerId { get; set; } = string.Empty;
    /// <summary>Leader/Master ID (e.g. "DrakeOfficial", "OVO-Insider").</summary>
    public string LeaderId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
