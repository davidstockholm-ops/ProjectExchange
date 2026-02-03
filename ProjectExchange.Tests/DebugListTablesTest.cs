using System.Data;
using Microsoft.EntityFrameworkCore;
using ProjectExchange.Core.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace ProjectExchange.Tests;

/// <summary>
/// Tillfälligt debug-test: listar tabellnamn med rå SQL.
/// PostgreSQL: SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';
/// SQLite (standard test-DB): sqlite_master.
/// </summary>
public class DebugListTablesTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public DebugListTablesTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ListTables_Output()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        // Exakt fråga för PostgreSQL; SQLite saknar information_schema så vi fallback till sqlite_master
        var isPostgres = connection.GetType().Name.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase);
        var query = isPostgres
            ? "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';"
            : "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;

        var tableNames = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                tableNames.Add(name);
            }
        }

        _testOutputHelper.WriteLine($"Tables ({tableNames.Count}):");
        foreach (var name in tableNames)
        {
            _testOutputHelper.WriteLine("  " + name);
            Console.WriteLine("  " + name);
        }
    }
}
