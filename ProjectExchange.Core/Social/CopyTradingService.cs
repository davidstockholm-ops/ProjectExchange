using System.Collections.Concurrent;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Social;

/// <summary>
/// In-memory follow graph and mirror logic: followers copy a master's orders (same outcome, price, type; scaled quantity).
/// </summary>
public class CopyTradingService
{
    /// <summary>Master ID -> set of follower IDs (string IDs for flexible names e.g. "david", "apple-pay").</summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _followersByMaster = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fixed quantity for mirrored orders when not scaling (e.g. 10 units).</summary>
    private const decimal DefaultMirroredQuantity = 10m;

    /// <summary>
    /// Registers followerId as following masterId (master becomes a "Master Trader" for copy-trading).
    /// </summary>
    public void Follow(string followerId, string masterId)
    {
        if (string.IsNullOrWhiteSpace(followerId) || string.IsNullOrWhiteSpace(masterId) || string.Equals(followerId, masterId, StringComparison.OrdinalIgnoreCase))
            return;
        var set = _followersByMaster.GetOrAdd(masterId.Trim(), _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (set) { set.Add(followerId.Trim()); }
    }

    /// <summary>Unfollows (optional; not in spec but useful).</summary>
    public void Unfollow(string followerId, string masterId)
    {
        if (string.IsNullOrWhiteSpace(masterId)) return;
        if (_followersByMaster.TryGetValue(masterId.Trim(), out var set))
            lock (set) { set.Remove(followerId?.Trim() ?? string.Empty); }
    }

    /// <summary>Returns follower IDs for a master. Empty if not a master.</summary>
    public IReadOnlyList<string> GetFollowers(string masterId)
    {
        if (string.IsNullOrWhiteSpace(masterId)) return Array.Empty<string>();
        if (!_followersByMaster.TryGetValue(masterId.Trim(), out var set))
            return Array.Empty<string>();
        lock (set) { return set.ToList(); }
    }

    /// <summary>
    /// Builds mirrored orders for every follower of masterOrder.UserId: same OutcomeId, Price, Type;
    /// quantity is a fixed default (e.g. 10) or a simple scale of the master's quantity.
    /// </summary>
    public Task<IReadOnlyList<Order>> MirrorOrderAsync(Order masterOrder, CancellationToken cancellationToken = default)
    {
        if (masterOrder == null)
            throw new ArgumentNullException(nameof(masterOrder));

        var followers = GetFollowers(masterOrder.UserId);
        if (followers.Count == 0)
            return Task.FromResult<IReadOnlyList<Order>>(Array.Empty<Order>());

        decimal quantity = DefaultMirroredQuantity;
        if (masterOrder.Quantity > 0)
            quantity = Math.Min(DefaultMirroredQuantity, Math.Max(0.01m, masterOrder.Quantity * 0.1m));

        var list = new List<Order>(followers.Count);
        foreach (var followerId in followers)
        {
            var order = new Order(
                Guid.NewGuid(),
                followerId,
                masterOrder.OutcomeId,
                masterOrder.Type,
                masterOrder.Price,
                quantity,
                masterOrder.OperatorId);
            list.Add(order);
        }
        return Task.FromResult<IReadOnlyList<Order>>(list);
    }
}
