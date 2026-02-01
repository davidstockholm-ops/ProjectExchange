using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Drake;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Tests;

/// <summary>
/// Provides a fresh SQLite in-memory database and enterprise services for each test.
/// Connection is opened manually and kept open for the duration of the test so the DB persists.
/// SQLite supports BeginTransactionAsync natively, so all MarketService tests run without warnings.
/// </summary>
public static class EnterpriseTestSetup
{
    /// <summary>
    /// Creates a fresh SQLite in-memory <see cref="ProjectExchangeDbContext"/> (unique DB per call).
    /// </summary>
    public static ProjectExchangeDbContext CreateFreshDbContext()
    {
        return CreateFreshDbContext(Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates a fresh SQLite in-memory <see cref="ProjectExchangeDbContext"/>.
    /// Use a fixed name when multiple scopes must share the same DB (e.g. concurrent tests) — same connection is reused via singleton context.
    /// Connection is opened manually and kept open; schema is created with EnsureCreated().
    /// </summary>
    public static ProjectExchangeDbContext CreateFreshDbContext(string databaseName)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ProjectExchangeDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Builds a service provider that uses the given context as a singleton so all scopes share the same DB.
    /// Use this for CopyTradingEngine and AutoSettlementAgent so their scopes see the same data.
    /// </summary>
    public static IServiceProvider CreateServiceProvider(ProjectExchangeDbContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IUnitOfWork>(sp => sp.GetRequiredService<ProjectExchangeDbContext>());
        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<ITransactionRepository, EfTransactionRepository>();
        services.AddScoped<LedgerService>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Market stack including the order book store (for tests that need to create DrakeOracleService with same store).
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        ITransactionRepository TransactionRepo,
        LedgerService LedgerService,
        MarketService MarketService,
        IOrderBookStore OrderBookStore) CreateMarketStackWithStore()
    {
        var context = CreateFreshDbContext();
        var provider = CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService);
        return (accountRepo, transactionRepo, ledgerService, marketService, orderBookStore);
    }

    /// <summary>
    /// Ledger-only stack: AccountRepo, TransactionRepo, LedgerService using EF repositories and a fresh SQLite in-memory DB.
    /// </summary>
    public static (IAccountRepository AccountRepo, ITransactionRepository TransactionRepo, LedgerService LedgerService) CreateLedger()
    {
        var context = CreateFreshDbContext();
        var accountRepo = new EfAccountRepository(context);
        var transactionRepo = new EfTransactionRepository(context);
        var ledgerService = new LedgerService(transactionRepo, accountRepo, context);
        return (accountRepo, transactionRepo, ledgerService);
    }

    /// <summary>
    /// Market stack: AccountRepo, TransactionRepo, LedgerService, MarketService using EF and a fresh SQLite in-memory DB.
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        ITransactionRepository TransactionRepo,
        LedgerService LedgerService,
        MarketService MarketService) CreateMarketStack()
    {
        var (accountRepo, transactionRepo, ledgerService, marketService, _) = CreateMarketStackWithStore();
        return (accountRepo, transactionRepo, ledgerService, marketService);
    }

    /// <summary>
    /// Social stack: AccountRepo, LedgerService, CopyTradingService, MarketService using EF and a fresh SQLite in-memory DB.
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService) CreateSocialStack()
    {
        var context = CreateFreshDbContext();
        var provider = CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService);
        return (accountRepo, ledgerService, copyTradingService, marketService);
    }

    /// <summary>
    /// Drake stack: AccountRepo, LedgerService, Oracle, CopyTradingEngine, AutoSettlementAgent using EF and a fresh SQLite in-memory DB.
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        DrakeOracleService Oracle,
        CopyTradingEngine CopyTradingEngine,
        AutoSettlementAgent AutoSettlementAgent) CreateDrakeStack()
    {
        var context = CreateFreshDbContext();
        var provider = CreateServiceProvider(context);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var orderBookStore = new OrderBookStore();
        var oracle = new DrakeOracleService(orderBookStore);
        var copyTradingEngine = new CopyTradingEngine(scopeFactory, oracle);
        var autoSettlementAgent = new AutoSettlementAgent(scopeFactory, copyTradingEngine);

        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        return (accountRepo, ledgerService, oracle, copyTradingEngine, autoSettlementAgent);
    }

    /// <summary>
    /// Full stack: AccountRepo, LedgerService, CopyTradingService, MarketService, DrakeOracleService using EF and a fresh SQLite in-memory DB.
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService,
        DrakeOracleService Oracle) CreateFullStack()
    {
        var (accountRepo, ledgerService, copyTradingService, marketService, oracle, _) = CreateFullStackWithContext();
        return (accountRepo, ledgerService, copyTradingService, marketService, oracle);
    }

    /// <summary>
    /// Full stack plus the DbContext (for integrity tests that query the same DB).
    /// </summary>
    public static (
        IAccountRepository AccountRepo,
        LedgerService LedgerService,
        CopyTradingService CopyTradingService,
        MarketService MarketService,
        DrakeOracleService Oracle,
        ProjectExchangeDbContext Context) CreateFullStackWithContext()
    {
        var context = CreateFreshDbContext();
        var provider = CreateServiceProvider(context);
        using var scope = provider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var orderBookStore = new OrderBookStore();
        var copyTradingService = new CopyTradingService();
        var marketService = new MarketService(orderBookStore, accountRepo, transactionRepo, context, copyTradingService, ledgerService);
        var oracle = new DrakeOracleService(orderBookStore);
        return (accountRepo, ledgerService, copyTradingService, marketService, oracle, context);
    }
}
