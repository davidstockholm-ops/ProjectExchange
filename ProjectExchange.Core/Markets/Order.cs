namespace ProjectExchange.Core.Markets;

/// <summary>
/// An order in the matching engine: Bid or Ask at a price (0.00–1.00) for an outcome.
/// Enterprise: OperatorId is required for secondary market and settlement/ledger.
/// </summary>
public class Order
{
    public Guid Id { get; }
    public string UserId { get; }
    /// <summary>Operator owning the settlement/ledger context. Required for secondary market (Enterprise).</summary>
    public string? OperatorId { get; }
    public string OutcomeId { get; }
    /// <summary>For binary markets: which contract leg (YES/NO). Null for non-binary or when not specified; can be derived from OutcomeId via BinaryMarketOutcomes.TryParseContractTypeFromOutcomeId.</summary>
    public ContractType? ContractType { get; }
    public OrderType Type { get; }
    public decimal Price { get; }
    /// <summary>Remaining quantity (decremented when matched).</summary>
    public decimal Quantity { get; private set; }

    public Order(Guid id, string userId, string outcomeId, OrderType type, decimal price, decimal quantity, string? operatorId = null, ContractType? contractType = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(outcomeId))
            throw new ArgumentException("OutcomeId is required.", nameof(outcomeId));
        if (price < 0.00m || price > 1.00m)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be between 0.00 and 1.00.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        Id = id;
        UserId = userId.Trim();
        OperatorId = operatorId?.Trim();
        OutcomeId = outcomeId.Trim();
        ContractType = contractType;
        Type = type;
        Price = price;
        Quantity = quantity;
    }

    /// <summary>Reduces remaining quantity by the given amount (used when matched).</summary>
    internal void ReduceBy(decimal amount)
    {
        if (amount <= 0 || amount > Quantity)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Quantity -= amount;
    }
}
