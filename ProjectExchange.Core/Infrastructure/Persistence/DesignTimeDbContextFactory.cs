using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectExchange.Core.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core tools at design time (e.g. dotnet ef migrations add).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProjectExchangeDbContext>
{
    public ProjectExchangeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProjectExchangeDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=projectexchange;Username=postgres;Password=postgres";
        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new ProjectExchangeDbContext(optionsBuilder.Options);
    }
}
