using System.Text.Json.Serialization;

namespace ProjectExchange.Agents.LPA.Models;

/// <summary>Request body for POST /api/secondary/order/bulk. All orders must use operatorId "mm-provider".</summary>
public record BulkOrderRequest(
    [property: JsonPropertyName("orders")] IReadOnlyList<BulkOrderItem> Orders);

/// <summary>Single order in a bulk request.</summary>
public record BulkOrderItem(
    [property: JsonPropertyName("marketId")] string MarketId,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("operatorId")] string OperatorId,
    [property: JsonPropertyName("userId")] string UserId);

/// <summary>Response from POST /api/secondary/order/bulk.</summary>
public record BulkOrderResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<BulkOrderResult> Results);

/// <summary>One result per order in the bulk response.</summary>
public record BulkOrderResult(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("matches")] IReadOnlyList<MatchLevel> Matches);

/// <summary>Single match in a result.</summary>
public record MatchLevel(
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("buyerUserId")] string BuyerUserId,
    [property: JsonPropertyName("sellerUserId")] string SellerUserId);
