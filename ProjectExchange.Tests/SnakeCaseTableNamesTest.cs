using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectExchange.Core.Infrastructure.Persistence;

namespace ProjectExchange.Tests;

/// <summary>
/// Ensures DbContext is configured with snake_case table names (e.g. ledger_entries not LedgerEntries)
/// so that PostgreSQL does not require quoted identifiers and migrations match.
/// </summary>
public class SnakeCaseTableNamesTest
{
    [Fact]
    public void DbContext_TableNames_AreSnakeCase()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accounts",
            "domain_events",
            "followers",
            "journal_entries",
            "ledger_entries",
            "orders",
            "transactions"
        };

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            Assert.True(!string.IsNullOrEmpty(tableName), $"Entity {entityType.ClrType.Name} has no table name.");
            Assert.True(
                expectedTables.Contains(tableName!),
                $"Table '{tableName}' should be one of: {string.Join(", ", expectedTables)}. Expected snake_case (e.g. ledger_entries).");
            Assert.True(
                tableName == tableName!.ToLowerInvariant(),
                $"Table '{tableName}' should be lowercase (snake_case convention).");
        }
    }

    [Fact]
    public void DbContext_LedgerEntriesEntity_MapsTo_ledger_entries()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var entityType = context.Model.FindEntityType(typeof(LedgerEntryEntity));
        Assert.NotNull(entityType);

        var tableName = entityType.GetTableName();
        Assert.Equal("ledger_entries", tableName);
    }
}
