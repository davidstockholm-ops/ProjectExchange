using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Tests;

/// <summary>
/// Unit tests for SettlementService.ResolveMarketAsync (manual resolve-market / admin flow).
/// </summary>
public class SettlementServiceTests
{
    [Fact]
    public async Task ResolveMarketAsync_NoHolders_ReturnsZeroAccountsAndZeroUsd()
    {
        var (settlementService, _) = CreateService(holderEntries: Array.Empty<LedgerEntry>());
        var settlementAccountId = Guid.NewGuid();

        var (accountsSettled, totalUsdPaidOut) = await settlementService.ResolveMarketAsync(
            "outcome-1", "DRAKE_WIN", settlementAccountId, 1.00m);

        Assert.Equal(0, accountsSettled);
        Assert.Equal(0, totalUsdPaidOut);
    }

    [Fact]
    public async Task ResolveMarketAsync_NullOrWhiteSpaceWinningAssetType_ThrowsArgumentException()
    {
        var (settlementService, _) = CreateService(holderEntries: Array.Empty<LedgerEntry>());
        var settlementAccountId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            settlementService.ResolveMarketAsync("o1", "", settlementAccountId));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            settlementService.ResolveMarketAsync("o1", "   ", settlementAccountId));
    }

    [Fact]
    public async Task ResolveMarketAsync_TwoHolders_SettlesTwoAccountsAndReturnsCorrectTotal()
    {
        var holderA = Guid.NewGuid();
        var holderB = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;
        var entries = new List<LedgerEntry>
        {
            new(holderA, "DRAKE_WIN", 3m, EntryType.Debit, ts),
            new(holderB, "DRAKE_WIN", 2m, EntryType.Debit, ts)
        };
        var (settlementService, mockRepo) = CreateService(holderEntries: entries);
        var settlementAccountId = Guid.NewGuid();

        var (accountsSettled, totalUsdPaidOut) = await settlementService.ResolveMarketAsync(
            "drake-album", "DRAKE_WIN", settlementAccountId, usdPerToken: 1.00m);

        Assert.Equal(2, accountsSettled);
        Assert.Equal(5.00m, totalUsdPaidOut); // 3 + 2
        Assert.True(mockRepo.AddRangeAsyncCalled);
        Assert.Equal(8, mockRepo.CapturedSettlementEntries.Count); // 4 entries per holder (holder credit outcome, debit USD; settlement debit outcome, credit USD); 2 holders = 8
    }

    [Fact]
    public async Task ResolveMarketAsync_NetZeroBalanceHolder_ExcludedFromSettlement()
    {
        var holderA = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;
        var entries = new List<LedgerEntry>
        {
            new(holderA, "DRAKE_WIN", 2m, EntryType.Debit, ts),
            new(holderA, "DRAKE_WIN", 2m, EntryType.Credit, ts) // net 0
        };
        var (settlementService, _) = CreateService(holderEntries: entries);
        var settlementAccountId = Guid.NewGuid();

        var (accountsSettled, totalUsdPaidOut) = await settlementService.ResolveMarketAsync(
            "o1", "DRAKE_WIN", settlementAccountId, 1.00m);

        Assert.Equal(0, accountsSettled);
        Assert.Equal(0, totalUsdPaidOut);
    }

    private static (SettlementService Service, SettlementMockLedgerRepo MockRepo) CreateService(IReadOnlyList<LedgerEntry> holderEntries)
    {
        var mockRepo = new SettlementMockLedgerRepo(holderEntries);
        var uow = new SettlementMockUnitOfWork();
        var accountingService = new AccountingService(mockRepo, uow);
        var settlementService = new SettlementService(mockRepo, accountingService);
        return (settlementService, mockRepo);
    }

    private sealed class SettlementMockUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class SettlementMockLedgerRepo : ILedgerEntryRepository
    {
        private readonly List<LedgerEntry> _entriesByAssetType;
        public List<LedgerEntry> CapturedSettlementEntries { get; } = new();
        public bool AddRangeAsyncCalled { get; private set; }

        public SettlementMockLedgerRepo(IReadOnlyList<LedgerEntry> entriesForGetByAssetType)
        {
            _entriesByAssetType = entriesForGetByAssetType?.ToList() ?? new List<LedgerEntry>();
        }

        public Task AddRangeAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken = default)
        {
            AddRangeAsyncCalled = true;
            CapturedSettlementEntries.AddRange(entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LedgerEntry>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            var list = _entriesByAssetType.Where(e => e.AccountId == accountId).ToList();
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
        }

        public Task<IReadOnlyList<LedgerEntry>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default)
        {
            var list = _entriesByAssetType
                .Where(e => string.Equals(e.AssetType, assetType?.Trim(), StringComparison.Ordinal))
                .ToList();
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
        }
    }
}
