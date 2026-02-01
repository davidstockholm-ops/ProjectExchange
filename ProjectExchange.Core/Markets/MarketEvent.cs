namespace ProjectExchange.Core.Markets;

/// <summary>
/// A tradeable market event: Flash (short expiry) or Base (longer expiry).
/// </summary>
public class MarketEvent
{
    public Guid Id { get; }
    public string Title { get; }
    public string Type { get; }
    public string OutcomeId { get; }
    public int DurationMinutes { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public MarketEvent(Guid id, string title, string type, string outcomeId, int durationMinutes, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        Title = title ?? string.Empty;
        Type = type ?? "Base";
        OutcomeId = outcomeId ?? throw new ArgumentNullException(nameof(outcomeId));
        DurationMinutes = durationMinutes;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public bool IsActive => DateTimeOffset.UtcNow < ExpiresAt;
}
