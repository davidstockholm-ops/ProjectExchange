using System.Collections.Concurrent;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Base implementation for any event oracle (Base, Flash, Celebrity, Sports).
/// Holds generic logic: event storage, OrderBook registration, MarketOpened broadcast,
/// GetActiveEvents, and NotifyOutcomeReachedAsync (delegates to IOutcomeSettlementService).
/// Subclasses implement CreateMarketEventCore to build the specific MarketEvent and OracleId.
/// </summary>
/// <remarks>
/// Circular-dependency avoidance: this type takes <see cref="IServiceProvider"/> instead of
/// <see cref="IOutcomeSettlementService"/> so that oracles can be constructed without the settlement
/// agent. The chain Oracle → AutoSettlementAgent → CopyTradingEngine → IOutcomeOracle would be a
/// cycle; resolving IOutcomeSettlementService only when <see cref="NotifyOutcomeReachedAsync"/> runs
/// breaks that cycle. Controllers depend only on IMarketOracle/IOutcomeOracle, not on
/// IOutcomeSettlementService or AutoSettlementAgent.
/// </remarks>
public abstract class BaseOracleService : IMarketOracle
{
    private readonly IOrderBookStore _orderBookStore;
    private readonly IOutcomeRegistry? _outcomeRegistry;
    private readonly IServiceProvider _serviceProvider;
    protected readonly ConcurrentDictionary<Guid, MarketEvent> Events = new();

    protected BaseOracleService(
        IOrderBookStore orderBookStore,
        IServiceProvider serviceProvider,
        IOutcomeRegistry? outcomeRegistry = null)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _outcomeRegistry = outcomeRegistry;
    }

    public abstract string OracleId { get; }
    public event EventHandler<MarketOpenedEventArgs>? MarketOpened;

    /// <summary>
    /// Creates a market event: delegates to subclass to build the event, then registers OrderBook and broadcasts MarketOpened.
    /// </summary>
    public MarketEvent CreateMarketEvent(string title, string type, int durationMinutes, IReadOnlyDictionary<string, string>? context = null)
    {
        var evt = CreateMarketEventCore(title, type, durationMinutes, context);
        Events[evt.Id] = evt;
        _outcomeRegistry?.Register(evt.OutcomeId);
        _orderBookStore.GetOrCreateOrderBook(evt.OutcomeId);
        MarketOpened?.Invoke(this, new MarketOpenedEventArgs(evt));
        return evt;
    }

    /// <summary>
    /// Subclass builds the MarketEvent (id, outcomeId, actorId/context, responsibleOracleId, duration, expiry).
    /// </summary>
    protected abstract MarketEvent CreateMarketEventCore(string title, string type, int durationMinutes, IReadOnlyDictionary<string, string>? context);

    public IReadOnlyList<MarketEvent> GetActiveEvents()
    {
        var now = DateTimeOffset.UtcNow;
        return Events.Values.Where(e => e.ExpiresAt > now).ToList();
    }

    /// <summary>
    /// Triggers OutcomeReached: resolves <see cref="IOutcomeSettlementService"/> from DI here (lazy) to avoid
    /// constructor-cycle with AutoSettlementAgent → CopyTradingEngine → IOutcomeOracle, then delegates.
    /// </summary>
    public virtual Task<OutcomeReachedResult> NotifyOutcomeReachedAsync(
        string outcomeId,
        decimal? confidenceScore = null,
        IReadOnlyList<string>? sourceVerificationList = null,
        CancellationToken cancellationToken = default)
    {
        var settlementService = _serviceProvider.GetRequiredService<IOutcomeSettlementService>();
        return settlementService.SettleOutcomeAsync(outcomeId, confidenceScore, sourceVerificationList, cancellationToken);
    }
}
