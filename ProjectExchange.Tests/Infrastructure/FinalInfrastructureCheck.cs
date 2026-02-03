using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Core.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace ProjectExchange.Tests.Infrastructure;

/// <summary>
/// Final infrastructure check: inline migration, explicit public schema, raw SQL to list tables, and write/read against public.ledger_entries.
/// All table names are lowercase/snake_case (ledger_entries, accounts, etc.) to match Postgres after UseSnakeCaseNamingConvention.
/// Requires ConnectionStrings__DefaultConnection (env).
/// </summary>
public class FinalInfrastructureCheck
{
    private readonly ITestOutputHelper _output;

    public FinalInfrastructureCheck(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(cs) ? null : cs;
    }

    [Fact]
    public async Task Migrate_ListTables_WriteAndReadLedgerEntry_Succeeds()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine("Skipped: set ConnectionStrings__DefaultConnection to run against real PostgreSQL.");
            return;
        }

        var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ProjectExchangeDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;

        // 1. Inline migration: create own DbContext and run Migrate() first.
        await using (var ctx = new ProjectExchangeDbContext(options))
        {
            _output.WriteLine("Running Migrate()...");
            ctx.Database.Migrate();
            _output.WriteLine("Migrate() completed.");
        }

        await using (var ctx = new ProjectExchangeDbContext(options))
        {
            Assert.True(await ctx.Database.CanConnectAsync(), "Database must be reachable.");

            // 2. Raw SQL: list tables in public schema and write to log.
            var tableNames = await ctx.Database
                .SqlQueryRaw<string>("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name")
                .ToListAsync();
            _output.WriteLine($"Tables in public schema ({tableNames.Count}): {string.Join(", ", tableNames)}");

            // 3. Explicit schema and lowercase table name: public.ledger_entries (not PascalCase).
            var countSql = "SELECT COUNT(*) FROM public.ledger_entries";
            var countBefore = await ctx.Database.SqlQueryRaw<long>(countSql).FirstOrDefaultAsync();
            _output.WriteLine($"Count from public.ledger_entries before insert: {countBefore}");

            // 4. Write: insert one row into public.ledger_entries.
            var id = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var assetType = "final-check";
            var amount = 1.2345m;
            var direction = (int)EntryType.Credit;
            var timestamp = DateTimeOffset.UtcNow;

            var insertSql = @"
                INSERT INTO public.ledger_entries (id, account_id, asset_type, amount, direction, timestamp)
                VALUES ({0}, {1}, {2}, {3}, {4}, {5})";
            await ctx.Database.ExecuteSqlRawAsync(
                insertSql,
                id, accountId, assetType, amount, direction, timestamp);

            // 5. Read: select the row back from public.ledger_entries.
            var selectSql = "SELECT id FROM public.ledger_entries WHERE id = {0}";
            var readId = await ctx.Database.SqlQueryRaw<Guid>(selectSql, id).FirstOrDefaultAsync();
            Assert.Equal(id, readId);
            _output.WriteLine($"Write/Read OK: inserted and read back id {readId}");
        }
    }
}
