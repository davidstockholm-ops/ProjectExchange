using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// Ultimate system test: Path A (Matching), Path B (Event/Oracle), and Path C (Copy-Trading) as one organism.
/// Tests the Celebrity flow with dynamic actorId. Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class GrandFinalIntegrationTests
{
    private static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService,
        CelebrityOracleService Oracle) CreateFullStack() =>
        EnterpriseTestSetup.CreateFullStack();

    [Fact]
    public async Task Celebrity_Social_Liquidity_Cycle_Should_Work()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService, oracle) = CreateFullStack();

        const string actorId = "Drake";
        var celebrityId = Guid.NewGuid();
        var liquidityProviderId = Guid.NewGuid();
        var fan1Id = Guid.NewGuid();
        var fan2Id = Guid.NewGuid();
        var fan3Id = Guid.NewGuid();
        var fan4Id = Guid.NewGuid();
        var fan5Id = Guid.NewGuid();

        var celebrityAccount = new Account(Guid.NewGuid(), actorId, AccountType.Asset, celebrityId.ToString());
        var lpAccount = new Account(Guid.NewGuid(), "Liquidity Provider", AccountType.Asset, liquidityProviderId.ToString());
        var fan1Account = new Account(Guid.NewGuid(), "Fan1", AccountType.Asset, fan1Id.ToString());
        var fan2Account = new Account(Guid.NewGuid(), "Fan2", AccountType.Asset, fan2Id.ToString());
        var fan3Account = new Account(Guid.NewGuid(), "Fan3", AccountType.Asset, fan3Id.ToString());
        var fan4Account = new Account(Guid.NewGuid(), "Fan4", AccountType.Asset, fan4Id.ToString());
        var fan5Account = new Account(Guid.NewGuid(), "Fan5", AccountType.Asset, fan5Id.ToString());

        var sinkId = Guid.NewGuid();
        var sinkAccount = new Account(Guid.NewGuid(), "Sink", AccountType.Asset, sinkId.ToString());
        await accountRepo.CreateAsync(celebrityAccount);
        await accountRepo.CreateAsync(lpAccount);
        await accountRepo.CreateAsync(fan1Account);
        await accountRepo.CreateAsync(fan2Account);
        await accountRepo.CreateAsync(fan3Account);
        await accountRepo.CreateAsync(fan4Account);
        await accountRepo.CreateAsync(fan5Account);
        await accountRepo.CreateAsync(sinkAccount);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(celebrityAccount.Id, 25m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan1Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan2Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan3Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan4Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(fan5Account.Id, 5m, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccount.Id, 50m, EntryType.Credit, SettlementPhase.Clearing)
        });

        copyTradingService.Follow(fan1Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(fan2Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(fan3Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(fan4Id.ToString(), celebrityId.ToString());
        copyTradingService.Follow(fan5Id.ToString(), celebrityId.ToString());

        var evt = oracle.CreateMarketEvent(actorId, "Grand Final", "Flash", 5);
        var outcomeId = evt.OutcomeId;
        Assert.NotNull(outcomeId);
        Assert.True(evt.IsActive);
        Assert.Equal(actorId, evt.ActorId);

        var lpAsk = new Order(Guid.NewGuid(), liquidityProviderId.ToString(), outcomeId, OrderType.Ask, 0.50m, 150m);
        await marketService.PlaceOrderAsync(lpAsk);

        var celebrityBid = new Order(Guid.NewGuid(), celebrityId.ToString(), outcomeId, OrderType.Bid, 0.50m, 50m);
        var result = await marketService.PlaceOrderAsync(celebrityBid);

        Assert.True(result.MatchCount >= 1, $"{actorId}'s order should have matched.");
        Assert.True(result.TradeTransactionIds.Count >= 6, $"Six trades expected: 1 {actorId} + 5 Fans.");

        // Buyers pay (Credit): balances decrease. Seller (LP) receives (Debit): balance increases.
        var celebrityBalance = await ledgerService.GetAccountBalanceAsync(celebrityAccount.Id, null);
        var fan1Balance = await ledgerService.GetAccountBalanceAsync(fan1Account.Id, null);
        var fan2Balance = await ledgerService.GetAccountBalanceAsync(fan2Account.Id, null);
        var fan3Balance = await ledgerService.GetAccountBalanceAsync(fan3Account.Id, null);
        var fan4Balance = await ledgerService.GetAccountBalanceAsync(fan4Account.Id, null);
        var fan5Balance = await ledgerService.GetAccountBalanceAsync(fan5Account.Id, null);
        var lpBalance = await ledgerService.GetAccountBalanceAsync(lpAccount.Id, null);

        Assert.Equal(0m, celebrityBalance);  // 25 - 25 paid
        Assert.Equal(0m, fan1Balance);      // 5 - 5 paid
        Assert.Equal(0m, fan2Balance);
        Assert.Equal(0m, fan3Balance);
        Assert.Equal(0m, fan4Balance);
        Assert.Equal(0m, fan5Balance);
        Assert.Equal(50m, lpBalance);        // received 25 + 5*5

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Empty(book.Bids);
        Assert.Single(book.Asks);
        Assert.Equal(50m, book.Asks[0].Quantity);
    }
}
