using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence model for <see cref="Order"/> (audit/snapshot).
/// </summary>
public class OrderEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OutcomeId { get; set; } = string.Empty;
    public OrderType Type { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
}
