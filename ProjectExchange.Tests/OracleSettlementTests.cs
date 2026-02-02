using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Oracle settlement: register a Celebrity market via the Oracle, call outcome-reached,
/// verify market is settled and Auto-Settlement triggers without errors using string-based IDs.
/// </summary>
public class OracleSettlementTests
{
    private const string OperatorId = "celebrity-op";
    private const string ActorId = "Drake";
    private const string MarketTitle = "Album Drop";
    private const string MarketType = "Celebrity";
    private const int DurationMinutes = 10;
    private const decimal TradeAmount = 100m;

    /// <summary>
    /// Register a Celebrity market via the Oracle. Run a simulate trade (clearing).
    /// Call outcome-reached for that market. Verify market status reflects settlement
    /// and Auto-Settlement logic triggers without errors using string-based IDs.
    /// </summary>
    [Fact]
    public async Task OracleSettlement_RegisterCelebrityMarket_OutcomeReached_MarketSettledAutoSettlementSucceeds()
    {
        var (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent) = EnterpriseTestSetup.CreateCelebrityStack();

        // 1. Register a Celebrity market via the Oracle (string-based actor and operator)
        var evt = oracle.CreateMarketEvent(ActorId, MarketTitle, MarketType, DurationMinutes);
        Assert.NotNull(evt.OutcomeId);
        Assert.True(evt.IsActive);
        Assert.Equal(ActorId, evt.ActorId);
        Assert.Equal(MarketType, evt.Type);

        var outcomeId = evt.OutcomeId;

        // 2. Create operator account (string ID) and run a simulate trade so there is clearing to settle
        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(
            celebrityAccountId,
            CelebrityConstants.GetMainOperatingAccountName(ActorId),
            AccountType.Asset,
            OperatorId);
        await accountRepo.CreateAsync(celebrityAccount);

        var signal = oracle.SimulateTrade(OperatorId, TradeAmount, outcomeId, "Album Outcome", ActorId);
        var clearingTxId = copyTradingEngine.GetLastClearingTransactionIdForOutcome(outcomeId);
        if (clearingTxId == null)
            await copyTradingEngine.ExecuteCopyTradeAsync(signal);

        Assert.NotNull(copyTradingEngine.GetLastClearingTransactionIdForOutcome(outcomeId));

        var balanceAfterClearing = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(TradeAmount, balanceAfterClearing);

        // 3. Call outcome-reached for that market
        var result = await oracle.NotifyOutcomeReachedAsync(outcomeId);

        // 4. Verify market status changes to 'Settled' and Auto-Settlement triggers without errors
        Assert.Equal(outcomeId, result.OutcomeId);
        Assert.NotEmpty(result.NewSettlementTransactionIds);
        Assert.True(
            result.Message.Contains("Settlement", StringComparison.OrdinalIgnoreCase) ||
            result.Message.Contains("settled", StringComparison.OrdinalIgnoreCase),
            $"Expected settlement message; got: {result.Message}");

        var finalBalance = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(0m, finalBalance);

        // 5. Idempotent: second outcome-reached returns already-settled (no error)
        var result2 = await oracle.NotifyOutcomeReachedAsync(outcomeId);
        Assert.Equal(outcomeId, result2.OutcomeId);
        Assert.NotEmpty(result2.AlreadySettledClearingIds);
    }
}
