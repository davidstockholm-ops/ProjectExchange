using System.Text.Json;
using ProjectExchange.Core.Auditing;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Computes a user's net position by aggregating TradeMatched domain events (buyer +quantity, seller -quantity).
/// </summary>
public class PositionService : IPositionService
{
    private const string EventTradeMatched = "TradeMatched";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AuditService _auditService;

    public PositionService(AuditService auditService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NetPositionDto>> GetNetPositionAsync(string userId, string? marketId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Array.Empty<NetPositionDto>();

        try
        {
            var events = await _auditService.GetByUserIdAsync(userId.Trim(), cancellationToken);
            if (events == null)
                return Array.Empty<NetPositionDto>();

            var positions = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in events)
            {
                if (e?.EventType != EventTradeMatched)
                    continue;

                TradeMatchedPayload? payload = null;
                try
                {
                    if (string.IsNullOrEmpty(e.Payload))
                        continue;
                    payload = JsonSerializer.Deserialize<TradeMatchedPayload>(e.Payload, JsonOptions);
                }
                catch
                {
                    continue;
                }

                if (payload == null || string.IsNullOrWhiteSpace(payload.OutcomeId))
                    continue;

                var outcomeId = payload.OutcomeId.Trim();
                if (marketId != null && !string.Equals(outcomeId, marketId.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                var qty = payload.Quantity;
                if (string.Equals(payload.BuyerUserId, userId.Trim(), StringComparison.OrdinalIgnoreCase))
                    AddToPosition(positions, outcomeId, qty);
                else if (string.Equals(payload.SellerUserId, userId.Trim(), StringComparison.OrdinalIgnoreCase))
                    AddToPosition(positions, outcomeId, -qty);
            }

            return positions
                .Where(p => p.Value != 0)
                .Select(p => new NetPositionDto(p.Key, p.Value))
                .OrderBy(p => p.OutcomeId)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PositionService] GetNetPositionAsync failed (returning empty list): {ex}");
            return Array.Empty<NetPositionDto>();
        }
    }

    private static void AddToPosition(Dictionary<string, decimal> positions, string outcomeId, decimal delta)
    {
        positions.TryGetValue(outcomeId, out var current);
        positions[outcomeId] = current + delta;
    }

    private sealed class TradeMatchedPayload
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string? BuyerUserId { get; set; }
        public string? SellerUserId { get; set; }
        public string? OutcomeId { get; set; }
    }
}
