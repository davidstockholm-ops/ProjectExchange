namespace ProjectExchange.Accounting.Domain.Enums;

/// <summary>
/// Clearing & Settlement Split [2026-01-31]: internal obligations vs external settlement.
/// Clearing = internal debt/credit tracked before money moves externally.
/// Settlement = external payment/settlement executed.
/// </summary>
public enum SettlementPhase
{
    /// <summary>Internal ledger entry; obligation between parties before external settlement.</summary>
    Clearing,

    /// <summary>External settlement; payment instruction executed.</summary>
    Settlement
}
