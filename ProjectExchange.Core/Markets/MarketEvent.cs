namespace ProjectExchange.Core.Markets;

/// <summary>
/// A tradeable market event: Flash (short expiry) or Base (longer expiry).
/// Tracks which IOutcomeOracle is responsible for its settlement and which actor (celebrity) it belongs to.
/// </summary>
public class MarketEvent
{
    public Guid Id { get; }
    public string Title { get; }
    public string Type { get; }
    public string OutcomeId { get; }
    /// <summary>Actor (celebrity) ID this market is for (e.g. "Drake", "Elon").</summary>
    public string ActorId { get; }
    /// <summary>Identifier of the IOutcomeOracle responsible for settling this market.</summary>
    public string ResponsibleOracleId { get; }
    public int DurationMinutes { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public MarketEvent(Guid id, string title, string type, string outcomeId, string actorId, string responsibleOracleId, int durationMinutes, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        Title = title ?? string.Empty;
        Type = type ?? "Base";
        OutcomeId = outcomeId ?? throw new ArgumentNullException(nameof(outcomeId));
        ActorId = actorId ?? string.Empty;
        ResponsibleOracleId = responsibleOracleId ?? throw new ArgumentNullException(nameof(responsibleOracleId));
        DurationMinutes = durationMinutes;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public bool IsActive => DateTimeOffset.UtcNow < ExpiresAt;
}
