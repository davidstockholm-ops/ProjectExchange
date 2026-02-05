using System.Text.Json;
using ProjectExchange.Core.Auditing;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Mock matching engine: adds the order to the book keyed by order.OutcomeId (e.g. marketId), runs matching, returns result.
/// Writes OrderPlaced and TradeMatched events to the audit store for each order and match.
/// </summary>
public class MockMatchingEngine : IMatchingEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string EventOrderPlaced = "OrderPlaced";
    private const string EventTradeMatched = "TradeMatched";

    private readonly IOrderBookStore _orderBookStore;
    private readonly IDomainEventStore? _domainEventStore;

    public MockMatchingEngine(IOrderBookStore orderBookStore, IDomainEventStore? domainEventStore = null)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
        _domainEventStore = domainEventStore;
    }

    /// <inheritdoc />
    public async Task<ProcessOrderResult> ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        var book = _orderBookStore.GetOrCreateOrderBook(order.OutcomeId);
        book.AddOrder(order);

        if (_domainEventStore != null)
        {
            var orderPayload = JsonSerializer.Serialize(new
            {
                order.Id,
                order.UserId,
                order.OperatorId,
                order.OutcomeId,
                Type = order.Type.ToString(),
                order.Price,
                order.Quantity
            }, JsonOptions);
            await _domainEventStore.AppendAsync(EventOrderPlaced, orderPayload, marketId: order.OutcomeId, userId: order.UserId, cancellationToken);
        }

        var matches = book.MatchOrders();

        if (_domainEventStore != null)
        {
            foreach (var m in matches)
            {
                var matchPayload = JsonSerializer.Serialize(new
                {
                    m.Price,
                    m.Quantity,
                    m.BuyerUserId,
                    m.SellerUserId,
                    OutcomeId = order.OutcomeId
                }, JsonOptions);
                await _domainEventStore.AppendAsync(EventTradeMatched, matchPayload, marketId: order.OutcomeId, userId: m.BuyerUserId, cancellationToken);
                await _domainEventStore.AppendAsync(EventTradeMatched, matchPayload, marketId: order.OutcomeId, userId: m.SellerUserId, cancellationToken);
            }
        }

        return new ProcessOrderResult(order.Id, matches);
    }
}
