namespace ProjectExchange.Accounting.Domain.Exceptions;

/// <summary>
/// Thrown when an order is placed for an outcome that is not registered (no market opened).
/// </summary>
public class InvalidOutcomeException : DomainException
{
    public string OutcomeId { get; }

    public InvalidOutcomeException(string outcomeId)
        : base($"Outcome '{outcomeId}' is not registered. Open a market first.")
    {
        OutcomeId = outcomeId ?? string.Empty;
    }
}
