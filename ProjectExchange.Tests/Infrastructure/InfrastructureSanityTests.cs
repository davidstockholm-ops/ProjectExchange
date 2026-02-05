using System.Data;
using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace ProjectExchange.Tests.Infrastructure;

/// <summary>
/// Sanity tests for infrastructure: DbContext naming, real DB connection, and settlement SQL path.
/// DatabaseConnection and SettlementSmokeTest require real PostgreSQL; set ConnectionStrings__DefaultConnection
/// (e.g. in env or launchSettings) to run them. Without it they pass without hitting the DB.
/// In CI (GitHub Actions): the workflow sets ConnectionStrings__DefaultConnection to the service container
/// at Host=localhost;Port=5432;Database=projectexchange_test;Username=postgres;Password=postgres.
/// All contexts use UseSnakeCaseNamingConvention. All raw SQL uses lowercase only for tables and columns
/// (e.g. public.ledger_entries, value, amount); no double-quoted identifiers. Tables: ledger_entries, accounts, transactions, journal_entries, orders.
/// </summary>
public class InfrastructureSanityTests
{
    private readonly ITestOutputHelper _output;

    public InfrastructureSanityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetRealConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(cs) ? null : cs;
    }

    /// <summary>
    /// Applies pending EF migrations once per test run when running against a real PostgreSQL.
    /// Uses the same design-time factory as "dotnet ef database update" so migrations are found in Core.
    /// </summary>
    private static void EnsureMigrationsApplied(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        lock (MigrationsLock)
        {
            if (MigrationsApplied) return;
            // Use the same connection string as the test; explicit MigrationsAssembly so migrations are found.
            var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ProjectExchangeDbContext).Assembly.GetName().Name))
                .UseSnakeCaseNamingConvention()
                .Options;
            try
            {
                using (var ctx = new ProjectExchangeDbContext(options))
                {
                    ctx.Database.Migrate();
                }
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42703" && ex.Message.Contains("migration_id", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "__EFMigrationsHistory has PascalCase columns. Run once: dotnet run --project ProjectExchange.Core -- --upgrade-history-table",
                    ex);
            }
            MigrationsApplied = true;
        }
    }

    private static readonly object MigrationsLock = new();
    private static bool MigrationsApplied;

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
            "domain_events",
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

        EnsureMigrationsApplied(connectionString);

        var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var ctx = new ProjectExchangeDbContext(options);
        Assert.True(await ctx.Database.CanConnectAsync(), "Database must be reachable.");

        // Schema-qualified name from model (EF has no GetSchemaQualifiedTableName; build from GetSchema + GetTableName) or fallback.
        var entityType = ctx.Model.FindEntityType(typeof(LedgerEntryEntity));
        var schema = entityType?.GetSchema();
        var table = entityType?.GetTableName();
        var tableName = !string.IsNullOrEmpty(schema) && !string.IsNullOrEmpty(table)
            ? $"{schema}.{table}"
            : "public.ledger_entries";

        _output.WriteLine($"Testing table: {tableName}");

        using (var command = ctx.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM public.ledger_entries";
            if (ctx.Database.GetDbConnection().State != ConnectionState.Open)
                await ctx.Database.OpenConnectionAsync();
            var count = Convert.ToInt64(await command.ExecuteScalarAsync());
            Assert.True(count >= 0, "Table must exist (COUNT succeeded).");
        }
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

        EnsureMigrationsApplied(connectionString);

        var services = new ServiceCollection();
        services.AddScoped<ProjectExchangeDbContext>(_ =>
        {
            var opts = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options;
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
