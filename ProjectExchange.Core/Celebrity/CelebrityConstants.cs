namespace ProjectExchange.Core.Celebrity;

/// <summary>
/// Well-known IDs and names for celebrity copy-trading and market holding accounts.
/// Supports multiple actors (e.g. Drake, Elon) via GetMainOperatingAccountName(actorId).
/// </summary>
public static class CelebrityConstants
{
    /// <summary>System operator for platform accounts (e.g. Market Holding per outcome).</summary>
    public static readonly Guid SystemOperatorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Name prefix for outcome-specific Market Holding accounts (one per outcome).</summary>
    public const string MarketHoldingAccountNamePrefix = "Market Holding Account - ";

    /// <summary>Default celebrity main operating account name when actorId is omitted (backward compatibility).</summary>
    public const string DefaultMainOperatingAccountName = "Drake Main Operating Account";

    /// <summary>Returns the main operating account name for the given actor (e.g. "Drake" -> "Drake Main Operating Account").</summary>
    public static string GetMainOperatingAccountName(string? actorId) =>
        string.IsNullOrWhiteSpace(actorId) ? DefaultMainOperatingAccountName : $"{actorId.Trim()} Main Operating Account";

    /// <summary>Default Master Trader ID used when simulating a trade (copy-trading). Use this operator when calling Simulate so mirrors apply.</summary>
    public static readonly Guid MasterTraderId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>Default balance when auto-creating celebrity wallets for testing/agentic use.</summary>
    public const decimal DefaultAutoCreateBalance = 1_000_000m;

    /// <summary>Platform Treasury account used when auto-funding celebrity wallets (testing/agentic).</summary>
    public static readonly Guid PlatformTreasuryAccountId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public const string PlatformTreasuryAccountName = "Platform Treasury";
}
