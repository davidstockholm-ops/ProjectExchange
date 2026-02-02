namespace ProjectExchange.Core.Markets;

/// <summary>
/// Side of an order in the secondary market API (Buy = Bid, Sell = Ask).
/// Enterprise requirement: used with OperatorId and UserId for settlement and the accounting ledger.
/// </summary>
public enum OrderSide
{
    Buy,
    Sell
}
