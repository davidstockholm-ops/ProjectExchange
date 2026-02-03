using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Tests;

/// <summary>
/// Unit tests for AccountingService.BookTradeAsync (double-entry ledger).
/// </summary>
public class AccountingServiceTests
{
    private static AccountingService CreateService(
        out MockLedgerEntryRepository repo,
        out MockUnitOfWork uow)
    {
        repo = new MockLedgerEntryRepository();
        uow = new MockUnitOfWork();
        return new AccountingService(repo, uow);
    }

    [Fact]
    public async Task BookTradeAsync_SingleTrade_GeneratesExactlyFourLedgerEntries()
    {
        var service = CreateService(out var repo, out _);
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        await service.BookTradeAsync(
            buyerId,
            sellerId,
            cashAmount: 0.50m,
            outcomeAssetType: "DRAKE_WIN",
            outcomeQuantity: 1m);

        Assert.Equal(4, repo.CapturedEntries.Count);
    }

    [Fact]
    public async Task BookTradeAsync_USDEntries_AreBalanced()
    {
        var service = CreateService(out var repo, out _);
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        await service.BookTradeAsync(
            buyerId,
            sellerId,
            cashAmount: 0.50m,
            outcomeAssetType: "DRAKE_WIN",
            outcomeQuantity: 1m);

        var usdEntries = repo.CapturedEntries.Where(e => e.AssetType == AccountingService.AssetTypeCash).ToList();
        Assert.Equal(2, usdEntries.Count);

        var totalDebit = usdEntries.Where(e => e.Direction == EntryType.Debit).Sum(e => e.Amount);
        var totalCredit = usdEntries.Where(e => e.Direction == EntryType.Credit).Sum(e => e.Amount);
        Assert.Equal(totalDebit, totalCredit);
    }

    [Fact]
    public async Task BookTradeAsync_OutcomeEntries_MatchPassedAssetType()
    {
        var service = CreateService(out var repo, out _);
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        const string outcomeAssetType = "FLASH_RESOLVED";

        await service.BookTradeAsync(
            buyerId,
            sellerId,
            cashAmount: 1.25m,
            outcomeAssetType,
            outcomeQuantity: 2m);

        var outcomeEntries = repo.CapturedEntries.Where(e => e.AssetType == outcomeAssetType).ToList();
        Assert.Equal(2, outcomeEntries.Count);
        Assert.All(outcomeEntries, e => Assert.Equal(outcomeAssetType, e.AssetType));
    }

    [Fact]
    public async Task BookTradeAsync_AddRangeAsync_CalledWithCorrectData()
    {
        var service = CreateService(out var repo, out _);
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await service.BookTradeAsync(
            buyerId,
            sellerId,
            cashAmount: 0.50m,
            outcomeAssetType: "DRAKE_WIN",
            outcomeQuantity: 1m,
            timestamp: null);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.True(repo.AddRangeAsyncCalled);
        Assert.Equal(4, repo.CapturedEntries.Count);

        // Buyer: Credit USD, Debit outcome
        var buyerUsd = repo.CapturedEntries.First(e => e.AccountId == buyerId && e.AssetType == AccountingService.AssetTypeCash);
        Assert.Equal(EntryType.Credit, buyerUsd.Direction);
        Assert.Equal(0.50m, buyerUsd.Amount);

        var buyerOutcome = repo.CapturedEntries.First(e => e.AccountId == buyerId && e.AssetType == "DRAKE_WIN");
        Assert.Equal(EntryType.Debit, buyerOutcome.Direction);
        Assert.Equal(1m, buyerOutcome.Amount);

        // Seller: Debit USD, Credit outcome
        var sellerUsd = repo.CapturedEntries.First(e => e.AccountId == sellerId && e.AssetType == AccountingService.AssetTypeCash);
        Assert.Equal(EntryType.Debit, sellerUsd.Direction);
        Assert.Equal(0.50m, sellerUsd.Amount);

        var sellerOutcome = repo.CapturedEntries.First(e => e.AccountId == sellerId && e.AssetType == "DRAKE_WIN");
        Assert.Equal(EntryType.Credit, sellerOutcome.Direction);
        Assert.Equal(1m, sellerOutcome.Amount);

        Assert.All(repo.CapturedEntries, e =>
        {
            Assert.True(e.Timestamp >= before && e.Timestamp <= after, "Timestamp should be within test window.");
        });
    }

    [Fact]
    public async Task BookTradeAsync_ZeroCash_ThrowsArgumentOutOfRangeException()
    {
        var service = CreateService(out var repo, out _);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.BookTradeAsync(Guid.NewGuid(), Guid.NewGuid(), 0m, "X", 1m));
        Assert.False(repo.AddRangeAsyncCalled);
    }

    [Fact]
    public async Task BookTradeAsync_NullOrWhiteSpaceOutcomeAssetType_ThrowsArgumentException()
    {
        var service = CreateService(out var repo, out _);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BookTradeAsync(Guid.NewGuid(), Guid.NewGuid(), 1m, "", 1m));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BookTradeAsync(Guid.NewGuid(), Guid.NewGuid(), 1m, "   ", 1m));
        Assert.False(repo.AddRangeAsyncCalled);
    }

    private sealed class MockLedgerEntryRepository : ILedgerEntryRepository
    {
        public List<LedgerEntry> CapturedEntries { get; } = new();
        public bool AddRangeAsyncCalled { get; private set; }

        public Task AddRangeAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken = default)
        {
            AddRangeAsyncCalled = true;
            CapturedEntries.AddRange(entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LedgerEntry>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            var list = CapturedEntries.Where(e => e.AccountId == accountId).ToList();
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
        }

        public Task<IReadOnlyList<LedgerEntry>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default)
        {
            var list = CapturedEntries.Where(e => string.Equals(e.AssetType, assetType?.Trim(), StringComparison.Ordinal)).ToList();
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
        }
    }

    private sealed class MockUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
