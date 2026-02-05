using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using ProjectExchange.Core.Settlement;

namespace ProjectExchange.Tests;

/// <summary>
/// Integration tests for SettlementGateway: verifies Polly Circuit Breaker activates after repeated failures
/// according to configuration (aligned with appsettings.json: FailureRatio 0.5, MinimumThroughput 2).
/// </summary>
public class ResilienceTests
{
    [Fact]
    public async Task SettlementGateway_AfterEnoughFailures_OpensCircuitAndThrowsBrokenCircuitException()
    {
        var callCount = 0;
        var failingHandler = new DelegatingHandlerStub(() =>
        {
            Interlocked.Increment(ref callCount);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        var client = new HttpClient(failingHandler) { BaseAddress = new Uri("http://localhost/") };

        var options = new SettlementGatewayOptions
        {
            SettlementNotifyUrl = "http://localhost/notify",
            RetryCount = 1,
            RetryDelayMs = 10,
            CircuitFailureRatio = 0.5,
            CircuitMinimumThroughput = 2,
            CircuitBreakDurationSeconds = 5
        };

        var logger = NullLogger<SettlementGateway>.Instance;
        var gateway = new SettlementGateway(client, logger, Options.Create(options));

        // First call(s) fail; with RetryCount=1 we get 2 attempts per call, so the circuit may open on the first call.
        // Accept either HttpRequestException (failure) or BrokenCircuitException (circuit already open).
        await AssertThrowsFailureOrBrokenCircuitAsync(() => gateway.NotifySettlementAsync("outcome-1", "WIN", 1, 100m));
        await AssertThrowsFailureOrBrokenCircuitAsync(() => gateway.NotifySettlementAsync("outcome-2", "WIN", 1, 100m));

        int countAfterFailures = callCount;

        // Next call must be rejected by open circuit without calling the handler again.
        var ex = await Assert.ThrowsAsync<BrokenCircuitException>(() =>
            gateway.NotifySettlementAsync("outcome-3", "WIN", 1, 100m));

        Assert.Contains("open", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(countAfterFailures, callCount);
    }

    private static async Task AssertThrowsFailureOrBrokenCircuitAsync(Func<Task> act)
    {
        var ex = await Record.ExceptionAsync(act);
        Assert.NotNull(ex);
        Assert.True(ex is HttpRequestException or BrokenCircuitException,
            $"Expected HttpRequestException or BrokenCircuitException, got {ex.GetType().Name}");
    }

    private sealed class DelegatingHandlerStub : DelegatingHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        public DelegatingHandlerStub(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory());
        }
    }
}
