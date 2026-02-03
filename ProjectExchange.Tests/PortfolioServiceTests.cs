using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Tests;

/// <summary>
/// Unit tests for PortfolioService.GetPortfolioAsync (aggregated holdings per account).
/// </summary>
public class PortfolioServiceTests
{
    [Fact]
    public async Task GetPortfolioAsync_EmptyAccount_ReturnsEmptyDictionary()
    {
        var repo = new PortfolioMockLedgerRepo(Array.Empty<LedgerEntry>());
        var service = new PortfolioService(repo);

        var portfolio = await service.GetPortfolioAsync(Guid.NewGuid().ToString());

        Assert.NotNull(portfolio);
        Assert.Empty(portfolio);
    }

    [Fact]
    public async Task GetPortfolioAsync_NullOrEmptyAccountId_ReturnsEmptyDictionary()
    {
        var repo = new PortfolioMockLedgerRepo(Array.Empty<LedgerEntry>());
        var service = new PortfolioService(repo);

        Assert.Empty(await service.GetPortfolioAsync(""));
        Assert.Empty(await service.GetPortfolioAsync("   "));
        Assert.Empty(await service.GetPortfolioAsync((string)null!));
    }

    [Fact]
    public async Task GetPortfolioAsync_InvalidGuid_ReturnsEmptyDictionary()
    {
        var repo = new PortfolioMockLedgerRepo(Array.Empty<LedgerEntry>());
        var service = new PortfolioService(repo);

        var portfolio = await service.GetPortfolioAsync("not-a-guid");

        Assert.NotNull(portfolio);
        Assert.Empty(portfolio);
    }

    [Fact]
    public async Task GetPortfolioAsync_OneAsset_DebitMinusCredit_ReturnsBalance()
    {
        var accountId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;
        var entries = new List<LedgerEntry>
        {
            new(accountId, "USD", 100m, EntryType.Debit, ts),
            new(accountId, "USD", 30m, EntryType.Credit, ts)
        };
        var repo = new PortfolioMockLedgerRepo(entries);
        var service = new PortfolioService(repo);

        var portfolio = await service.GetPortfolioAsync(accountId.ToString());

        Assert.Single(portfolio);
        Assert.Equal(70m, portfolio["USD"]);
    }

    [Fact]
    public async Task GetPortfolioAsync_MultipleAssets_ReturnsNonZeroBalancesOnly()
    {
        var accountId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;
        var entries = new List<LedgerEntry>
        {
            new(accountId, "USD", 50m, EntryType.Debit, ts),
            new(accountId, "DRAKE_WIN", 10m, EntryType.Debit, ts),
            new(accountId, "DRAKE_WIN", 10m, EntryType.Credit, ts) // net 0, excluded
        };
        var repo = new PortfolioMockLedgerRepo(entries);
        var service = new PortfolioService(repo);

        var portfolio = await service.GetPortfolioAsync(accountId.ToString());

        // DRAKE_WIN nets to 0 and is excluded by PortfolioService (.Where(kv => kv.Value != 0)); only USD remains
        Assert.Single(portfolio);
        Assert.Equal(50m, portfolio["USD"]);
        Assert.False(portfolio.ContainsKey("DRAKE_WIN"));
    }

    private sealed class PortfolioMockLedgerRepo : ILedgerEntryRepository
    {
        private readonly List<LedgerEntry> _entries;

        public PortfolioMockLedgerRepo(IReadOnlyList<LedgerEntry> entries)
        {
            _entries = entries?.ToList() ?? new List<LedgerEntry>();
        }

        public Task AddRangeAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<LedgerEntry>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            var list = _entries.Where(e => e.AccountId == accountId).ToList();
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(list);
        }

        public Task<IReadOnlyList<LedgerEntry>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(new List<LedgerEntry>());
        }
    }
}
