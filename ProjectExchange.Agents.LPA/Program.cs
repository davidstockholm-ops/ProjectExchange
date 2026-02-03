using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ProjectExchange.Agents.LPA.Models;

// Defaults: only OperatorId has a default; MarketId must be set per instance for scalability
const string DefaultOperatorId = "mm-provider";
const decimal DefaultSeedBid = 0.45m;
const decimal DefaultSeedAsk = 0.55m;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var marketId = config["MarketId"]?.Trim();
if (string.IsNullOrEmpty(marketId))
    marketId = args.Length > 0 ? args[0].Trim() : null;
if (string.IsNullOrEmpty(marketId))
{
    Console.Error.WriteLine("MarketId is required. Set it in appsettings.json, or pass as first argument, or set env MarketId.");
    Console.Error.WriteLine("Example: dotnet run --project ProjectExchange.Agents.LPA -- <marketId>");
    return 1;
}

var baseUrl = config["BaseUrl"]?.Trim() ?? "http://localhost:5051";
var operatorId = config["OperatorId"]?.Trim() ?? DefaultOperatorId;
var seedBid = decimal.TryParse(config["SeedBidPrice"], out var sb) && sb >= 0 && sb <= 1 ? sb : DefaultSeedBid;
var seedAsk = decimal.TryParse(config["SeedAskPrice"], out var sa) && sa >= 0 && sa <= 1 ? sa : DefaultSeedAsk;
var spreadPercent = decimal.TryParse(config["SpreadPercent"], out var sp) && sp > 0 ? sp : 5m;
var orderQuantity = decimal.TryParse(config["OrderQuantity"], out var qty) && qty > 0 ? qty : 10m;
var intervalSeconds = int.TryParse(config["IntervalSeconds"], out var sec) && sec > 0 ? sec : 30;

if (seedBid >= seedAsk)
{
    Console.Error.WriteLine("SeedBidPrice must be less than SeedAskPrice.");
    return 1;
}

var userId = $"agent-lpa-{marketId}";
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

Console.WriteLine("LPA started. MarketId={0}, BaseUrl={1}, OperatorId={2}, Interval={3}s", marketId, baseUrl, operatorId, intervalSeconds);

while (true)
{
    try
    {
        var bookResponse = await http.GetFromJsonAsync<BookResponse>($"/api/secondary/book/{Uri.EscapeDataString(marketId)}", jsonOptions);
        if (bookResponse == null)
        {
            Console.WriteLine("[{0:O}] No book response for market; skipping tick.", DateTime.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            continue;
        }

        var isEmpty = IsBookEmpty(bookResponse);
        decimal bidPrice, askPrice;

        if (isEmpty)
        {
            bidPrice = seedBid;
            askPrice = seedAsk;
            Console.WriteLine("[{0:O}] Book empty; placing seed spread Bid@{1:F2} Ask@{2:F2}.", DateTime.UtcNow, bidPrice, askPrice);
        }
        else
        {
            var mid = GetMidPrice(bookResponse);
            var spread = mid * (spreadPercent / 100m);
            bidPrice = Math.Max(0.00m, Math.Round(mid - spread, 2));
            askPrice = Math.Min(1.00m, Math.Round(mid + spread, 2));
            if (bidPrice >= askPrice)
            {
                Console.WriteLine("[{0:O}] Mid={1:F2} spread too tight; skipping.", DateTime.UtcNow, mid);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
                continue;
            }
            Console.WriteLine("[{0:O}] Competitive spread around mid={1:F2}: Bid@{2:F2} Ask@{3:F2}.", DateTime.UtcNow, mid, bidPrice, askPrice);
        }

        var bulkRequest = new BulkOrderRequest(new[]
        {
            new BulkOrderItem(marketId, bidPrice, orderQuantity, "Buy", operatorId, userId),
            new BulkOrderItem(marketId, askPrice, orderQuantity, "Sell", operatorId, userId)
        });

        var response = await http.PostAsJsonAsync("/api/secondary/order/bulk", bulkRequest, jsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[{0:O}] Bulk order failed {1}: {2}", DateTime.UtcNow, response.StatusCode, body);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
            continue;
        }

        var bulkResponse = await response.Content.ReadFromJsonAsync<BulkOrderResponse>(jsonOptions);
        var totalMatches = bulkResponse?.Results?.Sum(r => r.Matches?.Count ?? 0) ?? 0;
        Console.WriteLine("[{0:O}] Bulk placed: {1} orders, {2} matches.", DateTime.UtcNow, bulkResponse?.Results?.Count ?? 0, totalMatches);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine("[{0:O}] HTTP error: {1}", DateTime.UtcNow, ex.Message);
    }
    catch (TaskCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine("[{0:O}] Error: {1}", DateTime.UtcNow, ex.Message);
    }

    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
}

return 0;

static bool IsBookEmpty(BookResponse book)
{
    var hasBids = book.Bids?.Count > 0;
    var hasAsks = book.Asks?.Count > 0;
    return !hasBids && !hasAsks;
}

static decimal GetMidPrice(BookResponse book)
{
    decimal? bestBid = book.Bids?.Count > 0 ? book.Bids.MaxBy(b => b.Price)?.Price : null;
    decimal? bestAsk = book.Asks?.Count > 0 ? book.Asks.MinBy(a => a.Price)?.Price : null;
    if (bestBid.HasValue && bestAsk.HasValue)
        return (bestBid.Value + bestAsk.Value) / 2;
    if (bestBid.HasValue) return bestBid.Value;
    if (bestAsk.HasValue) return bestAsk.Value;
    return (0.00m + 1.00m) / 2;
}
