using System.Collections.Concurrent;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Social;

/// <summary>
/// In-memory follow graph and mirror logic: followers copy a master's orders (same outcome, price, type; scaled quantity).
/// </summary>
public class CopyTradingService
{
    /// <summary>Master ID -> set of follower IDs.</summary>
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _followersByMaster = new();

    /// <summary>Fixed quantity for mirrored orders when not scaling (e.g. 10 units).</summary>
    private const decimal DefaultMirroredQuantity = 10m;

    /// <summary>
    /// Registers followerId as following masterId (master becomes a "Master Trader" for copy-trading).
    /// </summary>
    public void Follow(Guid followerId, Guid masterId)
    {
        if (followerId == masterId)
            return;
        var set = _followersByMaster.GetOrAdd(masterId, _ => new HashSet<Guid>());
        lock (set) { set.Add(followerId); }
    }

    /// <summary>Unfollows (optional; not in spec but useful).</summary>
    public void Unfollow(Guid followerId, Guid masterId)
    {
        if (_followersByMaster.TryGetValue(masterId, out var set))
            lock (set) { set.Remove(followerId); }
    }

    /// <summary>Returns follower IDs for a master. Empty if not a master.</summary>
    public IReadOnlyList<Guid> GetFollowers(Guid masterId)
    {
        if (!_followersByMaster.TryGetValue(masterId, out var set))
            return Array.Empty<Guid>();
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
                quantity);
            list.Add(order);
        }
        return Task.FromResult<IReadOnlyList<Order>>(list);
    }
}
