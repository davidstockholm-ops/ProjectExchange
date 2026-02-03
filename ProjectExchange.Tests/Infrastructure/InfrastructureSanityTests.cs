using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;

namespace ProjectExchange.Tests.Infrastructure;

/// <summary>
/// Sanity tests for infrastructure: DbContext naming, real DB connection, and settlement SQL path.
/// DatabaseConnection and SettlementSmokeTest require real PostgreSQL; set ConnectionStrings__DefaultConnection
/// (e.g. in env or launchSettings) to run them. Without it they pass without hitting the DB.
/// </summary>
public class InfrastructureSanityTests
{
    private static string? GetRealConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(cs) ? null : cs;
    }

    /// <summary>
    /// Verifierar att ProjectExchangeDbContext mappar entiteter till snake_case-tabeller
    /// (t.ex. LedgerEntryEntity → ledger_entries) så att PostgreSQL inte får "relation does not exist".
    /// </summary>
    [Fact]
    public void SnakeCaseNaming_DbContext_MapsEntitiesToSnakeCaseTables()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accounts",
            "transactions",
            "journal_entries",
            "ledger_entries",
            "orders"
        };

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            Assert.True(!string.IsNullOrEmpty(tableName), $"Entity {entityType.ClrType.Name} has no table name.");
            Assert.True(
                expectedTables.Contains(tableName!),
                $"Table '{tableName}' should be one of: {string.Join(", ", expectedTables)}.");
            Assert.Equal(tableName, tableName!.ToLowerInvariant());
        }

        Assert.Equal("ledger_entries", context.Model.FindEntityType(typeof(LedgerEntryEntity))!.GetTableName());
    }

    /// <summary>
    /// Anropar den riktiga databasen: CanConnect och en enkel läsning mot ledger_entries.
    /// Säkerställer att anslutningen fungerar och att tabellen finns (inga "relation does not exist").
    /// Kräver ConnectionStrings__DefaultConnection (env eller launchSettings).
    /// </summary>
    [Fact]
    public async Task DatabaseConnection_CanConnectAndLedgerTableExists()
    {
        var connectionString = GetRealConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.True(true, "Skipped: set ConnectionStrings__DefaultConnection to run against real PostgreSQL.");
            return;
        }

        var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new ProjectExchangeDbContext(options);
        Assert.True(await ctx.Database.CanConnectAsync(), "Database must be reachable.");

        _ = await ctx.LedgerEntries.AnyAsync();
        // Om vi når hit utan 42P01 finns tabellen (snake_case eller PascalCase beroende på migration).
    }

    /// <summary>
    /// Integrationstest: kör ResolveMarketAsync i en transaktion som rullas tillbaka.
    /// Verifierar att hela kedjan (SettlementService → AccountingService → EfLedgerEntryRepository → SQL)
    /// fungerar mot riktig DB utan "relation does not exist".
    /// Kräver ConnectionStrings__DefaultConnection.
    /// </summary>
    [Fact]
    public async Task SettlementSmokeTest_ResolveMarketAsync_InRolledBackTransaction_NoRelationNotFound()
    {
        var connectionString = GetRealConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.True(true, "Skipped: set ConnectionStrings__DefaultConnection to run against real PostgreSQL.");
            return;
        }

        var services = new ServiceCollection();
        services.AddScoped<ProjectExchangeDbContext>(_ =>
        {
            var opts = new DbContextOptionsBuilder<ProjectExchangeDbContext>().UseNpgsql(connectionString).Options;
            return new ProjectExchangeDbContext(opts);
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProjectExchangeDbContext>());
        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<ITransactionRepository, EfTransactionRepository>();
        services.AddScoped<ILedgerEntryRepository, EfLedgerEntryRepository>();
        services.AddScoped<LedgerService>();
        services.AddScoped<AccountingService>();
        services.AddScoped<SettlementService>();

        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ProjectExchangeDbContext>();
        var settlementService = scope.ServiceProvider.GetRequiredService<SettlementService>();

        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            var (accountsSettled, totalUsdPaidOut) = await settlementService.ResolveMarketAsync(
                "smoke-test",
                "SMOKE_ASSET",
                Guid.NewGuid(),
                1.00m);
            await transaction.RollbackAsync();
            Assert.True(accountsSettled >= 0);
            Assert.True(totalUsdPaidOut >= 0);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException(
                "Relation does not exist: ensure migrations are applied (dotnet ef database update) and tables use snake_case.", ex);
        }
    }
}
