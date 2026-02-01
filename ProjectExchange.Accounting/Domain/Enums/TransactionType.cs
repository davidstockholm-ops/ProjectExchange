namespace ProjectExchange.Accounting.Domain.Enums;

/// <summary>
/// Type of ledger transaction (e.g. Trade for order-book matches).
/// </summary>
public enum TransactionType
{
    /// <summary>Order-book match between buyer and seller.</summary>
    Trade,

    /// <summary>Other/legacy (e.g. Drake clearing, settlement).</summary>
    Other
}
