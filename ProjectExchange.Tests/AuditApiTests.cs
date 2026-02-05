using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Auditing;
using ProjectExchange.Core.Controllers;
using ProjectExchange.Core.Infrastructure.Persistence;

namespace ProjectExchange.Tests;

/// <summary>
/// Verifies AuditController returns status 200 and correctly formatted JSON when requesting events for a market.
/// </summary>
public class AuditApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetByMarketId_ValidMarketId_Returns200AndCorrectlyFormattedJson()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        context.DomainEvents.Add(new DomainEventEntity
        {
            EventType = "OrderPlaced",
            Payload = "{\"id\":\"a1b2c3\",\"price\":0.85}",
            OccurredAt = DateTimeOffset.UtcNow,
            MarketId = "drake-album",
            UserId = "user-1"
        });
        await context.SaveChangesAsync();

        var auditService = new AuditService(context);
        var controller = new AuditController(auditService);

        var result = await controller.GetByMarketId("drake-album");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var value = okResult.Value;
        Assert.NotNull(value);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(doc.ValueKind == JsonValueKind.Array, "Response should be a JSON array.");
        Assert.True(doc.GetArrayLength() >= 1, "Array should contain at least one event.");
        var first = doc[0];
        Assert.True(first.TryGetProperty("eventType", out _), "Each event should have eventType.");
        Assert.True(first.TryGetProperty("payload", out _), "Each event should have payload.");
        Assert.True(first.TryGetProperty("occurredAt", out _), "Each event should have occurredAt.");
        Assert.True(first.TryGetProperty("marketId", out _), "Each event should have marketId.");
        Assert.True(first.TryGetProperty("userId", out _), "Each event should have userId.");
        Assert.Equal("OrderPlaced", first.GetProperty("eventType").GetString());
        Assert.Equal("drake-album", first.GetProperty("marketId").GetString());
    }

    [Fact]
    public async Task GetByMarketId_EmptyMarketId_Returns400()
    {
        var context = EnterpriseTestSetup.CreateFreshDbContext();
        var controller = new AuditController(new AuditService(context));

        var result = await controller.GetByMarketId("");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
