using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Controllers;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies DI registration of IMarketOracle, IOutcomeSettlementService and that MarketController
/// can resolve IMarketOracle without error (same pattern as Program.cs).
/// </summary>
public class OracleDiResolutionTests
{
    /// <summary>
    /// IMarketOracle and IOutcomeOracle must resolve to the same instance (CelebrityOracleService)
    /// so that MarketController sees the same oracle for base/celebrity/outcome-reached.
    /// </summary>
    [Fact]
    public void IMarketOracle_and_IOutcomeOracle_resolve_to_same_instance()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateFullStackServiceProvider(context);

        var marketOracle = provider.GetRequiredService<IMarketOracle>();
        var outcomeOracle = provider.GetRequiredService<IOutcomeOracle>();

        Assert.Same(marketOracle, outcomeOracle);
        Assert.IsType<CelebrityOracleService>(marketOracle);
    }

    /// <summary>
    /// MarketController must be resolvable with all dependencies (IOutcomeOracle, FlashOracleService, MarketService, CopyTradingEngine, etc.).
    /// Ensures Program.cs-style registration allows the unified MarketController to resolve at runtime.
    /// </summary>
    [Fact]
    public void MarketController_can_be_resolved_with_all_dependencies()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateFullStackServiceProvider(context);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var controller = new MarketController(
            sp.GetRequiredService<IOutcomeOracle>(),
            sp.GetRequiredService<FlashOracleService>(),
            sp.GetRequiredService<MarketService>(),
            sp.GetRequiredService<CopyTradingEngine>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<LedgerService>(),
            sp.GetRequiredService<IWebHostEnvironment>());

        Assert.NotNull(controller);
    }

    /// <summary>
    /// IOutcomeSettlementService must resolve to AutoSettlementAgent so that BaseOracleService.NotifyOutcomeReachedAsync
    /// gets the correct implementation when it resolves the service lazily.
    /// </summary>
    [Fact]
    public void IOutcomeSettlementService_resolves_to_AutoSettlementAgent()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var provider = EnterpriseTestSetup.CreateFullStackServiceProvider(context);

        var settlementService = provider.GetRequiredService<IOutcomeSettlementService>();

        Assert.IsType<AutoSettlementAgent>(settlementService);
    }
}
