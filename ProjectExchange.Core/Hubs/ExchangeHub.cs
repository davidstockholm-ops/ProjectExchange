using Microsoft.AspNetCore.SignalR;

namespace ProjectExchange.Core.Hubs;

/// <summary>
/// SignalR hub for real-time exchange updates. Clients receive TradeMatched when a match occurs.
/// </summary>
public class ExchangeHub : Hub
{
    /// <summary>
    /// Method name for server-to-client trade notifications. Server calls Clients.All.SendAsync(TradeMatchedMethod, payload) with { marketId, price, quantity, side }.
    /// </summary>
    public const string TradeMatchedMethod = "TradeMatched";
}
