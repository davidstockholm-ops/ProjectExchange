using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Markets;
using ProjectExchange.Core.Social;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Project Exchange API",
        Version = "v1",
        Description = "Clearing & Settlement. **Market** (api/markets): base/create, flash/create, celebrity/create, celebrity/simulate, outcome-reached, active, orderbook. **Wallet** (api/wallet): create account."
    });
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "ProjectExchange.Core.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
    options.TagActionsBy(api => new[] { api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
});
builder.Services.AddControllers();

// PostgreSQL + EF Core: DbContext and unit of work (scoped per request)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ProjectExchangeDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProjectExchangeDbContext>());

// Accounting: EF repositories and LedgerService (scoped)
builder.Services.AddScoped<IAccountRepository, EfAccountRepository>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddScoped<LedgerService>();

// Shared order books (singleton) and matching engine (scoped)
builder.Services.AddSingleton<IOrderBookStore, OrderBookStore>();
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
