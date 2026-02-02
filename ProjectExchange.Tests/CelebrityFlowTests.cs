using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Integration tests for the Celebrity flow: create celebrity Main Operating Account,
/// simulate a trade (Clearing), trigger Outcome Reached (Settlement), verify final balance.
/// Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class CelebrityFlowTests
{
    private static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CelebrityOracleService Oracle,
        CopyTradingEngine CopyTradingEngine,
        AutoSettlementAgent AutoSettlementAgent) CreateCelebrityStack() =>
        EnterpriseTestSetup.CreateCelebrityStack();

    [Fact]
    public async Task FullLifecycle_CreateAccount_SimulateTrade_OutcomeReached_FinalBalanceReflectsCompletedSettlement()
    {
        var (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent) = CreateCelebrityStack();

        var operatorId = Guid.NewGuid();
        const decimal tradeAmount = 250m;
        const string outcomeId = "outcome-wins";
        const string outcomeName = "Wins";
        const string actorId = "Drake";

        // 1. Create the celebrity Main Operating Account
        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(
            celebrityAccountId,
            CelebrityConstants.GetMainOperatingAccountName(actorId),
            AccountType.Asset,
            operatorId);
        await accountRepo.CreateAsync(celebrityAccount);

        // 2. Simulate a trade (Clearing phase): oracle emits signal, CopyTradingEngine posts Clearing entries
        var signal = oracle.SimulateTrade(operatorId, tradeAmount, outcomeId, outcomeName, actorId);
        var clearingTxId = copyTradingEngine.GetLastClearingTransactionIdForOutcome(outcomeId);
        if (clearingTxId == null)
            clearingTxId = await copyTradingEngine.ExecuteCopyTradeAsync(signal);

        Assert.NotNull(clearingTxId);

        // Celebrity's account should show Clearing-phase debit (balance = +tradeAmount)
        var balanceAfterClearing = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(tradeAmount, balanceAfterClearing);

        // 3. Trigger Outcome Reached (Settlement phase): AutoSettlementAgent posts reverse Settlement entries
        var result = await autoSettlementAgent.SettleOutcomeAsync(outcomeId);
        Assert.NotEmpty(result.NewSettlementTransactionIds);
        Assert.True(result.Message.Contains("Settlement", StringComparison.OrdinalIgnoreCase) || result.Message.Contains("settled", StringComparison.OrdinalIgnoreCase));

        // 4. Verify final balance reflects completed settlement: Clearing (+debit) + Settlement (-credit) = 0
        var finalBalance = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(0m, finalBalance);
    }

    [Fact]
    public async Task FullLifecycle_WithPhaseFilter_ClearingAndSettlementBalancesMatchExpected()
    {
        var (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent) = CreateCelebrityStack();

        var operatorId = Guid.NewGuid();
        const decimal tradeAmount = 100m;
        const string outcomeId = "outcome-y";
        const string actorId = "Drake";

        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(
            celebrityAccountId,
            CelebrityConstants.GetMainOperatingAccountName(actorId),
            AccountType.Asset,
            operatorId);
        await accountRepo.CreateAsync(celebrityAccount);

        oracle.SimulateTrade(operatorId, tradeAmount, outcomeId, "Outcome Y", actorId);
        if (copyTradingEngine.GetLastClearingTransactionIdForOutcome(outcomeId) == null)
            await copyTradingEngine.ExecuteCopyTradeAsync(
                new CelebrityTradeSignal(Guid.NewGuid(), operatorId, tradeAmount, outcomeId, "Outcome Y", actorId));

        var clearingOnly = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, SettlementPhase.Clearing);
        Assert.Equal(tradeAmount, clearingOnly);

        await autoSettlementAgent.SettleOutcomeAsync(outcomeId);

        var clearingAfterSettlement = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, SettlementPhase.Clearing);
        var settlementBalance = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, SettlementPhase.Settlement);
        Assert.Equal(tradeAmount, clearingAfterSettlement);
        Assert.Equal(-tradeAmount, settlementBalance);
        Assert.Equal(0m, clearingAfterSettlement + settlementBalance);
    }

    [Fact]
    public async Task OutcomeReached_Idempotent_SecondCallReturnsAlreadySettledAndLedgerUnchanged()
    {
        var (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent) = CreateCelebrityStack();

        var operatorId = Guid.NewGuid();
        const decimal tradeAmount = 100m;
        const string outcomeId = "outcome-idempotent";
        const string actorId = "Drake";

        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(
            celebrityAccountId,
            CelebrityConstants.GetMainOperatingAccountName(actorId),
            AccountType.Asset,
            operatorId);
        await accountRepo.CreateAsync(celebrityAccount);

        oracle.SimulateTrade(operatorId, tradeAmount, outcomeId, "Idempotent", actorId);
        if (copyTradingEngine.GetLastClearingTransactionIdForOutcome(outcomeId) == null)
            await copyTradingEngine.ExecuteCopyTradeAsync(
                new CelebrityTradeSignal(Guid.NewGuid(), operatorId, tradeAmount, outcomeId, "Idempotent", actorId));

        var result1 = await autoSettlementAgent.SettleOutcomeAsync(outcomeId);
        Assert.NotEmpty(result1.NewSettlementTransactionIds);
        var balanceAfterFirst = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(0m, balanceAfterFirst);

        var result2 = await autoSettlementAgent.SettleOutcomeAsync(outcomeId);
        Assert.NotEmpty(result2.AlreadySettledClearingIds);
        var balanceAfterSecond = await ledgerService.GetAccountBalanceAsync(celebrityAccountId, null);
        Assert.Equal(0m, balanceAfterSecond);
    }
}
