using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Drake;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// Ultimate system test: Path A (Matching), Path B (Event/Oracle), and Path C (Copy-Trading) as one organism.
/// Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class GrandFinalIntegrationTests
{
    private static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService,
        DrakeOracleService Oracle) CreateFullStack() =>
        EnterpriseTestSetup.CreateFullStack();

    [Fact]
    public async Task Drake_Social_Liquidity_Cycle_Should_Work()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService, oracle) = CreateFullStack();

        var drakeId = Guid.NewGuid();
        var liquidityProviderId = Guid.NewGuid();
        var fan1Id = Guid.NewGuid();
        var fan2Id = Guid.NewGuid();
        var fan3Id = Guid.NewGuid();
        var fan4Id = Guid.NewGuid();
        var fan5Id = Guid.NewGuid();

        var drakeAccount = new Account(Guid.NewGuid(), "Drake", AccountType.Asset, drakeId);
        var lpAccount = new Account(Guid.NewGuid(), "Liquidity Provider", AccountType.Asset, liquidityProviderId);
        var fan1Account = new Account(Guid.NewGuid(), "Fan1", AccountType.Asset, fan1Id);
        var fan2Account = new Account(Guid.NewGuid(), "Fan2", AccountType.Asset, fan2Id);
        var fan3Account = new Account(Guid.NewGuid(), "Fan3", AccountType.Asset, fan3Id);
        var fan4Account = new Account(Guid.NewGuid(), "Fan4", AccountType.Asset, fan4Id);
        var fan5Account = new Account(Guid.NewGuid(), "Fan5", AccountType.Asset, fan5Id);

        await accountRepo.CreateAsync(drakeAccount);
        await accountRepo.CreateAsync(lpAccount);
        await accountRepo.CreateAsync(fan1Account);
        await accountRepo.CreateAsync(fan2Account);
        await accountRepo.CreateAsync(fan3Account);
        await accountRepo.CreateAsync(fan4Account);
        await accountRepo.CreateAsync(fan5Account);

        copyTradingService.Follow(fan1Id, drakeId);
        copyTradingService.Follow(fan2Id, drakeId);
        copyTradingService.Follow(fan3Id, drakeId);
        copyTradingService.Follow(fan4Id, drakeId);
        copyTradingService.Follow(fan5Id, drakeId);

        var evt = oracle.CreateMarketEvent("Grand Final", "Flash", 5);
        var outcomeId = evt.OutcomeId;
        Assert.NotNull(outcomeId);
        Assert.True(evt.IsActive);

        var lpAsk = new Order(Guid.NewGuid(), liquidityProviderId, outcomeId, OrderType.Ask, 0.50m, 150m);
        await marketService.PlaceOrderAsync(lpAsk);

        var drakeBid = new Order(Guid.NewGuid(), drakeId, outcomeId, OrderType.Bid, 0.50m, 50m);
        var result = await marketService.PlaceOrderAsync(drakeBid);

        Assert.True(result.MatchCount >= 1, "Drake's order should have matched.");
        Assert.True(result.TradeTransactionIds.Count >= 6, "Six trades expected: 1 Drake + 5 Fans.");

        var drakeBalance = await ledgerService.GetAccountBalanceAsync(drakeAccount.Id, null);
        var fan1Balance = await ledgerService.GetAccountBalanceAsync(fan1Account.Id, null);
        var fan2Balance = await ledgerService.GetAccountBalanceAsync(fan2Account.Id, null);
        var fan3Balance = await ledgerService.GetAccountBalanceAsync(fan3Account.Id, null);
        var fan4Balance = await ledgerService.GetAccountBalanceAsync(fan4Account.Id, null);
        var fan5Balance = await ledgerService.GetAccountBalanceAsync(fan5Account.Id, null);
        var lpBalance = await ledgerService.GetAccountBalanceAsync(lpAccount.Id, null);

        Assert.Equal(25m, drakeBalance);
        Assert.Equal(5m, fan1Balance);
        Assert.Equal(5m, fan2Balance);
        Assert.Equal(5m, fan3Balance);
        Assert.Equal(5m, fan4Balance);
        Assert.Equal(5m, fan5Balance);
        Assert.Equal(-50m, lpBalance);

        var book = marketService.GetOrderBook(outcomeId);
        Assert.NotNull(book);
        Assert.Empty(book.Bids);
        Assert.Single(book.Asks);
        Assert.Equal(50m, book.Asks[0].Quantity);
    }
}
