using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// CLI: truncate clearing/ledger tables (no psql required). Usage: dotnet run --project ProjectExchange.Core -- --truncate-tables
// Truncates each table separately so missing tables (e.g. Orders) are skipped.
if (args.Contains("--truncate-tables"))
{
    var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>().UseNpgsql(connectionString).Options;
    using var ctx = new ProjectExchangeDbContext(options);
    // Whitelist: only these table names are ever used (no user input) to avoid SQL injection.
    var tablePairs = new (string Snake, string Pascal)[]
    {
        ("journal_entries", "\"JournalEntries\""),
        ("transactions", "\"Transactions\""),
        ("ledger_entries", "\"LedgerEntries\""),
        ("orders", "\"Orders\"")
    };
    var truncated = new List<string>();
    foreach (var (snake, pascal) in tablePairs)
    {
        var sqlSnake = GetTruncateSql(snake);
        var sqlPascal = GetTruncateSql(pascal);
        try
        {
            ctx.Database.ExecuteSqlRaw(sqlSnake);
            truncated.Add(snake);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            try
            {
                ctx.Database.ExecuteSqlRaw(sqlPascal);
                truncated.Add(pascal);
            }
            catch (Npgsql.PostgresException ex2) when (ex2.SqlState == "42P01") { /* table does not exist, skip */ }
        }
    }

    static string GetTruncateSql(string tableName)
    {
        // Only allow known identifiers; no interpolation of user input.
        return tableName switch
        {
            "journal_entries" => "TRUNCATE TABLE journal_entries RESTART IDENTITY CASCADE;",
            "transactions" => "TRUNCATE TABLE transactions RESTART IDENTITY CASCADE;",
            "ledger_entries" => "TRUNCATE TABLE ledger_entries RESTART IDENTITY CASCADE;",
            "orders" => "TRUNCATE TABLE orders RESTART IDENTITY CASCADE;",
            "\"JournalEntries\"" => "TRUNCATE TABLE \"JournalEntries\" RESTART IDENTITY CASCADE;",
            "\"Transactions\"" => "TRUNCATE TABLE \"Transactions\" RESTART IDENTITY CASCADE;",
            "\"LedgerEntries\"" => "TRUNCATE TABLE \"LedgerEntries\" RESTART IDENTITY CASCADE;",
            "\"Orders\"" => "TRUNCATE TABLE \"Orders\" RESTART IDENTITY CASCADE;",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unknown table name.")
        };
    }
    Console.WriteLine(truncated.Count > 0 ? $"Truncated: {string.Join(", ", truncated)}." : "No tables truncated (none of the target tables exist).");
    return;
}

// CLI: rename __EFMigrationsHistory columns to snake_case (no psql required). Usage: dotnet run --project ProjectExchange.Core -- --upgrade-history-table
// Run once on existing DBs before using UseSnakeCaseNamingConvention so "dotnet ef database update" can read the history table.
if (args.Contains("--upgrade-history-table"))
{
    var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>().UseNpgsql(connectionString).Options;
    using var ctx = new ProjectExchangeDbContext(options);
    try
    {
        ctx.Database.ExecuteSqlRaw(@"ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN ""MigrationId"" TO migration_id;");
        ctx.Database.ExecuteSqlRaw(@"ALTER TABLE ""__EFMigrationsHistory"" RENAME COLUMN ""ProductVersion"" TO product_version;");
        Console.WriteLine("Upgraded __EFMigrationsHistory columns to snake_case. You can now run dotnet ef database update.");
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42703")
    {
        Console.WriteLine("Columns already snake_case or table missing: " + ex.Message);
        Console.WriteLine("If history table is already upgraded, run: dotnet ef database update --project ProjectExchange.Core");
    }
    return;
}

// CLI: clear migration history and re-apply all migrations (fixes "already up to date" but ledger_entries missing).
// Usage: dotnet run --project ProjectExchange.Core -- --reset-and-migrate
if (args.Contains("--reset-and-migrate"))
{
    var options = new DbContextOptionsBuilder<ProjectExchangeDbContext>()
        .UseNpgsql(connectionString)
        .Options;
    using var ctx = new ProjectExchangeDbContext(options);
    ctx.Database.ExecuteSqlRaw(@"DELETE FROM ""__EFMigrationsHistory"";");
    Console.WriteLine("Cleared __EFMigrationsHistory. Applying all migrations...");
    ctx.Database.Migrate();
    Console.WriteLine("Done. All migrations applied; tables (e.g. ledger_entries) should exist.");
    return;
}

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Project Exchange API",
        Version = "v1",
        Description = "Clearing & Settlement. **Market** (api/markets): base/create, flash/create, celebrity/create, celebrity/simulate, outcome-reached, active, orderbook. **Secondary** (api/secondary): order, book/{marketId}. **Wallet** (api/wallet): create account. **Portfolio** (api/portfolio/{accountId}): aggregated holdings from LedgerEntries. **Admin** (api/admin): resolve-market (settlement by outcome)."
    });
    // Include XML comments (from GenerateDocumentationFile). Use assembly name so path matches on case-sensitive CI (Linux).
    var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
    var xmlFileName = $"{assemblyName}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
    options.TagActionsBy(api => new[] { api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
});
builder.Services.AddControllers();
// Temporarily allow requests to reach the controller so we can log what is failing instead of silent 400.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

// PostgreSQL + EF Core: DbContext and unit of work (scoped per request)
builder.Services.AddDbContext<ProjectExchangeDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProjectExchangeDbContext>());

// Accounting: EF repositories, LedgerService, and AccountingService (double-entry outcome ledger)
builder.Services.AddScoped<IAccountRepository, EfAccountRepository>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddScoped<ILedgerEntryRepository, EfLedgerEntryRepository>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<SettlementService>();

// Shared order books (singleton) and matching engine (scoped)
builder.Services.AddSingleton<IOrderBookStore, OrderBookStore>();
builder.Services.AddSingleton<IOutcomeAssetTypeResolver, OutcomeAssetTypeResolver>();
builder.Services.AddScoped<IMatchingEngine, MockMatchingEngine>();
builder.Services.AddScoped<MarketService>();

// Social copy-trading: follow graph (singleton)
builder.Services.AddSingleton<CopyTradingService>();

// Oracle architecture: no circular deps. BaseOracleService takes IServiceProvider and resolves
// IOutcomeSettlementService only when NotifyOutcomeReachedAsync runs (Oracle → Agent → CopyTradingEngine → Oracle).
// Controllers use only IMarketOracle/IOutcomeOracle; they never take AutoSettlementAgent directly.
builder.Services.AddSingleton<AutoSettlementAgent>();
builder.Services.AddSingleton<IOutcomeSettlementService>(sp => sp.GetRequiredService<AutoSettlementAgent>());
builder.Services.AddSingleton<CopyTradingEngine>();
builder.Services.AddSingleton<IOutcomeOracle, CelebrityOracleService>();
// IMarketOracle → same instance as IOutcomeOracle (CelebrityOracleService implements both). MarketController injects IMarketOracle without error.
builder.Services.AddSingleton<IMarketOracle>(sp => sp.GetRequiredService<IOutcomeOracle>());
builder.Services.AddSingleton<FlashOracleService>();

var app = builder.Build();

// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProjectExchangeDbContext>();
    db.Database.Migrate();
}

// Market Holding accounts are created per outcome by CopyTradingEngine on first trade (Clearing & Settlement).

// Configure the HTTP request pipeline.
app.Use(async (context, next) =>
{
    Console.WriteLine($"[GLOBAL LOG] {context.Request.Method} {context.Request.Path}");
    await next();
});
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project Exchange API v1");
    c.DocumentTitle = "Project Exchange — Swagger UI";
});

app.UseHttpsRedirection();

app.MapControllers();

// Simple health check: GET /health returns 200 when the app is up
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ProjectExchange" }))
    .WithName("Health")
    .WithTags("Health");

app.Run();

// Enterprise CI Trigger
