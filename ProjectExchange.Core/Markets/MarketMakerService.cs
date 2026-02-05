using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using ProjectExchange.Core.Hubs;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Auto-trading HostedService for drake-album: places orders every 3 seconds.
/// Price 0.80–0.90; quantity random 10–500. About every third run places a matching bid+ask so trades fill Trade History quickly.
/// CancellationToken is respected so restarts shut down cleanly without orphaned work.
/// </summary>
public class MarketMakerService : BackgroundService
{
    private const string MarketId = "drake-album";
    private const string OperatorId = "mm-provider";
    private const string UserId = "market-maker";
    private const decimal PriceMin = 0.80m;
    private const decimal PriceMax = 0.90m;
    private const int IntervalSeconds = 3;
    private const int QuantityMin = 10;
    private const int QuantityMax = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketMakerService> _logger;
    private readonly MarketMakerStatus _status;
    private readonly IHubContext<ExchangeHub> _hubContext;

    public MarketMakerService(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketMakerService> logger,
        MarketMakerStatus status,
        IHubContext<ExchangeHub> hubContext)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _status.IsRunning = true;
        try
        {
            _logger.LogInformation("[MarketMaker] Auto-trading started for {MarketId}. Interval: {Seconds}s", MarketId, IntervalSeconds);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PlaceOrdersAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MarketMaker] Tick failed for {MarketId}", MarketId);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            _status.IsRunning = false;
        }
    }

    private async Task PlaceOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var matchingEngine = scope.ServiceProvider.GetRequiredService<IMatchingEngine>();
        var tradeHistoryStore = scope.ServiceProvider.GetRequiredService<ITradeHistoryStore>();

        // ~1/3 chance: place bid then ask at same price so they match and fill Trade History quickly
        bool placeMatchingPair = Random.Shared.NextSingle() < 0.34f;
        decimal price = PriceMin + (decimal)(Random.Shared.NextDouble() * (double)(PriceMax - PriceMin));
        price = Math.Round(price, 2);
        decimal quantity = Random.Shared.Next(QuantityMin, QuantityMax + 1);

        if (placeMatchingPair)
        {
            var bid = new Order(Guid.NewGuid(), UserId, MarketId, OrderType.Bid, price, quantity, OperatorId);
            var bidResult = await matchingEngine.ProcessOrderAsync(bid, cancellationToken);
            foreach (var m in bidResult.Matches)
                tradeHistoryStore.Record(MarketId, m.Price, m.Quantity, "Buy");

            var ask = new Order(Guid.NewGuid(), UserId, MarketId, OrderType.Ask, price, quantity, OperatorId);
            var askResult = await matchingEngine.ProcessOrderAsync(ask, cancellationToken);
            foreach (var m in askResult.Matches)
            {
                tradeHistoryStore.Record(MarketId, m.Price, m.Quantity, "Sell");
                await _hubContext.Clients.All.SendAsync(ExchangeHub.TradeMatchedMethod, new { marketId = MarketId, price = m.Price, quantity = m.Quantity, side = "Sell" }, cancellationToken);
            }

            _logger.LogInformation("[MarketMaker] Matched pair at {Price} for {MarketId} (bid+ask)", price, MarketId);
        }
        else
        {
            var isBid = Random.Shared.Next(2) == 0;
            var order = new Order(
                Guid.NewGuid(),
                UserId,
                MarketId,
                isBid ? OrderType.Bid : OrderType.Ask,
                price,
                quantity,
                OperatorId);
            var result = await matchingEngine.ProcessOrderAsync(order, cancellationToken);
            var sideStr = isBid ? "Buy" : "Sell";
            foreach (var m in result.Matches)
            {
                tradeHistoryStore.Record(MarketId, m.Price, m.Quantity, sideStr);
                await _hubContext.Clients.All.SendAsync(ExchangeHub.TradeMatchedMethod, new { marketId = MarketId, price = m.Price, quantity = m.Quantity, side = sideStr }, cancellationToken);
            }
            _logger.LogInformation("[MarketMaker] Single {Side} at {Price} for {MarketId}", sideStr, price, MarketId);
        }
    }
}
