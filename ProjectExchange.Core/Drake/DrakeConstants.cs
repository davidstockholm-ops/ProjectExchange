namespace ProjectExchange.Core.Drake;

/// <summary>
/// Well-known IDs and names for Drake / copy-trading and market holding accounts.
/// </summary>
public static class DrakeConstants
{
    /// <summary>System operator for platform accounts (e.g. Market Holding per outcome).</summary>
    public static readonly Guid SystemOperatorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Name prefix for outcome-specific Market Holding accounts (one per outcome).</summary>
    public const string MarketHoldingAccountNamePrefix = "Market Holding Account - ";

    public const string DrakeMainOperatingAccountName = "Drake Main Operating Account";

    /// <summary>Default Master Trader ID used when simulating a trade (copy-trading). Use this operator when calling Simulate so mirrors apply.</summary>
    public static readonly Guid MasterTraderId = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
