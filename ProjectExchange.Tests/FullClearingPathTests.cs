using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// End-to-end clearing path: wallet (string IDs) → order → match → ledger updated.
/// Verifies that the full clearing flow with string-based OperatorId/UserId updates the Ledger correctly.
/// </summary>
public class FullClearingPathTests
{
    private const string OperatorApplePay = "apple-pay";
    private const string OperatorGooglePay = "google-pay";
    private const string UserDavid = "user-david";
    private const string MarketDrakeAlbum = "drake-album";
    private const decimal InitialCredits = 100m;
    private const decimal OrderPrice = 0.5m;
    private const decimal OrderQuantity = 50m;

    /// <summary>
    /// Initialize a wallet for user-david under operator apple-pay with 100 credits.
    /// Place a Buy order for 50 (qty) on drake-album; simulate a match with a seller (google-pay).
    /// Verify that the apple-pay balance in the Ledger is updated correctly (100 - price*qty = 75).
    /// </summary>
    [Fact]
    public async Task FullClearingPath_WalletBuyMatch_ApplePayBalanceUpdatedInLedger()
    {
        var (accountRepo, _, ledgerService, marketService) = EnterpriseTestSetup.CreateMarketStack();

        // 1. Initialize wallet for user-david under operator apple-pay with 100 credits
        var buyerAccountId = Guid.NewGuid();
        var buyerAccount = new Account(buyerAccountId, "user-david Wallet", AccountType.Asset, OperatorApplePay);
        await accountRepo.CreateAsync(buyerAccount);

        var sellerAccountId = Guid.NewGuid();
        var sellerAccount = new Account(sellerAccountId, "Wallet", AccountType.Asset, OperatorGooglePay);
        await accountRepo.CreateAsync(sellerAccount);

        var sinkAccountId = Guid.NewGuid();
        var sinkAccount = new Account(sinkAccountId, "Sink", AccountType.Asset, "sink-op");
        await accountRepo.CreateAsync(sinkAccount);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(buyerAccountId, InitialCredits, EntryType.Debit, SettlementPhase.Clearing),
            new(sinkAccountId, InitialCredits, EntryType.Credit, SettlementPhase.Clearing)
        });

        var buyerBalanceBefore = await ledgerService.GetAccountBalanceAsync(buyerAccountId, null);
        Assert.Equal(InitialCredits, buyerBalanceBefore);

        // 2. Place a Sell order first (google-pay), then Buy order (apple-pay) on drake-album at 0.5 for 50 qty
        var ask = new Order(Guid.NewGuid(), OperatorGooglePay, MarketDrakeAlbum, OrderType.Ask, OrderPrice, OrderQuantity);
        await marketService.PlaceOrderAsync(ask);

        var bid = new Order(Guid.NewGuid(), OperatorApplePay, MarketDrakeAlbum, OrderType.Bid, OrderPrice, OrderQuantity);
        var result = await marketService.PlaceOrderAsync(bid);

        // 3. Simulate a match and verify match occurred
        Assert.Equal(1, result.MatchCount);
        Assert.Single(result.TradeTransactionIds);

        decimal notional = OrderPrice * OrderQuantity; // 25
        decimal expectedApplePayBalance = InitialCredits - notional; // 75

        // 4. Verify apple-pay balance in the Ledger is updated correctly
        var applePayBalances = await ledgerService.GetOperatorBalancesAsync(OperatorApplePay);
        Assert.NotNull(applePayBalances);
        Assert.True(applePayBalances.Count >= 1, "Operator apple-pay should have at least one account.");
        var applePayBalance = applePayBalances.Values.Sum();
        Assert.Equal(expectedApplePayBalance, applePayBalance);
    }
}
