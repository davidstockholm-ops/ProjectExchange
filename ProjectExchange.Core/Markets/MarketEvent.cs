namespace ProjectExchange.Core.Markets;

/// <summary>
/// Base tradeable market event type for any oracle (Base, Flash, Celebrity, Sports).
/// Use Type for event kind; OutcomeId identifies the asset in the secondary market (liquid contracts).
/// Adding new event types (e.g. Flash, Sports) does not require controller changes.
/// </summary>
public class MarketEvent
{
    /// <summary>Standard event types. Extend with "Flash", "Sports", etc. as needed.</summary>
    public static class EventType
    {
        public const string Base = "Base";
        public const string Flash = "Flash";
        public const string Celebrity = "Celebrity";
        public const string Sports = "Sports";
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Type { get; }
    public string OutcomeId { get; }
    /// <summary>Optional context (e.g. actor/celebrity ID). Implementation-specific.</summary>
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
