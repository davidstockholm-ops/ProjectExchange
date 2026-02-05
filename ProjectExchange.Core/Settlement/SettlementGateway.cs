using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ProjectExchange.Core.Settlement;

/// <summary>
/// Settlement gateway with Polly Retry and Circuit Breaker. Calls optional external settlement endpoint when configured.
/// </summary>
public class SettlementGateway : ISettlementGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SettlementGateway> _logger;
    private readonly SettlementGatewayOptions _options;
    private readonly ResiliencePipeline _pipeline;

    public SettlementGateway(
        HttpClient httpClient,
        ILogger<SettlementGateway> logger,
        IOptions<SettlementGatewayOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new SettlementGatewayOptions();

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.RetryCount,
                Delay = TimeSpan.FromMilliseconds(_options.RetryDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<Exception>(),
                OnRetry = args =>
                {
                    _logger.LogWarning("[SettlementGateway] Retry {Attempt} after {Delay}ms", args.AttemptNumber + 1, _options.RetryDelayMs);
                    return new ValueTask();
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.CircuitFailureRatio,
                MinimumThroughput = _options.CircuitMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<Exception>(),
                OnOpened = args => { _logger.LogWarning("[SettlementGateway] Circuit breaker opened"); return new ValueTask(); },
                OnClosed = args => { _logger.LogInformation("[SettlementGateway] Circuit breaker closed"); return new ValueTask(); },
                OnHalfOpened = args => { _logger.LogInformation("[SettlementGateway] Circuit breaker half-open"); return new ValueTask(); }
            })
            .Build();
    }

    /// <inheritdoc />
    public async Task NotifySettlementAsync(string outcomeId, string winningAssetType, int accountsSettled, decimal totalUsdPaidOut, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SettlementNotifyUrl))
        {
            _logger.LogDebug("[SettlementGateway] No SettlementNotifyUrl configured; skipping notify.");
            return;
        }

        var payload = new { outcomeId, winningAssetType, accountsSettled, totalUsdPaidOut };
        try
        {
            await _pipeline.ExecuteAsync(async ct =>
            {
                var response = await _httpClient.PostAsJsonAsync(_options.SettlementNotifyUrl, payload, ct);
                response.EnsureSuccessStatusCode();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettlementGateway] Notify failed for outcome {OutcomeId}", outcomeId);
            throw;
        }
    }
}

/// <summary>Configuration for SettlementGateway. SettlementNotifyUrl empty = no external call (no-op).</summary>
public class SettlementGatewayOptions
{
    public const string SectionName = "SettlementGateway";
    public string SettlementNotifyUrl { get; set; } = "";
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;
    public double CircuitFailureRatio { get; set; } = 0.5;
    public int CircuitMinimumThroughput { get; set; } = 2;
    public int CircuitBreakDurationSeconds { get; set; } = 30;
}
